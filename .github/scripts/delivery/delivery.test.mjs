import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { deflateRawSync } from 'node:zlib';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const artifact = await import('./artifact.mjs');
const smoke = await import('./smoke.mjs');

const smokePaths = {
  testBadgePath: '/badges/tests/linux/fixture/service/feature%2Fdelivery',
  nugetBadgePath: '/badges/packages/nuget/Fixture.Package',
};
const smokeConfig = { ...smokePaths, apiUrl: 'https://gateway.invalid/prod/', customDomainUrl: 'https://public.invalid/' };
const healthy = { status: 'Healthy', time_stamp: '2026-10-16T00:00:00+00:00' };
const testBadge = { schemaVersion: 1, label: 'tests', message: '1 passed', color: 'green' };
const nugetBadge = { schemaVersion: 1, label: 'nuget', message: '1.0.0', color: 'blue' };
const goodResponse = url => Response.json(url.endsWith('/health') ? healthy : url.includes('/badges/tests/') ? testBadge : nugetBadge);

test('smoke verifies six GET JSON checks across Gateway stage and public origin', async () => {
  assert.equal(typeof smoke.runSmoke, 'function');
  const requested = [];
  const result = await smoke.runSmoke(smokeConfig, { fetch: async (url, options) => {
    requested.push([url, options.method, options.redirect]);
    assert.equal(options.signal.aborted, false);
    return goodResponse(url);
  } });
  assert.equal(result.checks, 6);
  assert.deepEqual(requested, [
    ['https://gateway.invalid/prod/health', 'GET', 'manual'],
    ['https://gateway.invalid/prod/badges/tests/linux/fixture/service/feature%2Fdelivery', 'GET', 'manual'],
    ['https://gateway.invalid/prod/badges/packages/nuget/Fixture.Package', 'GET', 'manual'],
    ['https://public.invalid/health', 'GET', 'manual'],
    ['https://public.invalid/badges/tests/linux/fixture/service/feature%2Fdelivery', 'GET', 'manual'],
    ['https://public.invalid/badges/packages/nuget/Fixture.Package', 'GET', 'manual'],
  ]);
});

test('smoke rejects unsafe output origins before making any GET', async () => {
  for (const apiUrl of ['http://gateway.invalid', 'https://user:password@gateway.invalid',
    'https://gateway.invalid/?query=secret', 'https://gateway.invalid/#fragment',
    'https://gateway.invalid/prod/../other', 'https://gateway.invalid/prod%2fsecret',
    'https://gateway.invalid/\n', 'not a url']) {
    let requests = 0;
    await assert.rejects(smoke.runSmoke({ ...smokeConfig, apiUrl }, { fetch: async () => { requests++; return Response.json(healthy); } }));
    assert.equal(requests, 0);
  }
});

test('smoke rejects HTML, malformed JSON, redirects, permanent status and invalid health without retry', async () => {
  const responses = [
    () => new Response('<html>ok</html>', { headers: { 'content-type': 'text/html' } }),
    () => new Response('{', { headers: { 'content-type': 'application/json' } }),
    () => new Response(null, { status: 302, headers: { location: 'https://elsewhere.invalid/' } }),
    () => new Response('{}', { status: 404 }),
    () => Response.json({ ...healthy, status: 'Degraded' }),
    () => Response.json({ ...healthy, time_stamp: 'garbage' }),
    () => Response.json({ status: 'Healthy' }),
    () => Response.json(null),
  ];
  for (const response of responses) {
    let requests = 0;
    await assert.rejects(smoke.runSmoke(smokeConfig, { fetch: async () => { requests++; return response(); } }));
    assert.equal(requests, 1);
  }
});

test('smoke rejects missing-data and malformed badge contracts without retry', async () => {
  for (const badge of [
    { ...testBadge, schemaVersion: 2 }, { ...testBadge, label: 'nuget' },
    { ...testBadge, message: '' }, { ...testBadge, message: '  ' },
    { ...testBadge, isError: true }, { ...testBadge, message: null },
    { ...testBadge, isError: 'true' }, { ...testBadge, isError: 1 },
  ]) {
    let requests = 0;
    await assert.rejects(smoke.runSmoke(smokeConfig, { fetch: async () => {
      requests++;
      return Response.json(requests === 1 ? healthy : badge);
    } }));
    assert.equal(requests, 2);
  }
});

test('smoke retries only transient status and network failures, then verifies all checks', async () => {
  for (const transient of [408, 429, 500, 502, 503, 504, 'network']) {
    let requests = 0;
    const pauses = [];
    const result = await smoke.runSmoke(smokeConfig, {
      fetch: async url => {
        requests++;
        if (requests === 1) {
          if (transient === 'network') throw new TypeError('fixture network error');
          return new Response(null, { status: transient });
        }
        return goodResponse(url);
      },
      wait: async milliseconds => { pauses.push(milliseconds); },
    });
    assert.equal(result.checks, 6);
    assert.equal(requests, 7);
    assert.deepEqual(pauses, [1000]);
  }
});

test('smoke stops after five failed attempts and does not retry arbitrary programming errors', async () => {
  let requests = 0;
  await assert.rejects(smoke.runSmoke(smokeConfig, {
    fetch: async () => { requests++; return new Response(null, { status: 503 }); },
    wait: async () => {},
  }), /exhausted/);
  assert.equal(requests, 5);
  requests = 0;
  await assert.rejects(smoke.runSmoke(smokeConfig, { fetch: async () => { requests++; throw new Error('fixture defect'); } }));
  assert.equal(requests, 1);
});

function smokeClock() {
  let time = 0, id = 0;
  const pending = new Map();
  return {
    now: () => time,
    setTimeout(callback, delay) { const key = ++id; pending.set(key, { at: time + delay, callback }); return key; },
    clearTimeout(key) { pending.delete(key); },
    async settle(operation) {
      let settled = false, result, failure;
      operation.then(value => { settled = true; result = value; }, error => { settled = true; failure = error; });
      for (let step = 0; step < 200 && !settled; step++) {
        await new Promise(resolve => setImmediate(resolve));
        if (settled) break;
        assert.ok(pending.size > 0, 'Smoke operation remained pending without a bounded timer.');
        const [key, entry] = [...pending].sort((a, b) => a[1].at - b[1].at)[0];
        pending.delete(key);
        time = entry.at;
        entry.callback();
      }
      assert.ok(settled, 'Smoke operation did not settle under the virtual time budget.');
      assert.equal(pending.size, 0, 'Smoke timers must be cleaned up.');
      if (failure) throw failure;
      return result;
    },
  };
}

