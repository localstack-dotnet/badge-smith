"""Offline arithmetic replay only. Standard library; no writes, network or launches."""
import copy
import csv
import hashlib
import io
import json
import math
from pathlib import Path
import statistics

RESEARCH = Path(__file__).resolve().parents[2]
PREFIX = '2026-10-19-native-first-request'
ORDERS = {
    'baseline': ['B-main1', 'B-health1', 'B-health2', 'B-main2'],
    'framing': ['framing-original1', 'framing-coalesced1', 'framing-coalesced2', 'framing-original2'],
    'tls': ['tls-main1', 'tls-main2'],
    'fixture-only-trust': ['v2-trust-default1', 'v2-trust-empty1', 'v2-trust-empty2', 'v2-trust-default2'],
    'common-bundle': ['common-default1', 'common-empty1', 'common-empty2', 'common-default2'],
    'al2023-v2-invalid': ['official-default1'],
    'al2023-v4': ['official-default1', 'official-default2'],
}
CASES = ['NoHeader', 'Quoted', 'WeakList', 'Miss']
ELF = 'ed6fdf159b9cc977e0da3bbc172c829da8ef87f9258164e779c9bac353c6c3dd'


def require(condition, label):
    if not condition:
        raise ValueError(label)


def certificate_set(section):
    values = section['derSha256']
    require(values == sorted(set(values)) and len(values) == section['uniqueDER'], 'certificate identities')
    require(all(len(value) == 64 and all(c in '0123456789abcdef' for c in value) for value in values), 'DER hash syntax')
    require(section['blocks'] >= len(values), 'certificate occurrences')
    digest = hashlib.sha256(('\n'.join(values)+'\n').encode('ascii')).hexdigest()
    require(digest == section['inputSetSha256'], 'certificate set hash')
    return set(values)


def observations(process, first):
    probe = process['observer']
    if probe is None:
        return 0
    events = probe['events']; phases = {event['phase']: event for event in events}
    tls = 'connection' in probe
    expected = {'cpu_before_send', 'event_sent', 'event_write_return', 'upstream_entry', 'cpu_at_upstream',
                'upstream_write_start', 'upstream_write_return', 'runtime_post_entry', 'body_complete', 'cpu_after_body'}
    if tls:
        expected |= {'tcp_accept_observed', 'cpu_at_accept', 'server_wrap_start', 'server_wrap_return', 'cpu_after_server_wrap'}
    require(set(phases) == expected and len(events) == len(expected), 'phase inventory')
    previous = {}; bracket_sum = 0
    for event in events:
        require(event['index'] == 0 and event['upstreamIndex'] == 1, 'phase request correlation')
        require(0 <= event['observerStartNs'] <= event['observerEndNs'] and 0 <= event['ns'] <= event['observerEndNs'], 'observer brackets')
        require(event['ns'] >= previous.get(event['threadId'], 0), 'same-thread monotonic observations')
        previous[event['threadId']] = event['ns']
        bracket_sum += event['observerEndNs']-event['observerStartNs']
    require(bracket_sum == probe['observerBracketNs'], 'bracket sum, not a subtraction')
    at = {key: value['ns'] for key, value in phases.items()}
    require(at['event_sent'] == first['sentNs'] and at['body_complete'] == first['completeBodyNs'], 'body/send phase identity')
    ordered = ['event_sent', 'upstream_entry', 'upstream_write_start', 'runtime_post_entry', 'body_complete']
    if tls:
        ordered = ['event_sent', 'tcp_accept_observed', 'server_wrap_start', 'server_wrap_return', 'upstream_entry', 'body_complete']
        connection = probe['connection']
        require(connection['peer'] == connection['httpPeer'] and connection['peer'][0] == '127.0.0.1' and connection['httpMatched'], 'TLS peer correlation')
        require(connection['acceptedFd'] == connection['wrappedFd'] and connection['acceptedFd'] >= 0, 'TLS FD correlation')
        require(connection['nativePid'] == process['identity']['pid'] and connection['nativeStartTicks'] == process['identity']['startTicks'], 'TLS Native identity')
    require(all(at[left] <= at[right] for left, right in zip(ordered, ordered[1:])), 'causal partition order')
    require(sum(at[right]-at[left] for left, right in zip(ordered, ordered[1:])) == first['elapsedNs'], 'partition sum')
    cpu_order = ['cpu_before_send', 'cpu_at_accept', 'cpu_after_server_wrap', 'cpu_at_upstream', 'cpu_after_body'] if tls else ['cpu_before_send', 'cpu_at_upstream', 'cpu_after_body']
    ticks = (0, 0)
    for name in cpu_order:
        event = phases[name]
        require(event['pid'] == process['identity']['pid'] and event['startTicks'] == process['identity']['startTicks'], 'CPU identity')
        require(event['clockTicksPerSecond'] == probe['clockTicksPerSecond'] == 100, 'coarse CPU frequency')
        require(event['utime'] >= ticks[0] and event['stime'] >= ticks[1], 'CPU cumulative ticks')
        ticks = event['utime'], event['stime']
    return len(events)


