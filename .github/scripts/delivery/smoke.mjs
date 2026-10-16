import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

const transientStatuses = new Set([408, 429, 500, 502, 503, 504]);
const failureMessages = Object.freeze({
  configuration: 'Required smoke badge configuration is missing or invalid.',
  'endpoint-output': 'Smoke endpoint output is missing or invalid.',
  'attempts-exhausted': 'Smoke attempts exhausted.',
  cancelled: 'Smoke cancelled.',
  deadline: 'Smoke total deadline exceeded.',
  'request-timeout': 'Smoke request timed out.',
  'network-failure': 'Smoke network request failed.',
  'transient-status': 'Smoke transient status failed.',
  'http-status': 'Smoke response status failed.',
  'content-type': 'Smoke response content type failed.',
  'body-missing': 'Smoke response body is missing.',
  'body-limit': 'Smoke response body exceeded limit.',
  'invalid-json': 'Smoke response JSON failed.',
  'body-transfer': 'Smoke response body transfer failed.',
  'response-contract': 'Smoke response contract failed.',
  'health-contract': 'Smoke health contract failed.',
  'badge-contract': 'Smoke badge contract failed.',
  command: 'Invalid smoke command.',
  unknown: 'Smoke request failed.',
});
const ownsCategory = category => Object.hasOwn(failureMessages, category);
class SmokeFailure extends Error {
  constructor(category, context = {}) {
    super(failureMessages[ownsCategory(category) ? category : 'unknown']);
    this.category = ownsCategory(category) ? category : 'unknown';
    // Keep only labels and numbers from closed domains. Never retain URLs/inputs.
    this.context = Object.freeze({
      origin: ['gateway', 'public'].includes(context.origin) ? context.origin : undefined,
      check: ['health', 'tests', 'nuget'].includes(context.check) ? context.check : undefined,
      status: Number.isInteger(context.status) && context.status >= 100 && context.status <= 599 ? context.status : undefined,
      attempt: Number.isInteger(context.attempt) && context.attempt >= 1 && context.attempt <= 5 ? context.attempt : undefined,
      reason: ownsCategory(context.reason) ? context.reason : undefined,
    });
  }
}
class TransientFailure extends SmokeFailure {}
function withContext(error, context) {
  const Failure = error instanceof TransientFailure ? TransientFailure : SmokeFailure;
  return new Failure(error instanceof SmokeFailure ? error.category : 'unknown', {
    ...(error instanceof SmokeFailure ? error.context : {}), ...context,
  });
}

export function validateSmokeConfig({ testBadgePath, nugetBadgePath } = {}) {
  if (!safeBadgePath(testBadgePath, '/badges/tests/', 4, true) ||
      !safeBadgePath(nugetBadgePath, '/badges/packages/nuget/', 1, false)) {
    throw new SmokeFailure('configuration');
  }
  return { testBadgePath, nugetBadgePath };
}

function safeBadgePath(value, prefix, count, branchSlashes) {
  if (typeof value !== 'string' || value.length > 2048 || !value.startsWith(prefix)) return false;
  const components = value.slice(prefix.length).split('/');
  if (components.length !== count) return false;
  try {
    return components.every((component, index) => {
      const decoded = decodeURIComponent(component);
      const parts = branchSlashes && index === count - 1 ? decoded.split('/') : [decoded];
      return parts.every(part => /^[A-Za-z0-9_.-]+$/.test(part) && part !== '.' && part !== '..');
    });
  } catch { return false; }
}