test('smoke aborts each hung GET at ten seconds and exhausts at five attempts', async () => {
  const clock = smokeClock();
  const starts = [], aborted = [];
  await assert.rejects(clock.settle(smoke.runSmoke(smokeConfig, { ...clock, fetch: (url, { signal }) => {
    starts.push(clock.now());
    signal.addEventListener('abort', () => aborted.push(clock.now()), { once: true });
    return new Promise(() => {});
  } })), /exhausted/);
  assert.deepEqual(starts, [0, 11000, 22000, 33000, 44000]);
  assert.deepEqual(aborted, [10000, 21000, 32000, 43000, 54000]);
});

test('smoke enforces one 180-second deadline across all checks and attempts', async () => {
  const clock = smokeClock();
  let requests = 0;
  const aborted = [];
  await assert.rejects(clock.settle(smoke.runSmoke(smokeConfig, { ...clock, fetch: (url, { signal }) => {
    requests++;
    if (requests % 5 === 0) return Promise.resolve(goodResponse(url));
    signal.addEventListener('abort', () => aborted.push(clock.now()), { once: true });
    return new Promise(() => {});
  } })), /deadline/);
  assert.equal(clock.now(), 180000);
  assert.equal(requests, 21);
  assert.equal(aborted.at(-1), 180000);
});

test('smoke bounds body consumption by the same request timeout', async () => {
  const clock = smokeClock();
  let cancelledBodies = 0, requests = 0;
  await assert.rejects(clock.settle(smoke.runSmoke(smokeConfig, { ...clock, fetch: async () => {
    requests++;
    return new Response(new ReadableStream({ cancel() { cancelledBodies++; } }), { headers: { 'content-type': 'application/json' } });
  } })), /exhausted/);
  assert.equal(clock.now(), 54000);
  assert.equal(requests, 5);
  assert.equal(cancelledBodies, 5);
});

test('smoke cancellation aborts a hung request immediately and prevents later GETs', async () => {
  const clock = smokeClock();
  const controller = new AbortController();
  let requests = 0, aborted = false;
  const operation = smoke.runSmoke(smokeConfig, { ...clock, signal: controller.signal, fetch: (url, { signal }) => {
    requests++;
    signal.addEventListener('abort', () => { aborted = true; }, { once: true });
    return new Promise(() => {});
  } });
  controller.abort(new Error('secret cancellation reason'));
  await assert.rejects(clock.settle(operation), /^Error: Smoke cancelled\.$/);
  assert.equal(requests, 1);
  assert.equal(aborted, true);
  assert.equal(clock.now(), 0);
  await assert.rejects(smoke.runSmoke(smokeConfig, { signal: controller.signal, fetch: async () => { requests++; } }), /cancelled/);
  assert.equal(requests, 1);
});

test('smoke bounds a stalled retry wait by its shared deadline', async () => {
  const clock = smokeClock();
  let requests = 0;
  await assert.rejects(clock.settle(smoke.runSmoke(smokeConfig, { ...clock,
    fetch: async () => { requests++; return new Response(null, { status: 503 }); },
    wait: () => new Promise(() => {}),
  })), /deadline/);
  assert.equal(clock.now(), 180000);
  assert.equal(requests, 1);
});

test('smoke CLI validates required inputs before reading deployment output and prints safe errors', () => {
  const script = resolve('.github/scripts/delivery/smoke.mjs');
  const env = { SMOKE_TEST_BADGE_PATH: smokePaths.testBadgePath, SMOKE_NUGET_BADGE_PATH: smokePaths.nugetBadgePath };
  const valid = spawnSync(process.execPath, [script, 'validate'], { env, encoding: 'utf8', timeout: 5000 });
  assert.equal(valid.status, 0);
  assert.match(valid.stdout, /validated/);
  for (const command of ['validate', 'run', 'unknown']) {
    const invalid = spawnSync(process.execPath, [script, command], {
      env: { SMOKE_TEST_BADGE_PATH: '/badges/tests/linux/a/b/secret?token=credential', SMOKE_NUGET_BADGE_PATH: '' },
      encoding: 'utf8', timeout: 5000,
    });
    assert.equal(invalid.status, 1);
    assert.doesNotMatch(invalid.stderr, /credential|token|secret|Error:/);
  }
});

test('smoke CLI uses only explicit CDK stack outputs and executes six local fetch fixtures', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'delivery-smoke-'));
  try {
    await mkdir(join(directory, 'artifacts'));
    await writeFile(join(directory, 'artifacts/cdk-outputs.json'), JSON.stringify({ BadgeSmithStack: {
      BadgeSmithApiUrl: smokeConfig.apiUrl, CustomDomainUrl: smokeConfig.customDomainUrl,
    } }));
    await writeFile(join(directory, 'fetch-fixture.mjs'), `
      import { appendFileSync } from 'node:fs';
      globalThis.fetch = async (url, options) => {
        if (!/^https:\\/\\/(gateway|public)\\.invalid\\//.test(url) || options.method !== 'GET') throw new Error('Fixture target violation');
        appendFileSync('requests.jsonl', JSON.stringify({ url, method: options.method }) + '\\n');
        return Response.json(url.endsWith('/health') ? ${JSON.stringify(healthy)} : url.includes('/badges/tests/') ? ${JSON.stringify(testBadge)} : ${JSON.stringify(nugetBadge)});
      };
    `);
    const command = spawnSync(process.execPath, ['--import', join(directory, 'fetch-fixture.mjs'), resolve('.github/scripts/delivery/smoke.mjs'), 'run'], {
      cwd: directory, env: { SMOKE_TEST_BADGE_PATH: smokePaths.testBadgePath, SMOKE_NUGET_BADGE_PATH: smokePaths.nugetBadgePath },
      encoding: 'utf8', timeout: 5000,
    });
    assert.equal(command.status, 0, command.stderr);
    assert.equal((await readFile(join(directory, 'requests.jsonl'), 'utf8')).trim().split('\n').length, 6);
    assert.match(command.stdout, /6/);
  } finally { await rm(directory, { recursive: true, force: true }); }
});

test('smoke rejects invalid UTF-8 JSON instead of replacing corrupt response bytes', async () => {
  let requests = 0;
  const body = Buffer.concat([Buffer.from('{"schemaVersion":1,"label":"tests","message":"'), Buffer.from([0xff]), Buffer.from('"}')]);
  await assert.rejects(smoke.runSmoke(smokeConfig, { fetch: async () => {
    requests++;
    return requests === 1 ? Response.json(healthy) : new Response(body, { headers: { 'content-type': 'application/json' } });
  } }));
  assert.equal(requests, 2);
});

test('smoke rejects oversized bodies without retry or completing the remaining checks', async () => {
  let requests = 0;
  await assert.rejects(smoke.runSmoke(smokeConfig, { fetch: async () => {
    requests++;
    return new Response(JSON.stringify({ ...healthy, extra: 'x'.repeat(65536) }), { headers: { 'content-type': 'application/json' } });
  } }));
  assert.equal(requests, 1);
});