def validate(packet, rows):
    require(packet['schemaVersion'] == 1 and packet['snapshotDate'] == '2026-10-19', 'packet version')
    studies = packet['studies']
    require([study['id'] for study in studies] == list(ORDERS), 'study inventory/order')
    groups = {}
    for row in rows:
        groups.setdefault((row['study'], row['process']), []).append(row)
    require(len(groups) == 21 and len(rows) == packet['invocationCsv']['rows'] == 1551, 'complete response inventory')
    accepted_responses = accepted_upstream = invalid_responses = phase_count = 0
    for study in studies:
        require(study['order'] == ORDERS[study['id']] == [process['id'] for process in study['processes']], 'fixed process order')
        invalid = study['id'] == 'al2023-v2-invalid'
        qualification = 'invalid' if invalid else 'accepted-local'
        require(study['qualification'] == qualification, 'study qualification')
        for process in study['processes']:
            events = groups.pop((study['id'], process['id']))
            require(process['qualification'] == qualification and all(row['qualification'] == qualification for row in events), 'invalid study not pooled')
            health = process['cohort'] == 'health'
            cases = ['Health', 'NoHeader']+CASES if health else ['NoHeader']+CASES*20
            roles = ['first-health', 'first-feature']+['warm-control']*4 if health else ['first-feature']+['warmup']*16+['warm']*64
            require(len(events) == len(cases) == process['responses'], 'process response count')
            previous = process['readyAnchorNs']; upstream = 0
            require(previous == process['t0Ns']+process['readyNs'], 'ready identity')
            for index, (event, case, role) in enumerate(zip(events, cases, roles)):
                require(event['index'] == index and event['case'] == case and event['role'] == role, 'case/role order')
                require(previous <= event['sentNs'] < event['completeBodyNs'] and event['elapsedNs'] == event['completeBodyNs']-event['sentNs'], 'complete-body clocks')
                require(event['status'] == (304 if case in ('Quoted', 'WeakList') else 200) and event['contractPass'] is True, 'recorded response oracle')
                require(0 < event['responseBytes'] <= 65536, 'response size')
                upstream += case != 'Health'
                require(event['upstreamCumulative'] == upstream, 'every-request revalidation count')
                previous = event['completeBodyNs']
            require(process['upstreamCalls'] == upstream and process['upstreamStatuses'] == {'200': 1, '304': upstream-1}, 'upstream statuses')
            require(process['apiStatuses'] == {str(code): sum(row['status'] == code for row in events) for code in (200, 304)}, 'API status totals')
            first = events[1 if health else 0]
            require(process['firstFeatureIndex'] == first['index'] and process['firstFeatureNs'] == first['elapsedNs'], 'first feature is not first health')
            require(process['initialDispatchGapNs'] == events[0]['sentNs']-process['readyAnchorNs'], 'initial dispatch gap')
            require(process['launchToFeatureNs'] == first['completeBodyNs']-process['t0Ns'], 'launch-to-feature clock')
            require(process['sequenceCompletionNs'] == events[-1]['completeBodyNs']-process['t0Ns'], 'sequence completion')
            warm = [event['elapsedNs'] for event in events if event['role'] == 'warm']
            require(process['warm64MedianNs'] == (statistics.median(warm) if warm else None), 'per-process warm64 median')
            require(process['memoryKiB']['VmHWM'] >= process['memoryKiB']['VmRSS'] > 0 and process['fixtureThreadsReaped'], 'recorded memory/fixture closure')
            require(process['artifact']['elfSha256'] == process['artifact']['entrySha256'] == process['identity']['exeSha256'] == ELF, 'ELF identity')
            require(process['identity']['alive'] and process['identity']['notDeleted'], 'post-sequence held-live identity')
            official = study['platform'] == 'al2023-x64'
            require(process['readyDomain'] == ('docker-transport-inclusive' if official else 'native-pre-popen-to-first-runtime-get'), 'ready domain')
            if official:
                outcome = process['outcome']
                require(outcome['status'] == ('INVALID' if invalid else 'PASS') and outcome['containersClosed'] and outcome['keyAbsent'] and outcome['errors'] == [], 'separate driver outcome/closure')
                require(outcome['error'] == ('native_pipe_projection' if invalid else None), 'retained V2 failure')
            else:
                require(process['nativeReapedInFixture'], 'recorded Native reaping')
            phase_count += observations(process, first)
            if invalid:
                invalid_responses += len(events)
            else:
                accepted_responses += len(events); accepted_upstream += upstream
    require(not groups and (accepted_responses, accepted_upstream, invalid_responses) == (1470, 1468, 81), 'qualified accounting')
    common = next(study for study in studies if study['id'] == 'common-bundle')['processes']
    source = set(packet['caInputs']['debianSets']['fileDER_SHA256'])
    require(len(source) == 150 and source == set(packet['caInputs']['debianSets']['directoryDER_SHA256']), 'observed Debian input equality')
    for process in common:
        trust = process['trustInputs']
        require(certificate_set(trust['osBundle']) == source, 'common source inputs')
        require(certificate_set(trust['common']) == source | certificate_set(trust['fixture']) and trust['common']['uniqueDER'] == 151, 'ordinary union only')
        require(trust['ordinaryInputSetOnly'] and trust['defaultUnionEqualsEmptyUnion'] and trust['directoryReferenceSubsetCommon'], 'input coverage, not policy')
    require(len({process['trustInputs']['fixture']['sha256'] for process in common}) == len({process['trustInputs']['common']['sha256'] for process in common}) == 1, 'same common cohort bytes')
    official = next(study for study in studies if study['id'] == 'al2023-v4')['processes']
    for process in official:
        trust = process['trustInputs']
        require(len(certificate_set(trust['official'])) == 143 and len(certificate_set(trust['fixture'])) == 1, 'official ordinary inputs')
        require(trust['auxiliaryTrustedPolicyEquivalence'] == 'NOT_ESTABLISHED' and process['trustDirArm'] == 'default', 'no official empty-directory experiment')
    require(len({process['trustInputs']['common']['sha256'] for process in official}) == 1, 'same official common file')
    bdn = packet['bdn']; keys = set(); actual_count = 0
    for case in bdn['cases']:
        require(case['corpus'] in (3, 32) and (case['method'], case['corpus']) not in keys, 'distinct managed cases')
        keys.add((case['method'], case['corpus']))
        result = [sample for sample in case['measurements'] if sample['IterationStage'] == 'Result']
        actual = [sample for sample in case['measurements'] if sample['IterationStage'] == 'Actual']
        require(len(result) == len(actual) == len(case['correctedValuesNs']) == 3, 'managed sample counts')
        values = [sample['Nanoseconds']/sample['Operations'] for sample in result]
        require(values == case['correctedValuesNs'], 'overhead-corrected managed samples')
        require(math.isclose(statistics.mean(values), case['meanNs'], abs_tol=1e-8) and math.isclose(statistics.stdev(values), case['stddevNs'], abs_tol=1e-8), 'managed mean/stddev')
        require(case['error999Ns'] > 0 and case['allocatedBytesPerOperation'] > 0, 'reported confidence/allocation fields')
        actual_count += len(actual)
    require(len(keys) == 8 and actual_count == bdn['actualSamples'] == 24 and bdn['successfulChildProcesses'] == 8, 'BDN observed inventory')
    return {'processes': 21, 'acceptedResponses': accepted_responses, 'acceptedUpstream': accepted_upstream,
            'invalidResponsesRetained': invalid_responses, 'observations': phase_count, 'bdnCases': 8, 'bdnActualSamples': actual_count}


