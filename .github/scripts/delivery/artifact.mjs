import { createHash } from 'node:crypto';
import { inflateRawSync } from 'node:zlib';
import { appendFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

const zipName = 'badge-lambda-linux-arm64.zip';
const maxArchiveBytes = 64 * 1024 * 1024;
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

const failureCategories = new Set([
  'input-invalid', 'repository-mismatch', 'ci-identity-mismatch', 'ci-run-ineligible',
  'artifact-selection', 'artifact-id-mismatch', 'artifact-expired', 'archive-digest-missing',
  'artifact-run-mismatch', 'archive-layout', 'archive-digest-mismatch', 'manifest-invalid',
  'manifest-identity-mismatch', 'zip-digest-mismatch', 'github-metadata', 'artifact-download',
  'storage-redirect', 'storage-download', 'response-too-large', 'source-not-on-master', 'unknown',
]);
class ArtifactFailure extends Error {
  constructor(category) {
    super('Delivery artifact validation failed.');
    this.category = failureCategories.has(category) ? category : 'unknown';
  }
}
function requireValue(condition, category = 'archive-layout') {
  if (!condition) throw new ArtifactFailure(category);
}

function positiveId(value) {
  return typeof value === 'string' && /^[1-9][0-9]*$/.test(value) && Number.isSafeInteger(Number(value));
}

function canonicalRunTimestamp(value) {
  requireValue(typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{3})?Z$/.test(value),
    'ci-run-ineligible');
  const timestamp = new Date(value);
  requireValue(Number.isFinite(timestamp.getTime()), 'ci-run-ineligible');
  const canonical = timestamp.toISOString();
  requireValue(canonical === value || canonical.replace('.000Z', 'Z') === value, 'ci-run-ineligible');
  return canonical;
}

function validateIdentity(selection) {
  requireValue(/^[A-Za-z0-9_-]+\/[A-Za-z0-9_.-]+$/.test(selection.repository ?? ''), 'input-invalid');
  requireValue(!['.', '..'].includes(selection.repository.split('/')[1]), 'input-invalid');
  for (const field of ['runId', 'attempt']) {
    requireValue(positiveId(selection[field]), 'input-invalid');
  }
  requireValue(/^[a-f0-9]{40}$/.test(selection.sha ?? ''), 'input-invalid');
  return selection;
}

export function validateSelection(selection) {
  validateIdentity(selection);
  requireValue(positiveId(selection.artifactId), 'input-invalid');
  return selection;
}

export function validateProvenance(selection, run, listing) {
  validateSelection(selection);
  requireValue(run.repository?.full_name === selection.repository &&
    run.head_repository?.full_name === selection.repository &&
    Number.isSafeInteger(run.repository?.id) && run.repository.id > 0 &&
    run.repository.id === run.head_repository.id, 'repository-mismatch');
  requireValue(run.id === Number(selection.runId) && run.run_attempt === Number(selection.attempt) &&
    run.head_sha === selection.sha, 'ci-identity-mismatch');
  requireValue(run.path === '.github/workflows/ci-cd.yml' && run.event === 'push' &&
    run.head_branch === 'master' && run.status === 'completed' && run.conclusion === 'success',
  'ci-run-ineligible');
  const ciRunCreatedAtUtc = canonicalRunTimestamp(run.created_at);
  requireValue(listing.total_count === 1 && listing.artifacts?.length === 1,
    'artifact-selection');
  const item = listing.artifacts[0];
  requireValue(item.id === Number(selection.artifactId) &&
    item.name === `lambda-zip-${selection.runId}-${selection.attempt}`,
  'artifact-id-mismatch');
  requireValue(item.expired !== true, 'artifact-expired');
  requireValue(item.expired === false, 'artifact-selection');
  requireValue(/^sha256:[a-f0-9]{64}$/.test(item.digest ?? ''), 'archive-digest-missing');
  requireValue(item.workflow_run?.id === run.id && item.workflow_run?.head_sha === selection.sha &&
    item.workflow_run?.head_branch === 'master' && item.workflow_run?.repository_id === run.repository.id &&
    item.workflow_run?.head_repository_id === run.repository.id, 'artifact-run-mismatch');
  return {
    repository: selection.repository, runId: selection.runId, artifactId: selection.artifactId,
    sha: selection.sha, attempt: selection.attempt, archiveSha256: item.digest.slice(7), ciRunCreatedAtUtc,
  };
}