test('smoke configuration requires explicit safe badge paths and preserves encoded branch slash', () => {
  assert.equal(typeof smoke.validateSmokeConfig, 'function');
  assert.deepEqual(smoke.validateSmokeConfig(smokePaths), smokePaths);
  assert.throws(() => smoke.validateSmokeConfig({}));
});

test('smoke configuration rejects origin changes, traversal, injection and ambiguous route shapes', () => {
  for (const testBadgePath of [
    'https://other.invalid/badges/tests/linux/a/b/c', '//other.invalid/badges/tests/linux/a/b/c',
    '/badges/tests/linux/a/b/../secret', '/badges/tests/linux/a/b/%2e%2e',
    '/badges/tests/linux/a/b/%252e%252e', '/badges/tests/linux/a/b/x%2F..%2Fy',
    '/badges/tests/linux/a/b/x?token=secret', '/badges/tests/linux/a/b/x#fragment',
    '/badges/tests/linux/a/b/x%0a', '/badges/tests/linux/a/b/$(id)',
    '/badges/tests/linux/a/b/x\\y', '/badges/tests/linux/a/b/x/y',
    '/badges/tests/linux/a/b/%', '/badges/tests/linux/a/b/',
  ]) assert.throws(() => smoke.validateSmokeConfig({ ...smokePaths, testBadgePath }));
  for (const nugetBadgePath of ['/badges/packages/nuget/../x', '/badges/packages/nuget/x?q=x',
    '/badges/packages/nuget/a%2Fb', '/badges/packages/nuget/%2e', '/badges/packages/nuget/x\n']) {
    assert.throws(() => smoke.validateSmokeConfig({ ...smokePaths, nugetBadgePath }));
  }
});
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const sha = 'a'.repeat(40);
const selection = { repository: 'fixture/service', runId: '123', artifactId: '456', sha, attempt: '2' };
const run = {
  id: 123, repository: { full_name: 'fixture/service', id: 10 },
  head_repository: { full_name: 'fixture/service', id: 10 },
  path: '.github/workflows/ci-cd.yml', event: 'push', head_branch: 'master',
  status: 'completed', conclusion: 'success', head_sha: sha, run_attempt: 2,
  created_at: '2026-10-15T10:20:30Z',
};
const metadata = {
  id: 456, name: 'lambda-zip-123-2', expired: false, digest: `sha256:${'b'.repeat(64)}`,
  workflow_run: { id: 123, repository_id: 10, head_repository_id: 10, head_branch: 'master', head_sha: sha },
};

const masterSha = 'c'.repeat(40);
const masterRef = { ref: 'refs/heads/master', object: { type: 'commit', sha: masterSha } };
const ancestorComparison = {
  status: 'ahead', ahead_by: 3, behind_by: 0, total_commits: 3,
  base_commit: { sha }, merge_base_commit: { sha },
};
const identicalComparison = {
  status: 'identical', ahead_by: 0, behind_by: 0, total_commits: 0,
  base_commit: { sha }, merge_base_commit: { sha },
};
const masterRefUrl = 'https://api.github.com/repos/fixture/service/git/ref/heads/master';
const compareUrl = `https://api.github.com/repos/fixture/service/compare/${sha}...${masterSha}`;

// Tiny ZIP fixture builder, independent of the promotion reader. Local headers and
// central records deliberately allow adversarial names and duplicate entries.
function zipFixture(entries) {
  const local = [], central = [];
  let offset = 0;
  for (const [name, data] of entries) {
    const fileName = Buffer.from(name);
    const contents = Buffer.from(data);
    const compressed = deflateRawSync(contents);
    const header = Buffer.alloc(30);
    header.writeUInt32LE(0x04034b50);
    header.writeUInt16LE(8, 8);
    header.writeUInt32LE(compressed.length, 18);
    header.writeUInt32LE(contents.length, 22);
    header.writeUInt16LE(fileName.length, 26);
    const record = Buffer.alloc(46);
    record.writeUInt32LE(0x02014b50);
    record.writeUInt16LE(8, 10);
    record.writeUInt32LE(compressed.length, 20);
    record.writeUInt32LE(contents.length, 24);
    record.writeUInt16LE(fileName.length, 28);
    record.writeUInt32LE(offset, 42);
    local.push(header, fileName, compressed);
    central.push(record, fileName);
    offset += header.length + fileName.length + compressed.length;
  }
  const directory = Buffer.concat(central);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...local, directory, end]);
}

const lambdaZip = Buffer.from('fixture Lambda ZIP bytes');
const manifest = { schemaVersion: 1, ...selection, zipSha256: sha256(lambdaZip), zipBytes: lambdaZip.length };
function archiveFixture(record = manifest, entries) {
  const archive = zipFixture(entries ?? [
    ['badge-lambda-linux-arm64.zip', lambdaZip], ['provenance.json', JSON.stringify(record)],
  ]);
  return { archive, evidence: { ...selection, archiveSha256: sha256(archive) } };
}

test('promotion accepts one identified artifact from a successful same-repository master CI attempt', () => {
  const result = artifact.validateProvenance(selection, run, { total_count: 1, artifacts: [metadata] });
  assert.equal(result.sha, sha);
  assert.equal(result.archiveSha256, 'b'.repeat(64));
});

test('promotion rejects unsafe selection and missing, ambiguous or mismatched CI evidence', () => {
  for (const patch of [
    { repository: 'other/service' }, { head_repository: { id: 99, full_name: 'other/service' } },
    { path: '.github/workflows/other.yml' }, { event: 'pull_request' }, { head_branch: 'feature' },
    { status: 'in_progress' }, { conclusion: 'failure' }, { head_sha: 'c'.repeat(40) },
    { run_attempt: 1 }, { id: 124 },
  ]) {
    const candidate = { ...run, ...patch };
    if (typeof patch.repository === 'string') candidate.repository = { id: 10, full_name: patch.repository };
    assert.throws(() => artifact.validateProvenance(selection, candidate, { total_count: 1, artifacts: [metadata] }));
  }
  for (const patch of [
    { id: 457 }, { name: '../lambda.zip' }, { expired: true }, { digest: null },
    { digest: `sha256:${'z'.repeat(64)}` }, { workflow_run: { ...metadata.workflow_run, id: 124 } },
    { workflow_run: { ...metadata.workflow_run, head_sha: 'c'.repeat(40) } },
    { workflow_run: { ...metadata.workflow_run, head_repository_id: 99 } },
  ]) assert.throws(() => artifact.validateProvenance(selection, run, { total_count: 1, artifacts: [{ ...metadata, ...patch }] }));
  for (const listing of [{ total_count: 0, artifacts: [] }, { total_count: 2, artifacts: [metadata, metadata] }]) {
    assert.throws(() => artifact.validateProvenance(selection, run, listing));
  }
  for (const patch of [{ runId: '1\nsha=evil' }, { artifactId: '../456' }, { attempt: '0' },
    { repository: 'fixture/../../evil' }, { sha: '$(id)' }, { runId: '9007199254740993' }]) {
    assert.throws(() => artifact.validateProvenance({ ...selection, ...patch }, run, { total_count: 1, artifacts: [metadata] }));
  }
});

