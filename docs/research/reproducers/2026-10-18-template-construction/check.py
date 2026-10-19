"""Offline historical arithmetic/checksums. No processes, listeners, builds, AWS or downloads."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import statistics
import sys

HERE = Path(__file__).resolve().parent
PACKET = HERE.parents[1] / 'baselines/2026-10-18-template-construction.json'


def need(condition, reason):
    if not condition:
        raise ValueError(reason)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def canonical(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def metric(process, invocations, field):
    if field in ('VmRSS', 'VmHWM'):
        return process['memoryKiB'][field]
    if field == 'firstFeatureNs':
        return next(r['elapsedNs'] for r in invocations if r['role'] == 'first-feature')
    if field == 'warmMedianNs':
        return statistics.median(r['elapsedNs'] for r in invocations if r['role'] in ('warm', 'warm-control'))
    return process[field]


def paired(processes, group):
    rows = [p for p in processes if p['group'] == group]
    numbers = sorted({p['pair'] for p in rows})
    need(len(rows) == len(numbers) * 2, 'pair inventory: ' + group)
    pairs = []
    for number in numbers:
        a, b = [next(p for p in rows if p['pair'] == number and p['slot'] == slot) for slot in ('A', 'B')]
        need(a['order'] != b['order'], 'duplicate pair order')
        earlier, later = sorted((a, b), key=lambda p:p['order'])
        need(earlier['closeAnchorNs'] < later['t0Ns'] and earlier['closeUtcNs'] < later['t0UtcNs'], 'pair overlap')
        expected_a_first = (number % 2 == 1) != group.endswith('-2')
        need((a['order'] == 1) == expected_a_first, 'AB/BA schedule')
        pairs.append((a, b))
    return pairs


def diag(process, field):
    markers = process['markers']
    if field in ('firstMatchBytes', 'reuseMatchBytes'):
        return sum(r[2] for r in markers if r[0] == (3 if field == 'firstMatchBytes' else 4))
    kind, ordinal, column = {'routeBytes':(1, 1, 2), 'startupBytes':(2, 0, 2),
                            'globalCumulativeBytes':(7, 6, 3)}[field]
    return next(r[column] for r in markers if r[:2] == [kind, ordinal])


def check(packet=PACKET):
    packet = Path(packet)
    data = json.loads(packet.read_bytes())
    need(data['schema'] == 'template-construction-historical/v1', 'schema')
    need(canonical(data['frozenAcceptance']) == data['frozenAcceptanceProjectionSha256'], 'acceptance projection hash')
    need(data['originalAcceptanceSha256']==data['adjudication']['planSha256'],'original frozen acceptance identity')
    for name, expected in data['publicFiles'].items():
        need(Path(name).name == name, 'unsafe public file path')
        need(digest(HERE/name) == expected, 'public file hash: ' + name)
    descriptor = data['invocations']
    need(Path(descriptor['path']).name == descriptor['path'], 'unsafe CSV path')
    csv_path = packet.parent / descriptor['path']
    need(digest(csv_path) == descriptor['sha256'], 'CSV checksum')
    with csv_path.open(newline='') as f:
        rows = list(csv.DictReader(f))
    for row in rows:
        for key in row:
            if key not in ('processId', 'case', 'role', 'contractPass'):
                row[key] = int(row[key])
        need(row['contractPass'] == 'True', 'response contract')
    processes = data['processes']
    need(len(processes) == len({p['processId'] for p in processes}) == 100, '100 unique processes')
    need(len(rows) == descriptor['rows'] == 4200, '4200 responses')
    by = {p['processId']:[r for r in rows if r['processId'] == p['processId']] for p in processes}
    need(sum(map(len, by.values())) == len(rows), 'unknown process row')
    expected_groups={'cal-main-AA':16,'cal-health-AA':8,'cal-route-DA':10,'formal-main-1':16,'formal-main-2':16,
                     'formal-health':8,'formal-route-1':10,'formal-route-2':10,'settings':5,'ABI':1}
    need({name:sum(p['group']==name for p in processes) for name in {p['group'] for p in processes}} == expected_groups, 'role accounting')
    for variant, source in data['sources'].items():
        need(canonical(source['files']) == source['fileMapProjectionSha256'], 'public source-file map SHA')
        code={n:r['sha256'] for n,r in source['files'].items()
              if n.startswith(('src/BadgeSmith.Api/','src/shared/')) and n.endswith('.cs')}
        need(canonical(code)==data['lambdaCodeSourceIds'][variant], 'Lambda source-code projection SHA')
    for public,variant,source in [('source-probe.cs.txt','H2DA','Core/Observability/H2Probe.cs'),
                                  ('program-source-probe.cs.txt','H2DA','Program.cs'),
                                  ('route-table-source-probe.cs.txt','H2DA','Core/Routing/RouteTable.cs'),
                                  ('settings-program-source-probe.cs.txt','H2Settings','Program.cs')]:
        need(digest(HERE/public)==data['sources'][variant]['files']['src/BadgeSmith.Api/'+source]['sha256'],'exact source-only probe bytes')
    need(data['selectedSdk']['selectedSdk']=='10.0.402' and data['selectedSdk']['status']=='PASS', 'selected SDK')
    need(data['settingsOracle']['evaluatedAssemblyVersion']=='1.0.0.0', 'independent version oracle')
    for name,h in data['policyHashes'].items():
        need(all(s['files'][name]['sha256']==h for s in data['sources'].values()), 'unchanged policy hashes')
    upstream_total = upstream_200 = upstream_304 = 0
    for p in processes:
        r = by[p['processId']]
        need(p['status'] == 'PASS' and p['failuresCount'] == 0, 'launch status')
        need(p['responseCount'] == len(r) and [v['index'] for v in r] == list(range(len(r))), 'response sequence')
        need(len(r) == (81 if p['cohort'] == 'main' else 6), 'cohort event count')
        expected_roles = (['first-feature'] + ['warmup']*16 + ['warm']*64 if p['cohort'] == 'main'
                          else ['first-health', 'first-feature'] + ['warm-control']*4)
        need([v['role'] for v in r] == expected_roles, 'invocation roles')
        expected_cases = (['NoHeader'] + ['NoHeader','Quoted','WeakList','Miss']*20 if p['cohort'] == 'main'
                          else ['Health','NoHeader','NoHeader','Quoted','WeakList','Miss'])
        need([v['case'] for v in r] == expected_cases, 'invocation cases')
        cumulative = 0
        for i, v in enumerate(r):
            cumulative += v['case'] != 'Health'
            need(v['upstreamCumulative'] == cumulative, 'every-event revalidation')
            need(v['status'] == (304 if v['case'] in ('Quoted','WeakList') else 200), 'downstream status')
            need(v['elapsedNs'] == v['completeBodyNs'] - v['sentNs'] > 0, 'body-complete interval')
            need(p['t0Ns'] < p['readyAnchorNs'] <= v['sentNs'] < v['completeBodyNs'] < p['closeAnchorNs'], 'monotonic anchors')
            need(p['t0UtcNs'] <= p['readyUtcNs'] <= v['sentUtcNs'] <= v['completeUtcNs'] <= p['closeUtcNs'], 'UTC anchors')
            if i:
                need(r[i-1]['completeBodyNs'] < v['sentNs'], 'invocation overlap')
        need(p['readyNs'] == p['readyAnchorNs'] - p['t0Ns'], 'readiness identity')
        need(p['firstInvokeNs'] == r[0]['elapsedNs'], 'first identity')
        need(p['dispatchGapNs'] == r[0]['sentNs'] - p['readyAnchorNs'], 'dispatch gap identity')
        need(p['pairedReadyFirstNs'] == p['readyNs'] + p['firstInvokeNs'], 'same-process paired identity')
        need(p['totalFirstCompletionNs'] == p['pairedReadyFirstNs'] + p['dispatchGapNs'] == r[0]['completeBodyNs']-p['t0Ns'], 'total identity')
        need(p['sequenceCompletionNs'] == r[-1]['completeBodyNs'] - p['t0Ns'], 'sequence identity')
        warm = [v['elapsedNs'] for v in r if v['role'] in ('warm','warm-control')]
        need(p['warmStatsNs'] == {'count':len(warm),'median':statistics.median(warm),'min':min(warm),'max':max(warm)}, 'warm statistics')
        need(p['upstreamCalls'] == cumulative and p['upstreamStatuses'] == {'200':1,'304':cumulative-1}, 'upstream counts')
        upstream_total += cumulative; upstream_200 += 1; upstream_304 += cumulative-1
        need(all(p['cleanup'].values()) and p['nativeReaped'] and p['fixtureThreadsReaped'], 'cleanup')
        identity = p['pidIdentity']
        need(identity['alive'] and identity['notDeleted'] and identity['startTicks'] == p['initialStartTicks'], 'live native identity')
        need(identity['exeSha256'] == p['artifact']['elfSha256'], 'PID/executable SHA')
        build = next(b for b in data['builds'].values() if b.get('buildId') == p['buildId'])
        need(build['artifact'] == p['artifact'] and build['originalReceiptSha256'] == p['buildReceiptSha256'], 'build/artifact SHA')
        need(p['sourceManifestSha256'] == data['sources'][p['variant']]['originalCanonicalReceiptSha256'], 'source manifest SHA')
        if p['variant'] in ('B','H2DB'):
            need(p['t0Ns'] > data['frozenAcceptance']['monotonicNs'] and p['t0UtcNs'] > data['frozenAcceptance']['utcNs'], 'frozen before B')
        if p['group']=='settings':
            need(p['settingsPasscode']==1 and p['timingEligible'] is False,'Settings excluded from timings')
    need((upstream_total,upstream_200,upstream_304) == (4148,100,4048), 'all launch upstream totals')
    frozen = data['frozenAcceptance']
    cal = paired(processes,'cal-route-DA')
    need(len(cal)==5, '5 DA/DA pairs')
    spread = max(abs(diag(a,'routeBytes')-diag(b,'routeBytes')) for a,b in cal)
    floor = max(256,2*spread)
    baseline = statistics.median(diag(p,'routeBytes') for pair in cal for p in pair)
    need(floor == frozen['allocationFloorBytes'] == 256 and baseline == 4152, 'allocation calibration')
    need(baseline-floor == frozen['allocationBudgetBytes'], 'native budget')
    for prefix, bounds in [('main',frozen['mainBounds']),('health',frozen['healthBounds'])]:
        calibration = paired(processes,'cal-'+prefix+'-AA')
        need(len(calibration)==(8 if prefix=='main' else 4), 'AA cohort size')
        for a,b in calibration:
            need(a['closeAnchorNs'] < frozen['monotonicNs'] and b['closeUtcNs'] < frozen['utcNs'], 'AA before freeze')
        for field, bound in bounds.items():
            values = [metric(p,by[p['processId']],field) for pair in calibration for p in pair]
            spread = max(abs(metric(b,by[b['processId']],field)-metric(a,by[a['processId']],field)) for a,b in calibration)
            median = statistics.median(values)
            margin = max(512 if field in ('VmRSS','VmHWM') else median*.05,2*spread)
            need(bound == {'absolutePairedSpread':spread,'baselineMedian':median,'margin':margin}, 'frozen AA margin: '+field)
    need(frozen['latencyClaimFloorNs'] == max(1000000,.05*frozen['mainBounds']['totalFirstCompletionNs']['baselineMedian'],
                                           2*frozen['mainBounds']['totalFirstCompletionNs']['absolutePairedSpread']), 'latency claim floor')
    for block in (1,2):
        diagnostic = paired(processes,'formal-route-'+str(block))
        need(len(diagnostic)==5, 'formal diagnostic size')
        savings = [diag(a,'routeBytes')-diag(b,'routeBytes') for a,b in diagnostic]
        need(savings == [1632]*5 and min(savings)>0 and statistics.median(savings)>=floor, 'primary savings')
        for a,b in diagnostic:
            need(diag(a,'routeBytes')>frozen['allocationBudgetBytes']>=diag(b,'routeBytes'), 'native budget RED/GREEN')
            for p in (a,b):
                need(len(p['markers'])==28, '28 diagnostic records')
                need(all(r[2]==0 for r in p['markers'] if r[0] in (0,3,4,5)), 'empty/idle/match controls')
        for field in ('startupBytes','firstMatchBytes','reuseMatchBytes'):
            need(statistics.median(diag(b,field)-diag(a,field) for a,b in diagnostic)<=0, 'synchronous MEDIAN gate')
        observed=data['summary']['diagnostic'][block-1]
        for slot in ('A','B'):
            for field in ('routeBytes','startupBytes','firstMatchBytes','reuseMatchBytes','globalCumulativeBytes'):
                values=[diag(pair[0 if slot=='A' else 1],field) for pair in diagnostic]
                need(observed[slot][field]=={'values':values,'min':min(values),'median':statistics.median(values),'max':max(values)}, 'diagnostic full values/ranges')
        for field, bound in frozen['mainBounds'].items():
            pairs = paired(processes,'formal-main-'+str(block)); need(len(pairs)==8,'main block size')
            deltas = [metric(b,by[b['processId']],field)-metric(a,by[a['processId']],field) for a,b in pairs]
            expected = data['summary']['main'][block-1][field]
            need(deltas==expected['values'] and min(deltas)==expected['min'] and max(deltas)==expected['max'] and
                 statistics.median(deltas)==expected['median']<=bound['margin'], 'formal main MEDIAN: '+field)
    for field, bound in frozen['healthBounds'].items():
        pairs=paired(processes,'formal-health'); need(len(pairs)==4,'formal health size')
        deltas=[metric(b,by[b['processId']],field)-metric(a,by[a['processId']],field) for a,b in pairs]
        need(deltas == data['summary']['health'][field]['values'] and statistics.median(deltas)<=bound['margin'], 'health MEDIAN: '+field)
    main=[p for p in processes if p['group'].startswith('formal-main-')]
    need(len(main)==32 and sum(p['responseCount'] for p in main)==2592 and sum(p['upstreamCalls'] for p in main)==2592, 'formal main accounting')
    h1=data['h1Rejected']
    need(statistics.median(h1['factorySavingsBytes'])==83592 and min(h1['completeGlobalSavingsBytes'])==-36360, 'H1 benefit/direction')
    need(h1['candidateFeatureGlobalMedians'][0]==104248>h1['frozenFeatureCeilings'][0]==93144, 'H1 rejection preserved')
    need(max(256,2*(max(h1['calibrationFactoryBytes'])-min(h1['calibrationFactoryBytes'])))==h1['floorBytes']==44304,'H1 original floor')
    return {'status':'PASS','processes':100,'responses':4200,'upstream':4148,'formalMainProcesses':32,
            'formalMainResponses':2592,'allocationSavingBytes':1632,'allocationFloorBytes':floor,
            'qualification':'No frozen median-gate regression detected; no demonstrated latency or RSS improvement.',
            'publicJsonSha256':digest(packet),'finalPackageGate':'PENDING','publication':'PENDING'}


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packet',type=Path,default=PACKET)
    args=parser.parse_args()
    try:
        print(json.dumps(check(args.packet),indent=2))
    except (ValueError,KeyError,StopIteration,OSError) as error:
        print(json.dumps({'status':'FAIL','reason':str(error)}));sys.exit(1)