// The public seam accepts explicit targets; options substitute only I/O, time, and
// cancellation for deterministic local fixtures. wait(milliseconds, { signal })
// is optional; the runner also bounds an injected wait that ignores cancellation.
export async function runSmoke(config, options = {}) {
  const paths = validateSmokeConfig(config);
  const fetchImpl = options.fetch ?? globalThis.fetch;
  const runtime = {
    now: options.now ?? (() => performance.now()),
    setTimeout: options.setTimeout ?? globalThis.setTimeout,
    clearTimeout: options.clearTimeout ?? globalThis.clearTimeout,
    signal: options.signal,
  };
  const deadline = runtime.now() + 180000;
  const wait = options.wait ?? ((milliseconds, { signal }) => pause(runtime, milliseconds, signal));
  let checks = 0;
  const bases = [['gateway', config.apiUrl], ['public', config.customDomainUrl]].map(([origin, value]) => {
    try { return [origin, validateBase(value)]; }
    catch (error) { throw withContext(error, { origin }); }
  });
  for (const [origin, base] of bases) {
    for (const [path, kind] of [['/health', 'health'], [paths.testBadgePath, 'tests'], [paths.nugetBadgePath, 'nuget']]) {
      for (let attempt = 1; attempt <= 5; attempt++) {
        try {
          await bounded(runtime, deadline, 10000, signal => checkResponse(fetchImpl, `${base}${path}`, kind, signal));
          break;
        } catch (error) {
          const failure = withContext(error, { origin, check: kind, attempt });
          if (!(failure instanceof TransientFailure)) throw failure;
          if (attempt === 5) throw new SmokeFailure('attempts-exhausted', { ...failure.context, reason: failure.category });
          try { await bounded(runtime, deadline, null, signal => wait(1000, { signal })); }
          catch (waitError) { throw withContext(waitError, { origin, check: kind, attempt }); }
        }
      }
      checks++;
    }
  }
  return { checks };
}

function assertActive(runtime, deadline) {
  if (runtime.signal?.aborted) throw new SmokeFailure('cancelled');
  if (runtime.now() >= deadline) throw new SmokeFailure('deadline');
}

async function bounded(runtime, deadline, requestTimeout, operation) {
  assertActive(runtime, deadline);
  const remaining = deadline - runtime.now();
  const duration = requestTimeout === null ? remaining : Math.min(requestTimeout, remaining);
  const controller = new AbortController();
  let timer, abort;
  try {
    const result = await new Promise((resolve, reject) => {
      abort = () => {
        reject(new SmokeFailure('cancelled'));
        controller.abort();
      };
      runtime.signal?.addEventListener('abort', abort, { once: true });
      timer = runtime.setTimeout(() => {
        reject(duration === remaining ? new SmokeFailure('deadline') : new TransientFailure('request-timeout'));
        controller.abort();
      }, duration);
      try { Promise.resolve(operation(controller.signal)).then(resolve, reject); }
      catch (error) { reject(error); }
    });
    assertActive(runtime, deadline);
    return result;
  } finally {
    runtime.clearTimeout(timer);
    runtime.signal?.removeEventListener('abort', abort);
    controller.abort();
  }
}

function pause(runtime, milliseconds, signal) {
  return new Promise((resolve, reject) => {
    const cleanup = () => { runtime.clearTimeout(timer); signal.removeEventListener('abort', abort); };
    const abort = () => { cleanup(); reject(new SmokeFailure('cancelled')); };
    const timer = runtime.setTimeout(() => { cleanup(); resolve(); }, milliseconds);
    signal.addEventListener('abort', abort, { once: true });
    if (signal.aborted) abort();
  });
}

async function checkResponse(fetchImpl, url, kind, signal) {
  let response;
  try { response = await fetchImpl(url, { method: 'GET', redirect: 'manual', signal }); }
  catch (error) {
    if (error instanceof TypeError) throw new TransientFailure('network-failure');
    throw new SmokeFailure('unknown');
  }
  try { await inspectResponse(response, kind, signal); }
  catch (error) { throw withContext(error, { status: response.status }); }
}