test('selection rejects repository dot segments before any API request', () => {
  for (const repository of ['fixture/.', 'fixture/..']) {
    assert.throws(() => artifact.validateSelection({ ...selection, repository }));
  }
});

test('verified archive keeps archive SHA256 distinct from inner ZIP digest and exact bytes', () => {
  const { archive, evidence } = archiveFixture();
  const verified = artifact.verifyArchive(archive, evidence);
  assert.deepEqual(verified.zip, lambdaZip);
  assert.equal(verified.provenance.archiveSha256, sha256(archive));
  assert.equal(verified.provenance.zipSha256, sha256(lambdaZip));
  assert.equal(verified.provenance.zipBytes, lambdaZip.length);
  assert.notEqual(verified.provenance.archiveSha256, verified.provenance.zipSha256);
});

test('archive verification rejects corruption, unsafe extraction and mismatched inner provenance', () => {
  const { archive, evidence } = archiveFixture();
  assert.throws(() => artifact.verifyArchive(Buffer.from('not a zip'), evidence));
  assert.throws(() => artifact.verifyArchive(archive, { ...evidence, archiveSha256: '0'.repeat(64) }));
  for (const patch of [{ zipSha256: '0'.repeat(64) }, { zipBytes: lambdaZip.length + 1 },
    { repository: 'other/repo' }, { sha: 'c'.repeat(40) }, { runId: '124' }, { attempt: '1' }, { schemaVersion: 9 }]) {
    const fixture = archiveFixture({ ...manifest, ...patch });
    assert.throws(() => artifact.verifyArchive(fixture.archive, fixture.evidence));
  }
  for (const entries of [
    [['../badge-lambda-linux-arm64.zip', lambdaZip], ['provenance.json', JSON.stringify(manifest)]],
    [['/tmp/evil', lambdaZip], ['provenance.json', JSON.stringify(manifest)]],
    [['badge-lambda-linux-arm64.zip', lambdaZip], ['badge-lambda-linux-arm64.zip', lambdaZip]],
    [['badge-lambda-linux-arm64.zip', lambdaZip], ['provenance.json', '{']],
    [['badge-lambda-linux-arm64.zip', lambdaZip], ['provenance.json', 'x'.repeat(17000)]],
    [['badge-lambda-linux-arm64.zip', lambdaZip]],
  ]) {
    const fixture = archiveFixture(manifest, entries);
    assert.throws(() => artifact.verifyArchive(fixture.archive, fixture.evidence));
  }
  const symlink = Buffer.from(archive);
  const central = symlink.indexOf(Buffer.from([0x50, 0x4b, 0x01, 0x02]));
  symlink.writeUInt32LE(0xa1ff0000, central + 38);
  assert.throws(() => artifact.verifyArchive(symlink, { ...evidence, archiveSha256: sha256(symlink) }));
});

test('promotion binds current master and older ancestors to the explicit branch and resolved SHA', async t => {
  const { archive } = archiveFixture();
  for (const [head, comparison, status, aheadBy] of [
    [sha, identicalComparison, 'identical', 0],
    [masterSha, ancestorComparison, 'ahead', 3],
  ]) {
    await t.test(status, async () => {
      const requests = [];
      const result = await artifact.downloadPromotion(selection, { token: 'fixture-token', fetchImpl: async url => {
        requests.push(url);
        if (url === masterRefUrl) return Response.json({ ...masterRef, object: { type: 'commit', sha: head }, url: 'https://secret.invalid/ref' });
        if (url === `https://api.github.com/repos/fixture/service/compare/${sha}...${head}`) {
          return Response.json({ ...comparison, html_url: 'https://secret.invalid/compare', secret: 'unsafe metadata' });
        }
        if (url.endsWith('/actions/runs/123')) return Response.json({ ...run, html_url: 'https://secret.invalid/run' });
        if (url.includes('/artifacts?')) return Response.json({ total_count: 1, artifacts: [{ ...metadata, digest: `sha256:${sha256(archive)}` }] });
        if (url.endsWith('/actions/artifacts/456/zip')) return new Response(null, { status: 302, headers: { location: 'https://fixture.blob.core.windows.net/archive' } });
        assert.equal(url, 'https://fixture.blob.core.windows.net/archive');
        return new Response(archive);
      } });
      assert.deepEqual(requests.filter(url => url.includes('/git/') || url.includes('/compare/')), [
        masterRefUrl, `https://api.github.com/repos/fixture/service/compare/${sha}...${head}`,
      ]);
      assert.ok(requests.indexOf(masterRefUrl) < requests.findIndex(url => url.endsWith('/zip')));
      assert.deepEqual(result.provenance, {
        ...selection, archiveSha256: sha256(archive), zipSha256: sha256(lambdaZip),
        zipBytes: lambdaZip.length, archiveBytes: archive.length,
        ciRunCreatedAtUtc: '2026-10-15T10:20:30.000Z',
        masterHistory: { sourceSha: sha, masterSha: head, direction: 'source-to-master', status, aheadBy, behindBy: 0, totalCommits: aheadBy },
      });
      assert.deepEqual(result.zip, lambdaZip);
    });
  }
});