async function verifyMasterHistory(sourceSha, apiJson) {
  try {
    // Resolve the branch explicitly: a tag named master is never a branch proof.
    const ref = await apiJson('/git/ref/heads/master');
    requireValue(ref?.ref === 'refs/heads/master' && ref.object?.type === 'commit' &&
      typeof ref.object.sha === 'string' && /^[a-f0-9]{40}$/.test(ref.object.sha), 'source-not-on-master');
    const masterSha = ref.object.sha;
    // GitHub describes HEAD relative to BASE. Master must equal or be ahead of source.
    // The resolved request SHA identifies HEAD; compare has no head_commit field.
    const comparison = await apiJson(`/compare/${sourceSha}...${masterSha}`);
    requireValue(comparison?.base_commit?.sha === sourceSha && comparison.merge_base_commit?.sha === sourceSha,
      'source-not-on-master');
    const { status, ahead_by: aheadBy, behind_by: behindBy, total_commits: totalCommits } = comparison;
    requireValue([aheadBy, behindBy, totalCommits].every(count => Number.isSafeInteger(count) && count >= 0) &&
      behindBy === 0 && totalCommits === aheadBy, 'source-not-on-master');
    requireValue((status === 'identical' && sourceSha === masterSha && aheadBy === 0) ||
      (status === 'ahead' && sourceSha !== masterSha && aheadBy > 0), 'source-not-on-master');
    return { sourceSha, masterSha, direction: 'source-to-master', status, aheadBy, behindBy, totalCommits };
  } catch {
    // Lookup, parsing and comparison failures share a finite category. External
    // exceptions may carry credentials, URLs or forged category properties.
    throw new ArtifactFailure('source-not-on-master');
  }
}

// Read only the two flat files produced by CI. Never extract artifact paths onto
// disk. Reject ZIP64, encryption, links, duplicate names and oversized expansion.
function readArchive(archive) {
  requireValue(archive.length <= maxArchiveBytes && archive.length >= 22, 'archive-layout');
  let end = archive.length - 22;
  while (end >= Math.max(0, archive.length - 65557)) {
    if (archive.readUInt32LE(end) === 0x06054b50 &&
      end + 22 + archive.readUInt16LE(end + 20) === archive.length) break;
    end--;
  }
  requireValue(end >= 0 && archive.readUInt32LE(end) === 0x06054b50, 'archive-layout');
  requireValue(archive.readUInt16LE(end + 4) === 0 && archive.readUInt16LE(end + 6) === 0 &&
    archive.readUInt16LE(end + 8) === 2 && archive.readUInt16LE(end + 10) === 2, 'archive-layout');
  const directoryStart = archive.readUInt32LE(end + 16);
  requireValue(directoryStart + archive.readUInt32LE(end + 12) === end, 'archive-layout');
  const files = new Map();
  let position = directoryStart;
  for (let index = 0; index < 2; index++) {
    requireValue(position + 46 <= end && archive.readUInt32LE(position) === 0x02014b50, 'archive-layout');
    const flags = archive.readUInt16LE(position + 8);
    const method = archive.readUInt16LE(position + 10);
    const compressedBytes = archive.readUInt32LE(position + 20);
    const bytes = archive.readUInt32LE(position + 24);
    const nameBytes = archive.readUInt16LE(position + 28);
    const extraBytes = archive.readUInt16LE(position + 30);
    const commentBytes = archive.readUInt16LE(position + 32);
    const local = archive.readUInt32LE(position + 42);
    const next = position + 46 + nameBytes + extraBytes + commentBytes;
    requireValue(next <= end, 'archive-layout');
    const nameBuffer = archive.subarray(position + 46, position + 46 + nameBytes);
    const name = nameBuffer.toString('utf8');
    requireValue((name === zipName || name === 'provenance.json') && !files.has(name), 'archive-layout');
    requireValue((flags & ~0x808) === 0 && (method === 0 || method === 8) &&
      archive.readUInt16LE(position + 34) === 0, 'archive-layout');
    const mode = archive.readUInt32LE(position + 38) >>> 16;
    requireValue((mode & 0xf000) === 0 || (mode & 0xf000) === 0x8000, 'archive-layout');
    const limit = name === zipName ? maxArchiveBytes : 16384;
    requireValue(bytes > 0 && bytes <= limit && local + 30 <= directoryStart &&
      archive.readUInt32LE(local) === 0x04034b50, 'archive-layout');
    requireValue(archive.readUInt16LE(local + 6) === flags && archive.readUInt16LE(local + 8) === method &&
      archive.readUInt16LE(local + 26) === nameBytes, 'archive-layout');
    const dataStart = local + 30 + nameBytes + archive.readUInt16LE(local + 28);
    requireValue(dataStart + compressedBytes <= directoryStart &&
      archive.subarray(local + 30, local + 30 + nameBytes).equals(nameBuffer), 'archive-layout');
    const compressed = archive.subarray(dataStart, dataStart + compressedBytes);
    const contents = method === 0 ? compressed : inflateRawSync(compressed, { maxOutputLength: limit });
    requireValue(contents.length === bytes, 'archive-layout');
    files.set(name, contents);
    position = next;
  }
  requireValue(position === end && files.has(zipName) && files.has('provenance.json'), 'archive-layout');
  return files;
}