async function inspectResponse(response, kind, signal) {
  if (signal.aborted) {
    void response.body?.cancel().catch(() => {});
    throw new SmokeFailure('cancelled');
  }
  if (response.status !== 200) {
    void response.body?.cancel().catch(() => {});
    if (transientStatuses.has(response.status)) throw new TransientFailure('transient-status');
    throw new SmokeFailure('http-status');
  }
  if (!/^application\/json(?:\s*;|$)/i.test(response.headers.get('content-type') ?? '')) {
    void response.body?.cancel().catch(() => {});
    throw new SmokeFailure('content-type');
  }
  const reader = response.body?.getReader();
  if (!reader) throw new SmokeFailure('body-missing');
  const abort = () => { void reader.cancel().catch(() => {}); };
  signal.addEventListener('abort', abort, { once: true });
  let value;
  try {
    const chunks = [];
    let bytes = 0;
    while (true) {
      const part = await reader.read();
      if (part.done) break;
      bytes += part.value.byteLength;
      if (bytes > 65536) throw new SmokeFailure('body-limit');
      chunks.push(Buffer.from(part.value));
    }
    try {
      value = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.concat(chunks)));
    } catch { throw new SmokeFailure('invalid-json'); }
  } catch (error) {
    if (error instanceof TypeError && !signal.aborted) throw new TransientFailure('body-transfer');
    throw withContext(error, {});
  } finally {
    signal.removeEventListener('abort', abort);
    void reader.cancel().catch(() => {});
  }
  validateResponse(value, kind);
}

function validateBase(value) {
  if (typeof value !== 'string' || value.length > 2048 ||
      !/^https:\/\/[A-Za-z0-9.-]+(?::[0-9]+)?(?:\/[A-Za-z0-9_-]+)*\/?$/.test(value)) {
    throw new SmokeFailure('endpoint-output');
  }
  try {
    const url = new URL(value);
    if (url.username || url.password || !url.hostname) throw new Error();
  } catch { throw new SmokeFailure('endpoint-output'); }
  return value.replace(/\/$/, '');
}

function validateResponse(value, kind) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new SmokeFailure('response-contract');
  if (kind === 'health') {
    if (value.status !== 'Healthy' || typeof value.time_stamp !== 'string' ||
        !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(value.time_stamp) ||
        !Number.isFinite(Date.parse(value.time_stamp))) throw new SmokeFailure('health-contract');
  } else if (value.schemaVersion !== 1 || value.label !== kind ||
      typeof value.message !== 'string' || value.message.trim().length === 0 ||
      (value.isError != null && typeof value.isError !== 'boolean') || value.isError === true) {
    throw new SmokeFailure('badge-contract');
  }
}

async function main() {
  const command = process.argv[2];
  if (process.argv.length !== 3 || !['validate', 'run'].includes(command)) throw new SmokeFailure('command');
  const paths = validateSmokeConfig({
    testBadgePath: process.env.SMOKE_TEST_BADGE_PATH,
    nugetBadgePath: process.env.SMOKE_NUGET_BADGE_PATH,
  });
  if (command === 'validate') {
    console.log('Smoke configuration validated.');
    return;
  }
  const outputs = JSON.parse(await readFile('artifacts/cdk-outputs.json', 'utf8'));
  const stack = outputs?.BadgeSmithStack;
  const controller = new AbortController();
  const abort = () => controller.abort();
  process.once('SIGINT', abort);
  process.once('SIGTERM', abort);
  try {
    const result = await runSmoke({ ...paths, apiUrl: stack?.BadgeSmithApiUrl, customDomainUrl: stack?.CustomDomainUrl }, { signal: controller.signal });
    console.log(`Smoke checks passed (${result.checks} GET checks).`);
  } finally {
    process.removeListener('SIGINT', abort);
    process.removeListener('SIGTERM', abort);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    const failure = withContext(error, {});
    const fields = [`category=${failure.category}`];
    for (const [key, value] of Object.entries(failure.context)) {
      if (value !== undefined) fields.push(`${key}=${value}`);
    }
    console.error(`Delivery smoke failed [${fields.join(', ')}].`);
    process.exitCode = 1;
  });
}