test('promotion rejects absent, malformed or incoherent master history before archive download', async t => {
  const cases = [
    ['missing-ref', null, ancestorComparison],
    ['tag-ref', { ...masterRef, ref: 'refs/tags/master' }, ancestorComparison],
    ['ambiguous-ref', { ...masterRef, ref: 'master' }, ancestorComparison],
    ['missing-object', { ref: 'refs/heads/master' }, ancestorComparison],
    ['tag-object', { ...masterRef, object: { type: 'tag', sha: masterSha } }, ancestorComparison],
    ['short-master-sha', { ...masterRef, object: { type: 'commit', sha: 'abc123' } }, ancestorComparison],
    ['invalid-master-sha', { ...masterRef, object: { type: 'commit', sha: 'Z'.repeat(40) } }, ancestorComparison],
    ['injected-master-sha', { ...masterRef, object: { type: 'commit', sha: masterSha + '\ncredential' } }, ancestorComparison],
    ['missing-comparison', masterRef, null],
    ['missing-status', masterRef, { ...ancestorComparison, status: undefined }],
    ['unknown-status', masterRef, { ...ancestorComparison, status: 'unexpected' }],
    ['behind', masterRef, { ...ancestorComparison, status: 'behind', ahead_by: 0, behind_by: 3, total_commits: 0 }],
    ['ahead-but-behind', masterRef, { ...ancestorComparison, behind_by: 1 }],
    ['diverged', masterRef, { ...ancestorComparison, status: 'diverged', behind_by: 2 }],
    ['wrong-base', masterRef, { ...ancestorComparison, base_commit: { sha: masterSha } }],
    ['wrong-merge-base', masterRef, { ...ancestorComparison, merge_base_commit: { sha: masterSha } }],
    ['missing-base', masterRef, { ...ancestorComparison, base_commit: undefined }],
    ['missing-merge-base', masterRef, { ...ancestorComparison, merge_base_commit: undefined }],
    ['wrong-direction', masterRef, { ...ancestorComparison, ahead_by: 0, behind_by: 3 }],
    ['zero-ahead', masterRef, { ...ancestorComparison, ahead_by: 0, total_commits: 0 }],
    ['ahead-equal-head', { ...masterRef, object: { type: 'commit', sha } }, ancestorComparison],
    ['identical-different-head', masterRef, identicalComparison],
    ['identical-nonzero', { ...masterRef, object: { type: 'commit', sha } }, { ...identicalComparison, ahead_by: 1, total_commits: 1 }],
    ['inconsistent-total', masterRef, { ...ancestorComparison, total_commits: 2 }],
  ];
  for (const field of ['ahead_by', 'behind_by', 'total_commits']) {
    for (const value of [undefined, null, '3', -1, 0.5, Number.MAX_SAFE_INTEGER + 1]) {
      cases.push([`invalid-${field}-${value}`, masterRef, { ...ancestorComparison, [field]: value }]);
    }
  }
  for (const [name, ref, comparison] of cases) {
    await t.test(name, async () => {
      const requests = [];
      await assert.rejects(artifact.downloadPromotion(selection, { token: 'fixture', fetchImpl: async url => {
        requests.push(url);
        if (url.endsWith('/actions/runs/123')) return Response.json(run);
        if (url.includes('/artifacts?')) return Response.json({ total_count: 1, artifacts: [metadata] });
        if (url === masterRefUrl) return Response.json(ref);
        if (url === compareUrl || url === `https://api.github.com/repos/fixture/service/compare/${sha}...${sha}`) return Response.json(comparison);
        throw new Error('Archive download must not be reached');
      } }), error => error.category === 'source-not-on-master' && error.message === 'Delivery artifact validation failed.', name);
      assert.ok(requests.includes(masterRefUrl), name);
      assert.ok(requests.every(url => !url.endsWith('/zip') && !url.includes('.blob.')), name);
    });
  }
});

test('promotion rejects missing or noncanonical CI run timestamps before archive download', async t => {
  for (const created_at of [undefined, null, '', 'secret', '2026-02-30T10:20:30Z',
    '2026-10-15T10:20:30+02:00', '2026-10-15', '2026-10-15T10:20:30Z\ncredential']) {
    await t.test(String(created_at), async () => {
      const requests = [];
      await assert.rejects(artifact.downloadPromotion(selection, { token: 'fixture', fetchImpl: async url => {
        requests.push(url);
        if (url.endsWith('/actions/runs/123')) return Response.json({ ...run, created_at });
        if (url.includes('/artifacts?')) return Response.json({ total_count: 1, artifacts: [metadata] });
        if (url === masterRefUrl) return Response.json(masterRef);
        if (url === compareUrl) return Response.json(ancestorComparison);
        throw new Error('Archive download must not be reached');
      } }), error => error.category === 'ci-run-ineligible');
      assert.ok(requests.every(url => !url.endsWith('/zip') && !url.includes('.blob.')));
    });
  }
});

test('artifact transport uses fixed API URLs and never forwards GitHub credentials to storage', async () => {
  const { archive } = archiveFixture();
  const item = { ...metadata, digest: `sha256:${sha256(archive)}` };
  const calls = [];
  const fetchImpl = async (url, options) => {
    calls.push({ url, options });
    if (url === masterRefUrl) return Response.json(masterRef);
    if (url === compareUrl) return Response.json(ancestorComparison);
    if (url.endsWith('/actions/runs/123')) return Response.json(run);
    if (url.includes('/actions/runs/123/artifacts?')) return Response.json({ total_count: 1, artifacts: [item] });
    if (url.endsWith('/actions/artifacts/456/zip')) return new Response(null, { status: 302, headers: { location: 'https://fixture.blob.core.windows.net/artifact?sig=secret' } });
    assert.equal(url, 'https://fixture.blob.core.windows.net/artifact?sig=secret');
    assert.equal(options.headers?.authorization, undefined);
    return new Response(archive);
  };
  const result = await artifact.downloadPromotion(selection, { token: 'fixture-token', fetchImpl });
  assert.deepEqual(result.zip, lambdaZip);
  assert.equal(calls.length, 6);
  assert.ok(calls.every(call => call.options.redirect === 'manual'));
  assert.ok(calls.slice(0, 5).every(call => call.options.headers.authorization === 'Bearer fixture-token'));
});

