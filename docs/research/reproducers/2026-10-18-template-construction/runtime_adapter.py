"""Historical H2 adapter, public flattened adaptation; NOT the as-executed study witness.
No default build, listener, native execution, network access or hidden imports.
Native execution requires `inside --execute-native-fixture` in the documented closed container.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import http.server
import json
import os
from pathlib import Path
import re
import shutil
import socket
import ssl
import stat
import struct
import subprocess
import sys
import threading
import time
from types import SimpleNamespace
from urllib.parse import urlsplit
import zipfile

class Rejected(Exception):
    pass

def need(condition, reason):
    if not condition:
        raise Rejected(reason)

def validate_protocol(method, path, index):
    need(0 <= index <= 100 and ((method == "GET" and path == "/2018-06-01/runtime/invocation/next") or
         (method == "POST" and index < 100 and path == f"/2018-06-01/runtime/invocation/local-{index}/response")), "runtime_path_or_id")
    return True

def validate_limits(events, upstream, body, elapsed, errors, expected_upstream):
    need(0 <= events <= 100 and 0 <= upstream <= 100 and 0 <= body <= 65536 and
         0 <= elapsed <= 30_000_000_000 and errors == 0 and upstream == expected_upstream, "budget_or_revalidation")
    return True

def validate_uri(uri):
    u = urlsplit(uri)
    need(u.scheme == "https" and u.hostname == "127.0.0.1" and u.port is not None and 0 < u.port < 65536 and
         not u.username and not u.password and u.path == "/" and not u.query and not u.fragment, "external_uri")
    return True

def validate_environment(actual, expected):
    need(actual == expected, "foreign_environment")
    return True

def read_complete(stream, size, clock=time.monotonic_ns):
    need(0 <= size <= 65536, "body_cap")
    blocks = []
    remaining = size
    while remaining:
        block = stream.read(remaining)
        need(block and len(block) <= remaining, "body_incomplete")
        blocks.append(block)
        remaining -= len(block)
    timestamp = clock()  # Before joining/parsing/asserting, after the final body byte.
    return b"".join(blocks), timestamp

def check_live(actual, expected, owned):
    need(actual["pid"] in owned and actual["alive"] and actual["notDeleted"], "unowned_or_dead_pid")
    need(all(actual.get(k) == v for k, v in expected.items()), "live_identity_mismatch")
    return True

def parse_markers(raw):
    need(len(raw) <= 65536 and (not raw or raw.endswith(b"\n")), "marker_body_cap_or_incomplete")
    lines = raw.splitlines()
    need(len(lines) <= 100, "marker_count")
    rows = []
    for line in lines:
        match = re.fullmatch(rb"NP\|([0-9]+)\|([0-9]+)\|([0-9]+)\|([0-9]+)\|([0-9]+)", line)
        need(match is not None, "marker_format")
        rows.append([int(v) for v in match.groups()])
    return rows

BODY = '{"schemaVersion":1,"label":"nuget","message":"1.2.3","color":"blue","namedLogo":"nuget"}'

TAG = '"' + hashlib.sha256(BODY.encode()).hexdigest().upper() + '"'

CASES = {"NoHeader": {}, "Quoted": {"if-none-match": TAG},
         "WeakList": {"if-none-match": ' \t"different", w/ ' + TAG.lower() + ' , "other" '},
         "Miss": {"if-none-match": '"different", W/"other", unquoted'}}

CACHE = "public, s-maxage=600, max-age=300, stale-while-revalidate=1200, stale-if-error=3600"

RUNTIME_IMAGE = "sha256:446efb4225200a0a196707aa7953898b37962fd3b8859a636035de3af657081e"

RUNTIME_ENV = {"PATH": "/usr/local/bin:/usr/bin:/bin", "HOME": "/work/home", "TMPDIR": "/work/tmp",
               "XDG_CONFIG_HOME": "/work/xdg", "XDG_CACHE_HOME": "/work/cache", "LANG": "C.UTF-8",
               "SSL_CERT_FILE": "/input/fixture.crt", "PYTHONDONTWRITEBYTECODE": "1"}

class Quiet(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def setup(self):
        super().setup()
        self.connection.settimeout(3)
        self.connection.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

    def log_message(self, *_): pass

    def reply(self, status, body=b"", headers=None):
        self.send_response(status)
        self.send_header("Content-Length", str(len(body)))
        for k, v in (headers or {}).items(): self.send_header(k, v)
        self.end_headers()
        if body: self.wfile.write(body); self.wfile.flush()

    def failed(self, category):
        s = self.server.state
        s["failures"].append(category)
        s["finished"].set()
        self.close_connection = True

class Upstream(Quiet):
    def do_GET(self):
        s = self.server.state
        try:
            need(self.path == "/v3-flatcontainer/badgesmith-fixture/index.json", "unexpected_upstream_path")
            s["upstream"] += 1
            validate_limits(len(s["results"]), s["upstream"], 0, time.monotonic_ns() - s["t0"], len(s["failures"]), s["upstream"])
            first = s["upstream"] == 1
            need(first or (self.headers.get("If-None-Match") == '"fixture-v1"' and
                           self.headers.get("If-Modified-Since") == "Fri, 16 Oct 2026 12:00:00 GMT"), "upstream_validator_missing")
            status = 200 if first else 304
            s["upstreamStatuses"][str(status)] += 1
            self.reply(status, b'{"versions":["1.0.0","1.2.3","2.0.0-preview.1"]}' if first else b"",
                       {"ETag": '"fixture-v1"', "Last-Modified": "Fri, 16 Oct 2026 12:00:00 GMT", "Content-Type": "application/json; charset=utf-8"})
        except (Rejected, OSError, ValueError, KeyError): self.failed("upstream_protocol_failure")

    def do_POST(self): self.failed("unexpected_upstream_method")

class Server(http.server.ThreadingHTTPServer):
    daemon_threads = True
    def handle_error(self, *_):
        self.state["failures"].append("fixture_thread_failure")
        self.state["finished"].set()

def literal_app_env(runtime_port, upstream_port):
    uri = "https://127.0.0.1:" + str(upstream_port) + "/"
    validate_uri(uri)
    return {"PATH": "/usr/local/bin:/usr/bin:/bin", "HOME": "/work/home", "TMPDIR": "/work/tmp",
            "XDG_CONFIG_HOME": "/work/xdg", "XDG_CACHE_HOME": "/work/cache", "LANG": "C.UTF-8",
            "DOTNET_ENVIRONMENT": "Production", "BADGESMITH_UPSTREAM_MODE": "Live", "APP_VERSION": "1.0.0.0",
            "AWS_LAMBDA_RUNTIME_API": "127.0.0.1:" + str(runtime_port), "AWS_REGION": "eu-central-1",
            "AWS_EC2_METADATA_DISABLED": "true", "AWS_CONFIG_FILE": "/dev/null", "AWS_SHARED_CREDENTIALS_FILE": "/dev/null",
            "HTTP_NUGET_BASE_URL": uri, "HTTP_GITHUB_BASE_URL": uri, "SSL_CERT_FILE": "/input/fixture.crt"}

def live_row(child):
    root = Path("/proc") / str(child.pid)
    path = os.readlink(root / "exe")
    return {"pid": child.pid, "startTicks": int((root / "stat").read_text().rsplit(")", 1)[1].split()[19]),
            "exePath": path, "exeSha256": sha(root / "exe"), "cwd": os.readlink(root / "cwd"),
            "pidNamespace": os.readlink(root / "ns/pid"), "alive": child.poll() is None, "notDeleted": not path.endswith(" (deleted)")}

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write_new(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, mode=0o700, exist_ok=True)
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600), "w") as f:
        json.dump(value, f, indent=2, sort_keys=True)
        f.write("\n")

def inspect_zip(path):
    need(Path(path).is_file() and not Path(path).is_symlink(), "zip_not_regular")
    with zipfile.ZipFile(path) as z:
        need(z.namelist() == ["bootstrap"], "zip_inventory")
        entry = z.getinfo("bootstrap")
        need(entry.create_system == 3 and entry.external_attr >> 16 == 0o100755 and
             entry.compress_type == zipfile.ZIP_DEFLATED and entry.file_size <= 100_000_000 and
             entry.flag_bits & 1 == 0, "zip_metadata")
        need(z.testzip() is None, "zip_crc")
        data = z.read("bootstrap")
    need(len(data) >= 64 and data[:6] == b"\x7fELF\x02\x01" and
         struct.unpack_from("<H", data, 18)[0] == 62, "elf_not_x64")
    return {"zipSha256": sha(path), "zipSize": Path(path).stat().st_size,
            "entrySha256": hashlib.sha256(data).hexdigest(), "elfSha256": hashlib.sha256(data).hexdigest(),
            "entrySize": len(data), "compressedSize": entry.compress_size, "crc": entry.CRC,
            "mode": "0100755", "compression": "Deflate", "machine": 62, "class": 64, "endian": "little"}

def canonical(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()

b = SimpleNamespace(sha=sha, write_new=write_new, inspect_zip=inspect_zip, canonical=canonical, Rejected=Rejected)

def cohort(kind):
    if kind == 'main': return ['NoHeader'] + list(CASES) * 20, ['first-feature'] + ['warmup'] * 16 + ['warm'] * 64
    need(kind == 'health', 'unknown_cohort')
    return ['Health', 'NoHeader', 'NoHeader', 'Quoted', 'WeakList', 'Miss'], ['first-health', 'first-feature'] + ['warm-control'] * 4

def validate_plan(cases, roles):
    need(any((cases, roles) == cohort(k) for k in ('main', 'health')), 'cohort_roles_or_caps')
    return True

def response_contract(payload, case):
    r = json.loads(payload)
    need(set(r) <= {'statusCode', 'headers', 'body', 'isBase64Encoded', 'cookies'} and not r.get('cookies') and
         not r.get('isBase64Encoded', False), 'response_schema')
    h = {k.lower(): v for k, v in r['headers'].items()}
    expected = {'content-type': 'application/json; charset=utf-8', 'access-control-allow-origin': '*'}
    if case == 'Health':
        expected['cache-control'] = 'no-store'
        need(r['statusCode'] == 200 and h == expected, 'health_headers_status')
        body = json.loads(r['body'])
        need(set(body) == {'status', 'time_stamp'} and body['status'] == 'Healthy', 'health_schema')
        try: stamp = datetime.fromisoformat(body['time_stamp'].replace('Z', '+00:00'))
        except (ValueError, TypeError, AttributeError): raise Rejected('health_timestamp') from None
        need(stamp.tzinfo is not None and stamp.utcoffset().total_seconds() == 0, 'health_timestamp_utc')
    else:
        expected |= {'etag': TAG, 'cache-control': CACHE, 'last-modified': 'Fri, 16 Oct 2026 12:00:00 GMT'}
        status = 304 if case in ('Quoted', 'WeakList') else 200
        need(r['statusCode'] == status and r.get('body') == (None if status == 304 else BODY) and h == expected,
             'nuget_exact_contract')
    return r['statusCode']

class Runtime(Quiet):
    def do_GET(self):
        s = self.server.state
        try:
            index = len(s['results']); s['runtimeGets'] += 1
            validate_protocol('GET', self.path, index)
            need(not s['pending'], 'overlapping_runtime_poll')
            if s['ready'] is None:
                s['ready'] = time.monotonic_ns(); s['readyUtcNs'] = time.time_ns()
            if index == len(s['cases']):
                s['finished'].set(); s['stop'].wait(3); self.close_connection = True; return
            need(index < len(s['cases']), 'extra_runtime_poll')
            case = s['cases'][index]
            path = '/health' if case == 'Health' else '/badges/packages/nuget/badgesmith-fixture'
            event = {'version': '2.0', 'routeKey': 'GET ' + path, 'rawPath': path, 'rawQueryString': '',
                     'headers': {} if case == 'Health' else CASES[case], 'queryStringParameters': {}, 'isBase64Encoded': False,
                     'requestContext': {'requestId': 'local-fixture', 'stage': '$default', 'http': {'method': 'GET', 'path': path,
                         'protocol': 'HTTP/1.1', 'sourceIp': '127.0.0.1', 'userAgent': 'local-fixture'}}}
            data = json.dumps(event, separators=(',', ':')).encode()
            headers = {'Content-Type': 'application/json', 'Lambda-Runtime-Aws-Request-Id': 'local-' + str(index),
                       'Lambda-Runtime-Deadline-Ms': str(int(time.time() * 1000) + 30000),
                       'Lambda-Runtime-Invoked-Function-Arn': 'arn:aws:lambda:eu-central-1:000000000000:function:local-fixture'}
            s['pending'] = True
            s['sentUtcNs'] = time.time_ns(); s['sent'] = time.monotonic_ns()
            self.reply(200, data, headers)
        except (Rejected, OSError, ValueError, KeyError): self.failed('runtime_get_failure')

    def do_POST(self):
        s = self.server.state
        try:
            s['runtimePostStarts'] += 1; index = len(s['results'])
            validate_protocol('POST', self.path, index)
            need(s['pending'] and index < len(s['cases']) and self.headers.get('Transfer-Encoding') is None, 'runtime_post_state')
            data, complete = read_complete(self.rfile, int(self.headers.get('Content-Length', '-1')))
            complete_utc = time.time_ns()
            s['runtimePostBodies'] += 1
            case = s['cases'][index]
            status = response_contract(data, case)
            if case == 'Health' and s.get('healthUtcBounds') is not None:
                stamp = datetime.fromisoformat(json.loads(json.loads(data)['body'])['time_stamp'].replace('Z', '+00:00')).timestamp()
                need(s['sentUtcNs'] / 1e9 - 1 <= stamp <= complete_utc / 1e9 + 1, 'health_timestamp_launch_bounds')
            validate_limits(index + 1, s['upstream'], len(data), complete - s['t0'], len(s['failures']),
                            sum(c != 'Health' for c in s['cases'][:index + 1]))
            roles = s.get('roles', ['first-health', 'first-feature'] + ['warm-control'] * 4)
            s['results'].append({'index': index, 'case': case, 'role': roles[index], 'sentNs': s['sent'],
                'sentUtcNs': s.get('sentUtcNs', complete_utc), 'completeBodyNs': complete, 'completeUtcNs': complete_utc,
                'elapsedNs': complete - s['sent'], 'status': status, 'responseBytes': len(data),
                'upstreamCumulative': s['upstream'], 'contractPass': True})
            s['pending'] = False; self.reply(202); s['runtimePostAcks'] += 1
        except (Rejected, OSError, ValueError, KeyError, IndexError): self.failed('runtime_response_failure')

def inside():
    result = {'status': 'FAIL', 'instrument': 'h2-body-complete-UTC-v2',
              'domain': 'local-container-linux-x64-NativeAOT-not-AL2023-ARM64-AWS'}
    child = None; servers = []; threads = []; state = None
    try:
        validate_environment(dict(os.environ), RUNTIME_ENV)
        for p in ('home', 'tmp', 'xdg', 'cache'): Path('/work', p).mkdir(mode=0o700)
        security = dict(line.split(':', 1) for line in Path('/proc/self/status').read_text().splitlines() if ':' in line)
        need(os.geteuid() > 0 and int(security['CapEff'], 16) == 0 and int(security['NoNewPrivs']) == 1 and
             os.statvfs('/').f_flag & os.ST_RDONLY and {n for _, n in socket.if_nameindex()} == {'lo'}, 'container_security')
        bundle, source, build = (json.loads(Path('/input', n + '.json').read_bytes()) for n in ('launch', 'source', 'build'))
        need(b.sha('/input/source.json') == bundle['sourceReceiptFileSha256'] and
             b.sha('/input/build.json') == bundle['buildReceiptFileSha256'], 'input_receipt_hash')
        need(source['status'] == build['status'] == 'PASS' and source['files'] == build['sourceFiles'] and
             source['frozenRoot'] == build['sourceRoot'] and b.canonical(source) == build['sourceReceiptSha256'], 'input_build_source_chain')
        need(b.inspect_zip('/input/artifact.zip') == build['artifact'], 'input_zip_chain')
        executable = '/executable/bootstrap'
        need(b.sha(executable) == build['artifact']['elfSha256'] and os.stat(executable).st_mode & 0o777 == 0o755, 'mounted_elf_chain')
        validate_plan(bundle['cases'], bundle['roles'])
        state = {'ready': None, 'sent': None, 'cases': bundle['cases'], 'roles': bundle['roles'], 'results': [], 'failures': [],
                 'pending': False, 'finished': threading.Event(), 'stop': threading.Event(), 'upstream': 0,
                 'upstreamStatuses': {'200': 0, '304': 0}, 'runtimeGets': 0, 'runtimePostStarts': 0, 'runtimePostBodies': 0,
                 'runtimePostAcks': 0, 'healthUtcBounds': True}
        upstream, runtime = Server(('127.0.0.1', 0), Upstream), Server(('127.0.0.1', 0), Runtime)
        servers = [upstream, runtime]
        for s in servers: s.state = state
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); context.load_cert_chain('/input/fixture.crt', '/input/fixture.key')
        upstream.socket = context.wrap_socket(upstream.socket, server_side=True)
        for s in servers:
            t = threading.Thread(target=s.serve_forever, kwargs={'poll_interval': .01}, daemon=True)
            t.start(); threads.append(t)
        env = literal_app_env(runtime.server_port, upstream.server_port)
        settings = bundle.get('settings')
        if settings is not None:
            need(source['variant'] == 'H2Settings' and set(settings) == {'initialVersion', 'dotnet', 'aspnet', 'expectedVersion', 'expectedEnvironment'}, 'settings_plan')
            for key, value in (('APP_VERSION', settings['initialVersion']), ('DOTNET_ENVIRONMENT', settings['dotnet']),
                               ('ASPNETCORE_ENVIRONMENT', settings['aspnet'])):
                if value is None: env.pop(key, None)
                else: env[key] = value
            env['H2_EXPECT_VERSION'] = settings['expectedVersion']
            env['H2_EXPECT_ENVIRONMENT'] = settings['expectedEnvironment']
        markers, pipes = [], {'bytes': 0, 'errors': 0, 'knownFailureCodes': []}
        load = list(os.getloadavg()); cpu = Path('/proc/stat').read_text().splitlines()[0].split()[1:]
        state['t0UtcNs'] = time.time_ns(); state['t0'] = time.monotonic_ns()
        child = subprocess.Popen([executable], cwd='/work', env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        pid_root = Path('/proc') / str(child.pid)
        start_ticks = int((pid_root / 'stat').read_text().rsplit(')', 1)[1].split()[19])
        initial_cwd = os.readlink(pid_root / 'cwd')
        need(initial_cwd == '/work', 'initial_cwd')
        def drain(pipe):
            while line := pipe.readline(65537):
                pipes['bytes'] += len(line)
                if pipes['bytes'] > 65536: pipes['errors'] += 1; state['finished'].set(); break
                for code in (b'OutOfMemoryException', b'TypeInitializationException', b'InvalidOperationException', b'JsonException',
                             b'NotSupportedException', b'SocketException', b'HttpRequestException', b'UnauthorizedAccessException'):
                    if code in line and code.decode() not in pipes['knownFailureCodes']: pipes['knownFailureCodes'].append(code.decode())
        drains = [threading.Thread(target=drain, args=(p,), daemon=True) for p in (child.stdout, child.stderr)]
        for t in drains: t.start()
        need(state['finished'].wait(30), 'native_timeout')
        need(not state['failures'] and len(state['results']) == len(state['cases']) and not state['pending'], 'runtime_closing_accounting')
        validate_limits(len(state['results']), state['upstream'], 0, time.monotonic_ns() - state['t0'], pipes['errors'],
                        sum(c != 'Health' for c in state['cases']))
        observed = live_row(child)
        check_live(observed, {'pid': child.pid, 'startTicks': start_ticks, 'exePath': executable,
             'exeSha256': build['artifact']['elfSha256'], 'cwd': '/work', 'pidNamespace': os.readlink('/proc/self/ns/pid')}, {child.pid})
        memory = {k: int(v.strip().split()[0]) for k, v in (line.split(':', 1) for line in (pid_root / 'status').read_text().splitlines()
                   if ':' in line) if k in ('VmRSS', 'VmHWM')}
        first = state['results'][0]; ready = state['ready'] - state['t0']; gap = first['sentNs'] - state['ready']
        need(first['completeBodyNs'] - state['t0'] == ready + first['elapsedNs'] + gap, 'paired_clock_identity')
        result |= {'status': 'PASS', 'variant': source['variant'], 'role': bundle['role'], 'cohort': bundle['cohort'], 'pidIdentity': observed,
                   'initialStartTicks': start_ticks, 'initialCwd': initial_cwd, 'sourceManifestSha256': b.canonical(source),
                   'buildReceiptSha256': b.sha('/input/build.json'), 'buildId': build['buildId'], 'artifact': build['artifact'],
                   't0Ns': state['t0'], 't0UtcNs': state['t0UtcNs'], 'readyAnchorNs': state['ready'], 'readyUtcNs': state['readyUtcNs'],
                   'readyNs': ready, 'firstInvokeNs': first['elapsedNs'], 'dispatchGapNs': gap,
                   'pairedReadyFirstNs': ready + first['elapsedNs'], 'totalFirstCompletionNs': first['completeBodyNs'] - state['t0'],
                   'sequenceCompletionNs': state['results'][-1]['completeBodyNs'] - state['t0'], 'memoryKiB': memory,
                   'invocations': state['results'], 'upstreamCalls': state['upstream'], 'upstreamStatuses': state['upstreamStatuses'],
                   'envLiteralKeys': sorted(env), 'failures': state['failures'], 'loadBefore': load, 'cpuBefore': list(map(int, cpu)),
                   'cpuAfter': list(map(int, Path('/proc/stat').read_text().splitlines()[0].split()[1:])), 'pipeBytesTransient': pipes['bytes']}
        if settings is not None:
            need(Path('/work/settings-result').read_bytes() == b'1\n', 'settings_numeric_passcode')
            result['settingsPasscode'] = 1
            result['timingEligible'] = False
    except (Rejected, b.Rejected, OSError, ValueError, KeyError, subprocess.SubprocessError) as e:
        result['category'] = str(e) if isinstance(e, (Rejected, b.Rejected)) else type(e).__name__
        if state: result |= {'invocations': state['results'], 'upstreamCalls': state['upstream'], 'failures': state['failures'],
                             'runtimeProgress': {k: state[k] for k in ('runtimeGets', 'runtimePostStarts', 'runtimePostBodies', 'runtimePostAcks', 'pending')}}
        if 'pipes' in locals(): result['pipeProjection'] = pipes
    finally:
        result['closeAnchorNs'] = time.monotonic_ns(); result['closeUtcNs'] = time.time_ns()
        if child:
            result['exitBeforeTeardown'] = child.poll()
            if child.poll() is None: child.terminate()
            try: child.wait(timeout=2)
            except subprocess.TimeoutExpired: child.kill(); child.wait(timeout=2)
            result['nativeReaped'] = child.poll() is not None
            for t in drains: t.join(timeout=2)
        if state: state['stop'].set()
        for s in servers: s.shutdown(); s.server_close()
        for t in threads: t.join(timeout=2)
        result['fixtureThreadsReaped'] = all(not t.is_alive() for t in threads)
        sidecar = Path('/work/diagnostic-markers')
        result['markers'] = []
        if sidecar.exists():
            try: result['markers'] = parse_markers(sidecar.read_bytes())
            except Rejected as e: result['status'], result['category'] = 'FAIL', str(e)
        b.write_new('/work/result.json', result)
    print(json.dumps({'status': result['status'], 'category': result.get('category')}))
    return 0 if result['status'] == 'PASS' else 1


def validate_source_root(value, files):
    """Exact stable owned tree: lexical chain/types/names first, then only declared file bytes."""
    need(isinstance(value, (str, os.PathLike)), 'source_root_lexical')
    raw = os.fspath(value)
    need(isinstance(raw, str) and raw and not any(ord(c) < 32 or ord(c) == 127 for c in raw)
         and '..' not in raw.split('/'), 'source_root_lexical')
    root = Path(raw)
    need(root.is_absolute(), 'source_root_lexical')
    current = Path(root.anchor)
    try:
        need(stat.S_ISDIR(current.lstat().st_mode), 'source_root_lexical')
        for part in root.parts[1:]:
            current = current / part
            need(stat.S_ISDIR(current.lstat().st_mode), 'source_root_lexical')
    except OSError:
        raise Rejected('source_root_lexical') from None
    need(isinstance(files, dict), 'source_relative_path')
    directories = set()
    for name in files:
        need(isinstance(name, str) and name and not any(ord(c) < 32 or ord(c) == 127 for c in name),
             'source_relative_path')
        parts = name.split('/')
        need(not Path(name).is_absolute() and all(p not in ('', '.', '..') for p in parts),
             'source_relative_path')
        directories.update('/'.join(parts[:n]) for n in range(1, len(parts)))
    observed = set()
    pending = [(root, '')]
    try:
        while pending:
            directory, prefix = pending.pop()
            with os.scandir(directory) as entries:
                for entry in entries:
                    name = prefix + entry.name
                    need(name in files or name in directories, 'source_inventory')
                    mode = entry.stat(follow_symlinks=False).st_mode
                    if stat.S_ISREG(mode):
                        need(name in files, 'source_entry_type')
                        observed.add(name)
                    elif stat.S_ISDIR(mode):
                        need(name in directories, 'source_entry_type')
                        pending.append((Path(entry.path), name + '/'))
                    else:
                        raise Rejected('source_entry_type')
    except OSError:
        raise Rejected('source_inventory') from None
    need(observed == set(files), 'source_inventory')
    for name, expected in files.items():
        path = root / name
        need(sha(path) == expected['sha256'] and path.stat().st_size == expected['size'] and
             oct(stat.S_IMODE(path.stat().st_mode)) == expected['mode'], 'source_file_bytes')
    return root


def prepare(args):
    """Validate explicit fresh source/build/artifact inputs; create an exclusively new fixture input."""
    source_path, build_path = Path(args.source_receipt), Path(args.build_receipt)
    source, build = json.loads(source_path.read_bytes()), json.loads(build_path.read_bytes())
    need(source['status'] == build['status'] == 'PASS', 'unpassed_source_build')
    need(build['sourceFiles'] == source['files'] and build['sourceRoot'] == source['frozenRoot'] and
         build['sourceReceiptSha256'] == canonical(source), 'source_build_chain')
    validate_source_root(source['frozenRoot'], source['files'])
    artifact = inspect_zip(args.zip)
    need(artifact == build['artifact'], 'zip_build_chain')
    exe = Path(args.executable)
    need(not exe.is_symlink() and exe.is_file() and sha(exe) == artifact['elfSha256'] and
         stat.S_IMODE(exe.stat().st_mode) == 0o755, 'executable_build_chain')
    need(isinstance(build['buildId'], str) and re.fullmatch('[a-z0-9-]+', build['buildId']), 'build_id')
    destination = Path(args.out)
    need(destination.is_absolute() and not destination.exists(), 'new_absolute_destination')
    destination.mkdir(mode=0o700)
    for p, n in [(source_path,'source.json'), (build_path,'build.json'), (Path(args.zip),'artifact.zip')]:
        shutil.copyfile(p, destination/n)
    cases, roles = cohort(args.cohort)
    bundle={'sourceReceiptFileSha256':sha(source_path),
        'buildReceiptFileSha256':sha(build_path),'role':args.role,'cohort':args.cohort,'cases':cases,'roles':roles}
    if args.settings_json is not None:
        settings=json.loads(Path(args.settings_json).read_bytes())
        need(source['variant']=='H2Settings' and args.cohort=='health' and
             set(settings)=={'initialVersion','dotnet','aspnet','expectedVersion','expectedEnvironment'} and
             all(isinstance(v,str) or v is None for v in settings.values()),'settings_plan')
        bundle['settings']=settings
    write_new(destination/'launch.json',bundle)
    print(json.dumps({'status':'PASS','inputPrepared':True,'nativeExecuted':False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command',required=True)
    p = commands.add_parser('prepare')
    for name in ['source-receipt','build-receipt','zip','executable','out','role']:
        p.add_argument('--'+name,required=True)
    p.add_argument('--cohort',choices=['main','health'],required=True)
    p.add_argument('--settings-json',help='Explicit fake Settings condition from the numeric packet; never inherited configuration.')
    p = commands.add_parser('inside')
    p.add_argument('--execute-native-fixture',action='store_true',required=True)
    args = parser.parse_args()
    if args.command == 'prepare': prepare(args); return 0
    return inside()


if __name__ == '__main__':
    raise SystemExit(main())