def main():
    packet = json.loads((RESEARCH/'baselines'/(PREFIX+'.json')).read_bytes())
    raw = (RESEARCH/'baselines'/(PREFIX+'-invocations.csv')).read_bytes()
    require(hashlib.sha256(raw).hexdigest() == packet['invocationCsv']['sha256'], 'CSV byte hash')
    rows = list(csv.DictReader(io.StringIO(raw.decode('utf-8'))))
    for row in rows:
        for key in ('index', 'sentNs', 'completeBodyNs', 'elapsedNs', 'status', 'upstreamCumulative', 'responseBytes'):
            row[key] = int(row[key])
        require(row['contractPass'] in ('True', 'False'), 'boolean encoding')
        row['contractPass'] = row['contractPass'] == 'True'
    result = validate(packet, rows)
    rejected = 0
    for kind in ('elapsed', 'warm', 'qualification', 'cpu', 'union', 'managed'):
        changed = copy.deepcopy(packet); events = copy.deepcopy(rows)
        if kind == 'elapsed': events[0]['elapsedNs'] += 1
        if kind == 'warm': changed['studies'][0]['processes'][0]['warm64MedianNs'] += 1
        if kind == 'qualification': changed['studies'][5]['qualification'] = 'accepted-local'
        if kind == 'cpu': changed['studies'][2]['processes'][0]['observer']['events'][0]['startTicks'] += 1
        if kind == 'union': changed['studies'][4]['processes'][0]['trustInputs']['common']['derSha256'].pop()
        if kind == 'managed': changed['bdn']['cases'][0]['meanNs'] += 1
        try:
            validate(changed, events)
        except ValueError:
            rejected += 1
        else:
            raise ValueError('mutation survived: '+kind)
    print(json.dumps({'status': 'PASS', **result, 'rejectedMutations': rejected, 'runtimeOperations': 0}, sort_keys=True))


if __name__ == '__main__':
    main()