test('promotion CLI writes fixed outputs and sanitized history with a canonical selected-run timestamp', async () => {
  const { archive } = archiveFixture();
  const directory = await mkdtemp(join(tmpdir(), 'delivery-promotion-'));
  try {
    const fixture = `globalThis.fetch = async url => {
      if (url.endsWith('/actions/runs/123')) return Response.json(${JSON.stringify({ ...run, created_at: '2026-10-15T10:20:30.123Z', html_url: 'https://secret.invalid/run', secret: 'unsafe metadata' })});
      if (url.includes('/artifacts?')) return Response.json(${JSON.stringify({ total_count: 1, artifacts: [{ ...metadata, digest: `sha256:${sha256(archive)}` }] })});
      if (url === '${masterRefUrl}') return Response.json(${JSON.stringify({ ...masterRef, url: 'https://secret.invalid/ref' })});
      if (url === '${compareUrl}') return Response.json(${JSON.stringify({ ...ancestorComparison, html_url: 'https://secret.invalid/compare' })});
      if (url.endsWith('/actions/artifacts/456/zip')) return new Response(null, {status:302,headers:{location:'https://fixture.blob.core.windows.net/archive?sig=credential'}});
      if (url === 'https://fixture.blob.core.windows.net/archive?sig=credential') return new Response(Buffer.from('${archive.toString('base64')}', 'base64'));
      throw new Error('Unexpected fixture request');
    };`;
    await writeFile(join(directory, 'fetch-fixture.mjs'), fixture);
    const command = spawnSync(process.execPath, ['--import', join(directory, 'fetch-fixture.mjs'), resolve('.github/scripts/delivery/artifact.mjs'), 'promote'], {
      cwd: directory, encoding: 'utf8', timeout: 5000, env: {
        GITHUB_REPOSITORY: selection.repository, CI_RUN_ID: selection.runId, CI_ARTIFACT_ID: selection.artifactId,
        CI_HEAD_SHA: selection.sha, CI_RUN_ATTEMPT: selection.attempt, GITHUB_TOKEN: 'fixture-token',
        GITHUB_OUTPUT: 'github-output', GITHUB_STEP_SUMMARY: 'summary.md',
      },
    });
    assert.equal(command.status, 0, command.stderr);
    assert.equal(await readFile(join(directory, 'github-output'), 'utf8'), `source_sha=${sha}\n`);
    assert.deepEqual(await readFile(join(directory, 'artifacts/badge-lambda-linux-arm64.zip')), lambdaZip);
    const json = await readFile(join(directory, 'artifacts/verified-provenance.json'), 'utf8');
    assert.deepEqual(JSON.parse(json), {
      ...selection, archiveSha256: sha256(archive), zipSha256: sha256(lambdaZip), zipBytes: lambdaZip.length,
      archiveBytes: archive.length, ciRunCreatedAtUtc: '2026-10-15T10:20:30.123Z',
      masterHistory: { sourceSha: sha, masterSha, direction: 'source-to-master', status: 'ahead', aheadBy: 3, behindBy: 0, totalCommits: 3 },
    });
    const summary = await readFile(join(directory, 'summary.md'), 'utf8');
    assert.equal(summary, `## Verified delivery artifact\n\n\`\`\`json\n${json.trimEnd()}\n\`\`\`\n`);
    assert.doesNotMatch(summary + command.stdout + command.stderr, /secret|unsafe metadata|sig=|credential|https?:\/\//);
  } finally { await rm(directory, { recursive: true, force: true }); }
});

test('CI record command emits source identity and inner ZIP evidence without a circular archive digest', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'delivery-record-'));
  try {
    await mkdir(join(directory, 'artifacts'));
    await writeFile(join(directory, 'artifacts/badge-lambda-linux-arm64.zip'), lambdaZip);
    const result = spawnSync(process.execPath, [resolve('.github/scripts/delivery/artifact.mjs'), 'record'], {
      cwd: directory, encoding: 'utf8', env: {
        ...process.env, GITHUB_REPOSITORY: selection.repository, GITHUB_SHA: sha,
        GITHUB_RUN_ID: selection.runId, GITHUB_RUN_ATTEMPT: selection.attempt,
      },
    });
    assert.equal(result.status, 0, result.stderr);
    const record = JSON.parse(await readFile(join(directory, 'artifacts/provenance.json'), 'utf8'));
    assert.deepEqual(record, {
      schemaVersion: 1, repository: selection.repository, sha, runId: '123', attempt: '2',
      zipSha256: sha256(lambdaZip), zipBytes: lambdaZip.length,
    });
    assert.equal(record.archiveSha256, undefined);
  } finally { await rm(directory, { recursive: true, force: true }); }
});

test('artifact transport fails closed on metadata errors, storage redirects and corrupted download', async () => {
  for (const scenario of ['metadata-error', 'missing', 'unsafe-redirect', 'credentials-redirect', 'storage-redirect', 'corrupt']) {
    let storageCalls = 0;
    const fetchImpl = async (url) => {
      if (url === masterRefUrl) return Response.json(masterRef);
      if (url === compareUrl) return Response.json(ancestorComparison);
      if (url.endsWith('/actions/runs/123')) return scenario === 'metadata-error' ? new Response(null, { status: 403 }) : Response.json(run);
      if (url.includes('/artifacts?')) return Response.json({ total_count: scenario === 'missing' ? 0 : 1, artifacts: scenario === 'missing' ? [] : [metadata] });
      if (url.endsWith('/zip')) return new Response(null, { status: 302, headers: { location:
        scenario === 'unsafe-redirect' ? 'http://127.0.0.1/evil' : scenario === 'credentials-redirect' ?
          'https://secret@fixture.blob.core.windows.net/evil' : 'https://fixture.blob.core.windows.net/zip' } });
      storageCalls++;
      return scenario === 'storage-redirect' ? new Response(null, { status: 302 }) : new Response('corrupt');
    };
    await assert.rejects(artifact.downloadPromotion(selection, { token: 'fixture', fetchImpl }));
    if (['metadata-error', 'missing', 'unsafe-redirect', 'credentials-redirect'].includes(scenario)) assert.equal(storageCalls, 0);
  }
});

async function deployWorkflow() { return readFile('.github/workflows/deploy.yml', 'utf8'); }
function stepRun(workflow, name) {
  const marker = `      - name: ${name}\n`;
  const block = workflow.split(marker)[1]?.split('\n      - ')[0];
  assert.ok(block, `Missing ${name} step`);
  const match = block.match(/        run: (.+)/);
  assert.ok(match && match[1] !== '|', `Expected single command in ${name}`);
  assert.doesNotMatch(block, /continue-on-error:|\n        if:/, `${name} must be an unconditional gate`);
  return match[1];
}

test('workflow shell stops before deploy on stack-access or change-set errors but permits a successful diff', async () => {
  const workflow = await deployWorkflow();
  const diff = stepRun(workflow, 'CDK diff');
  const deploy = stepRun(workflow, 'CDK deploy');
  for (const [scenario, code] of [['differences', 0], ['missing-stack', 0], ['access-denied', 42], ['change-set-error', 43]]) {
    const script = `cdk() {\ncase "$1" in\ndiff) test "$*" = "diff BadgeSmithStack --no-fail --method change-set" || return 99; return "$DIFF_CODE";;\ndeploy) echo DEPLOY_CALLED;;\n*) return 98;;\nesac\n}\n${diff}\n${deploy}\n`;
    const result = spawnSync('bash', ['-e', '-c', script], { encoding: 'utf8', env: { ...process.env, DIFF_CODE: String(code) } });
    assert.equal(result.status, code, scenario);
    assert.equal(result.stdout.includes('DEPLOY_CALLED'), code === 0, scenario);
  }
});