export function verifyArchive(archive, evidence) {
  requireValue(sha256(archive) === evidence.archiveSha256, 'archive-digest-mismatch');
  const files = readArchive(archive);
  const manifest = JSON.parse(files.get('provenance.json').toString('utf8'));
  requireValue(manifest?.schemaVersion === 1, 'manifest-invalid');
  for (const field of ['repository', 'runId', 'sha', 'attempt']) {
    requireValue(manifest[field] === evidence[field], 'manifest-identity-mismatch');
  }
  const zip = files.get(zipName);
  requireValue(manifest.zipSha256 === sha256(zip) && manifest.zipBytes === zip.length,
    'zip-digest-mismatch');
  return { zip, provenance: { ...evidence, zipSha256: manifest.zipSha256, zipBytes: zip.length } };
}

async function readLimited(response, limit) {
  requireValue(response.body, 'artifact-download');
  const chunks = [];
  let size = 0;
  for await (const chunk of response.body) {
    size += chunk.byteLength;
    requireValue(size <= limit, 'response-too-large');
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

export async function downloadPromotion(selection, { token, fetchImpl = fetch } = {}) {
  validateSelection(selection);
  requireValue(typeof token === 'string' && token.length > 0, 'input-invalid');
  const base = `https://api.github.com/repos/${selection.repository}`;
  const headers = {
    authorization: `Bearer ${token}`, accept: 'application/vnd.github+json',
    'x-github-api-version': '2022-11-28',
  };
  async function apiJson(path) {
    const response = await fetchImpl(`${base}${path}`, { headers, redirect: 'manual', signal: AbortSignal.timeout(30000) });
    requireValue(response.status === 200, 'github-metadata');
    return JSON.parse((await readLimited(response, 1024 * 1024)).toString('utf8'));
  }
  const run = await apiJson(`/actions/runs/${selection.runId}`);
  const name = `lambda-zip-${selection.runId}-${selection.attempt}`;
  const listing = await apiJson(`/actions/runs/${selection.runId}/artifacts?name=${name}&per_page=100`);
  const evidence = validateProvenance(selection, run, listing);
  evidence.masterHistory = await verifyMasterHistory(selection.sha, apiJson);
  const response = await fetchImpl(`${base}/actions/artifacts/${selection.artifactId}/zip`, {
    headers, redirect: 'manual', signal: AbortSignal.timeout(30000),
  });
  requireValue(response.status === 302, 'artifact-download');
  const location = new URL(response.headers.get('location'));
  requireValue(location.protocol === 'https:' && !location.username && !location.password && !location.port &&
    /^[a-z0-9]+\.blob\.core\.windows\.net$/.test(location.hostname), 'storage-redirect');
  // Signed storage URL is supplied only by GitHub. Never forward the GitHub token.
  const archiveResponse = await fetchImpl(location.href, { redirect: 'manual', signal: AbortSignal.timeout(30000) });
  requireValue(archiveResponse.status === 200, 'storage-download');
  const archive = await readLimited(archiveResponse, maxArchiveBytes);
  const verified = verifyArchive(archive, evidence);
  return { ...verified, provenance: { ...verified.provenance, archiveBytes: archive.length } };
}

async function main(command, env) {
  const identity = {
    repository: env.GITHUB_REPOSITORY, sha: env.GITHUB_SHA,
    runId: env.GITHUB_RUN_ID, attempt: env.GITHUB_RUN_ATTEMPT,
  };
  if (command === 'record') {
    validateIdentity(identity);
    const zip = await readFile(`artifacts/${zipName}`);
    requireValue(zip.length > 0 && zip.length <= maxArchiveBytes, 'archive-layout');
    await writeFile('artifacts/provenance.json', `${JSON.stringify({
      schemaVersion: 1, ...identity, zipSha256: sha256(zip), zipBytes: zip.length,
    }, null, 2)}\n`);
    return;
  }
  requireValue(command === 'promote', 'input-invalid');
  const selection = {
    repository: env.GITHUB_REPOSITORY, runId: env.CI_RUN_ID, artifactId: env.CI_ARTIFACT_ID,
    sha: env.CI_HEAD_SHA, attempt: env.CI_RUN_ATTEMPT,
  };
  const { zip, provenance } = await downloadPromotion(selection, { token: env.GITHUB_TOKEN });
  await mkdir('artifacts', { recursive: true });
  // Every output path is fixed; artifact-provided names are never filesystem paths.
  await writeFile(`artifacts/${zipName}`, zip);
  await writeFile('artifacts/verified-provenance.json', `${JSON.stringify(provenance, null, 2)}\n`);
  if (env.GITHUB_OUTPUT) await appendFile(env.GITHUB_OUTPUT, `source_sha=${selection.sha}\n`);
  if (env.GITHUB_STEP_SUMMARY) await appendFile(env.GITHUB_STEP_SUMMARY,
    `## Verified delivery artifact\n\n\`\`\`json\n${JSON.stringify(provenance, null, 2)}\n\`\`\`\n`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv[2], process.env).catch(error => {
    // Fetch errors can contain signed storage URLs. Do not expose them in logs.
    const category = error instanceof ArtifactFailure && failureCategories.has(error.category) ? error.category : 'unknown';
    console.error(`Delivery artifact validation failed [category=${category}]; no artifact was approved.`);
    process.exitCode = 1;
  });
}