test('workflow validates inputs and provenance before AWS setup and checks out the verified CDK SHA', async () => {
  const workflow = await deployWorkflow();
  assert.doesNotMatch(workflow, /build_lambda|source_branch|artifact_name|show_diff|\|\| true|dawidd6/);
  assert.ok(workflow.indexOf('smoke.mjs validate') < workflow.indexOf('Configure AWS credentials'));
  assert.ok(workflow.indexOf('artifact.mjs promote') < workflow.indexOf('Configure AWS credentials'));
  assert.match(workflow, /ref: \$\{\{ steps\.artifact\.outputs\.source_sha \}\}/);
  assert.match(workflow, /cdk deploy BadgeSmithStack --require-approval never --outputs-file/);
  assert.match(workflow, /smoke.mjs run/);
  for (const runBlock of workflow.matchAll(/        run: (.+(?:\n          .*)*)/g)) {
    assert.doesNotMatch(runBlock[1], /\$\{\{/, 'Actions values must enter shell through environment or action arguments');
  }
});

test('CI records inner ZIP provenance before uploading an immutable run-and-attempt artifact', async () => {
  const workflow = await readFile('.github/workflows/ci-cd.yml', 'utf8');
  assert.match(workflow, /node --test \.github\/scripts\/delivery\/delivery.test.mjs/);
  assert.ok(workflow.includes("format('lambda-zip-pr-{0}', github.event.pull_request.number)"),
    'Preserve the existing numeric, filesystem-safe PR prefix contract');
  assert.ok(workflow.includes("format('lambda-zip-{0}-{1}', github.run_id, github.run_attempt)"));
  assert.ok(workflow.includes("format('{0}-{1}-{2}', format('lambda-zip-pr-{0}', github.event.pull_request.number), github.run_id, github.run_attempt)"));
  assert.match(workflow, /artifacts\/provenance.json/);
  assert.doesNotMatch(workflow, /overwrite: true|lambda-zip-latest/);
  assert.ok(workflow.indexOf('artifact.mjs record') < workflow.indexOf('name: Upload Lambda artifact'));
});

test('CI provenance summary renders the recorded JSON in a code fence', async () => {
  const workflow = await readFile('.github/workflows/ci-cd.yml', 'utf8');
  const summary = workflow.split('      - name: Artifact selection summary\n')[1];
  assert.ok(summary);
  assert.match(summary, /echo '```json' >> "\$GITHUB_STEP_SUMMARY"\n          cat artifacts\/provenance.json >> "\$GITHUB_STEP_SUMMARY"\n          echo '```' >> "\$GITHUB_STEP_SUMMARY"/);
});

const diagnosticSentinel = 'SENTINEL_CREDENTIAL_9c743';
async function cliDiagnostic(script, command, fixture, env = {}, outputs) {
  const directory = await mkdtemp(join(tmpdir(), 'delivery-diagnostic-'));
  try {
    await mkdir(join(directory, 'artifacts'));
    if (outputs) await writeFile(join(directory, 'artifacts/cdk-outputs.json'), JSON.stringify(outputs));
    await writeFile(join(directory, 'fetch-fixture.mjs'), fixture);
    const result = spawnSync(process.execPath, ['--import', join(directory, 'fetch-fixture.mjs'), resolve(script), command], {
      cwd: directory, env, encoding: 'utf8', timeout: 5000,
    });
    assert.equal(result.status, 1, result.stderr);
    assert.doesNotMatch(result.stdout + result.stderr, /SENTINEL_CREDENTIAL_9c743|sig=|https?:\/\/|Error:|\bat file:|raw-body/);
    if (script.endsWith('/artifact.mjs')) {
      for (const name of ['badge-lambda-linux-arm64.zip', 'verified-provenance.json']) {
        await assert.rejects(readFile(join(directory, 'artifacts', name)), { code: 'ENOENT' });
      }
    }
    return result.stderr;
  } finally { await rm(directory, { recursive: true, force: true }); }
}

test('artifact CLI distinguishes safe provenance, expiry and digest gates while suppressing all external error data', async () => {
  const { archive } = archiveFixture();
  const validMetadata = { ...metadata, digest: `sha256:${sha256(archive)}` };
  const environment = {
    GITHUB_REPOSITORY: selection.repository, CI_RUN_ID: selection.runId, CI_ARTIFACT_ID: selection.artifactId,
    CI_HEAD_SHA: selection.sha, CI_RUN_ATTEMPT: selection.attempt, GITHUB_TOKEN: diagnosticSentinel,
  };
  for (const [scenario, category] of [
    ['identity', 'ci-identity-mismatch'], ['expired', 'artifact-expired'], ['missing', 'artifact-selection'],
    ['archive-digest', 'archive-digest-mismatch'], ['inner-digest', 'zip-digest-mismatch'], ['unknown', 'unknown'],
  ]) {
    const inner = scenario === 'inner-digest' ? archiveFixture({ ...manifest, zipSha256: '0'.repeat(64) }).archive : archive;
    const record = { ...validMetadata,
      digest: scenario === 'archive-digest' ? `sha256:${'0'.repeat(64)}` : `sha256:${sha256(inner)}`,
      expired: scenario === 'expired',
    };
    const fixture = `globalThis.fetch = async url => {
      if (${JSON.stringify(scenario)} === 'unknown') {
        const error = new Error('https://fixture.blob.core.windows.net/zip?sig=${diagnosticSentinel}');
        error.cause = new Error('raw-body ${diagnosticSentinel}');
        error.stack = 'forged-stack ${diagnosticSentinel}'; error.secret = '${diagnosticSentinel}';
        error.code = 'artifact-expired'; error.category = 'artifact-expired';
        error.context = {origin:'public',check:'nuget',status:418,attempt:5}; throw error;
      }
      if (url === '${masterRefUrl}') return Response.json(${JSON.stringify(masterRef)});
      if (url === '${compareUrl}') return Response.json(${JSON.stringify(ancestorComparison)});
      if (url.endsWith('/actions/runs/123')) return Response.json(${JSON.stringify(scenario === 'identity' ? { ...run, run_attempt: 99 } : run)});
      if (url.includes('/artifacts?')) return Response.json(${JSON.stringify({ total_count: scenario === 'missing' ? 0 : 1, artifacts: scenario === 'missing' ? [] : [record] })});
      if (url.endsWith('/zip')) return new Response('raw-body ${diagnosticSentinel}', {status:302,headers:{location:'https://fixture.blob.core.windows.net/zip?sig=${diagnosticSentinel}'}});
      return new Response(Buffer.from('${inner.toString('base64')}', 'base64'));
    };`;
    const diagnostic = await cliDiagnostic('.github/scripts/delivery/artifact.mjs', 'promote', fixture, environment);
    assert.match(diagnostic, new RegExp(`category=${category}(?:[ ,\\]])`), scenario);
    if (scenario === 'unknown') assert.equal(diagnostic, 'Delivery artifact validation failed [category=unknown]; no artifact was approved.\n');
  }
});

test('artifact CLI fails closed with a finite category for master lookup and comparison errors', async t => {
  const environment = {
    GITHUB_REPOSITORY: selection.repository, CI_RUN_ID: selection.runId, CI_ARTIFACT_ID: selection.artifactId,
    CI_HEAD_SHA: selection.sha, CI_RUN_ATTEMPT: selection.attempt, GITHUB_TOKEN: diagnosticSentinel,
  };
  for (const endpoint of [masterRefUrl, compareUrl]) {
    for (const failure of ['403', '404', '500', 'redirect', 'json', 'network', 'body']) {
      await t.test(`${endpoint.includes('/compare/') ? 'compare' : 'ref'}-${failure}`, async () => {
        const fixture = `globalThis.fetch = async url => {
          if (url === '${endpoint}') {
            if ('${failure}' === 'network') {
              const error = new Error('https://secret.invalid/?sig=${diagnosticSentinel}');
              error.category = 'artifact-expired'; error.stack = '${diagnosticSentinel}'; throw error;
            }
            if ('${failure}' === 'body') return new Response(new ReadableStream({start(controller) {controller.error(new Error('raw-body ${diagnosticSentinel}'));}}));
            if ('${failure}' === 'json') return new Response('raw-body ${diagnosticSentinel}');
            return new Response('raw-body ${diagnosticSentinel}', {status:'${failure}' === 'redirect' ? 302 : Number('${failure}'), headers:{location:'https://secret.invalid/?sig=${diagnosticSentinel}'}});
          }
          if (url.endsWith('/actions/runs/123')) return Response.json(${JSON.stringify(run)});
          if (url.includes('/artifacts?')) return Response.json(${JSON.stringify({ total_count: 1, artifacts: [metadata] })});
          if (url === '${masterRefUrl}') return Response.json(${JSON.stringify(masterRef)});
          if (url === '${compareUrl}') return Response.json(${JSON.stringify(ancestorComparison)});
          throw new Error('Unexpected download ${diagnosticSentinel}');
        };`;
        const diagnostic = await cliDiagnostic('.github/scripts/delivery/artifact.mjs', 'promote', fixture, environment);
        assert.equal(diagnostic, 'Delivery artifact validation failed [category=source-not-on-master]; no artifact was approved.\n');
      });
    }
  }
});

test('smoke CLI identifies the failing public badge check, status and attempt without printing bodies or inputs', async () => {
  const outputs = { BadgeSmithStack: { BadgeSmithApiUrl: smokeConfig.apiUrl, CustomDomainUrl: smokeConfig.customDomainUrl } };
  const environment = { SMOKE_TEST_BADGE_PATH: smokePaths.testBadgePath, SMOKE_NUGET_BADGE_PATH: smokePaths.nugetBadgePath };
  for (const [scenario, category, status] of [
    ['status', 'http-status', 404], ['json', 'invalid-json', 200], ['contract', 'badge-contract', 200],
    ['unknown', 'unknown', null],
  ]) {
    const fixture = `globalThis.fetch = async url => {
      if (url.startsWith('https://public.invalid/') && url.includes('/badges/tests/')) {
        if ('${scenario}' === 'unknown') {
          const error = new Error('https://secret.invalid/path?sig=${diagnosticSentinel}', {cause:new Error('raw-body ${diagnosticSentinel}')});
          error.stack='forged-stack ${diagnosticSentinel}';error.secret='${diagnosticSentinel}';
          error.category='badge-contract';error.context={origin:'gateway',check:'nuget',status:418,attempt:5,reason:'badge-contract'};
          error.origin='${diagnosticSentinel}';error.check='${diagnosticSentinel}';error.status='${diagnosticSentinel}';throw error;
        }
        if ('${scenario}' === 'contract') return Response.json({schemaVersion:9,label:'${diagnosticSentinel}',message:'raw-body ${diagnosticSentinel}'});
        return new Response('raw-body ${diagnosticSentinel}', {status:${status ?? 200},headers:{'content-type':'application/json','x-secret':'${diagnosticSentinel}'}});
      }
      return Response.json(url.endsWith('/health') ? ${JSON.stringify(healthy)} : url.includes('/badges/tests/') ? ${JSON.stringify(testBadge)} : ${JSON.stringify(nugetBadge)});
    };`;
    const diagnostic = await cliDiagnostic('.github/scripts/delivery/smoke.mjs', 'run', fixture, environment, outputs);
    assert.match(diagnostic, new RegExp(`category=${category}(?:[ ,\\]])`));
    assert.match(diagnostic, /origin=public/);
    assert.match(diagnostic, /check=tests/);
    assert.match(diagnostic, /attempt=1/);
    if (status) assert.match(diagnostic, new RegExp(`status=${status}`));
    if (scenario === 'unknown') assert.doesNotMatch(diagnostic, /category=badge-contract|status=418|attempt=5|reason=/);
  }
});

test('smoke CLI rejects a forged known category and context at its outer error boundary', async () => {
  const fixture = `JSON.parse = () => {
    const error = new Error('https://secret.invalid/?sig=${diagnosticSentinel}', {cause:new Error('raw-body ${diagnosticSentinel}')});
    error.stack='forged-stack ${diagnosticSentinel}';error.secret='${diagnosticSentinel}';
    error.category='badge-contract';
    error.context={origin:'public',check:'nuget',status:418,attempt:5,reason:'health-contract'};
    throw error;
  };`;
  const diagnostic = await cliDiagnostic('.github/scripts/delivery/smoke.mjs', 'run', fixture, {
    SMOKE_TEST_BADGE_PATH: smokePaths.testBadgePath, SMOKE_NUGET_BADGE_PATH: smokePaths.nugetBadgePath,
  }, { BadgeSmithStack: { BadgeSmithApiUrl: smokeConfig.apiUrl, CustomDomainUrl: smokeConfig.customDomainUrl } });
  assert.equal(diagnostic, 'Delivery smoke failed [category=unknown].\n');
});

test('smoke CLI reports bounded retry context and keeps fetch failures generic and redacted', async () => {
  const outputs = { BadgeSmithStack: { BadgeSmithApiUrl: smokeConfig.apiUrl, CustomDomainUrl: smokeConfig.customDomainUrl } };
  const environment = { SMOKE_TEST_BADGE_PATH: smokePaths.testBadgePath, SMOKE_NUGET_BADGE_PATH: smokePaths.nugetBadgePath };
  for (const scenario of ['status', 'fetch']) {
    const fixture = `
      const realSetTimeout = globalThis.setTimeout;
      globalThis.setTimeout = (fn, ms, ...args) => realSetTimeout(fn, ms === 1000 ? 0 : ms, ...args);
      globalThis.fetch = async () => {
        if ('${scenario}' === 'fetch') throw new TypeError('https://secret.invalid/?sig=${diagnosticSentinel}', {cause:new Error('raw-body ${diagnosticSentinel}')});
        return new Response('raw-body ${diagnosticSentinel}', {status:503});
      };
    `;
    const diagnostic = await cliDiagnostic('.github/scripts/delivery/smoke.mjs', 'run', fixture, environment, outputs);
    assert.match(diagnostic, /category=attempts-exhausted/);
    assert.match(diagnostic, /origin=gateway/);
    assert.match(diagnostic, /check=health/);
    assert.match(diagnostic, /attempt=5/);
    assert.match(diagnostic, new RegExp(`reason=${scenario === 'status' ? 'transient-status' : 'network-failure'}`));
    if (scenario === 'status') assert.match(diagnostic, /status=503/);
  }
});
