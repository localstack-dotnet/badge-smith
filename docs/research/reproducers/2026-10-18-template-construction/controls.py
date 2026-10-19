"""Public adaptation controls, not additional Native timing/HTTP samples. Stdlib, fake streams only."""
import copy
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
from types import SimpleNamespace
import sys
import threading
import tempfile
import time
import unittest
from unittest.mock import patch
import zipfile


def deny(event, _args):
    if event.startswith(('socket.','subprocess.','os.system','os.posix_spawn')):
        raise RuntimeError('offline control forbids process/network: '+event)


sys.addaudithook(deny)
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('historical_runtime_adapter',Path(__file__).with_name('runtime_adapter.py'))
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)


def payload(case):
    headers={'content-type':'application/json; charset=utf-8','access-control-allow-origin':'*'}
    if case=='Health':
        headers['cache-control']='no-store'
        body=json.dumps({'status':'Healthy','time_stamp':'2026-10-18T19:00:00Z'})
        status=200
    else:
        headers|={'etag':a.TAG,'cache-control':a.CACHE,'last-modified':'Fri, 16 Oct 2026 12:00:00 GMT'}
        status=304 if case in ('Quoted','WeakList') else 200
        body=None if status==304 else a.BODY
    return json.dumps({'statusCode':status,'headers':headers,'body':body,'isBase64Encoded':False}).encode()


def state(kind='health'):
    cases,roles=a.cohort(kind)
    return {'ready':None,'sent':None,'cases':cases,'roles':roles,'results':[],'failures':[],
            'pending':False,'finished':threading.Event(),'stop':threading.Event(),'upstream':0,
            'upstreamStatuses':{'200':0,'304':0},'runtimeGets':0,'runtimePostStarts':0,'runtimePostBodies':0,
            'runtimePostAcks':0,'t0':time.monotonic_ns()}


def handler(s,path,data=b'',reader=None):
    h=object.__new__(a.Runtime)
    h.server=SimpleNamespace(state=s);h.path=path
    h.headers={'Content-Length':str(len(data))};h.rfile=reader or io.BytesIO(data)
    h.reply=lambda *args,**kwargs:None
    return h


class DelayedFinalByte:
    def __init__(self,data):self.data=data;self.reads=0;self.finishedNs=None;self.finishedUtcNs=None
    def read(self,size):
        self.reads+=1
        if self.reads==1:return self.data[:-1]
        until=time.monotonic_ns()+1000000
        while time.monotonic_ns()<until:pass
        self.finishedNs=time.monotonic_ns();self.finishedUtcNs=time.time_ns()
        return self.data[-1:]


class Boundaries(unittest.TestCase):
    def test_81_actual_runtime_and_upstream_handlers(self):
        s=state('main')
        for i,case in enumerate(s['cases']):
            h=handler(s,'/2018-06-01/runtime/invocation/next');h.do_GET()
            u=object.__new__(a.Upstream);u.server=SimpleNamespace(state=s)
            u.path='/v3-flatcontainer/badgesmith-fixture/index.json'
            u.headers={} if i==0 else {'If-None-Match':'"fixture-v1"','If-Modified-Since':'Fri, 16 Oct 2026 12:00:00 GMT'}
            u.reply=lambda *args,**kwargs:None;u.do_GET()
            handler(s,f'/2018-06-01/runtime/invocation/local-{i}/response',payload(case)).do_POST()
        self.assertEqual(s['failures'],[])
        self.assertEqual(len(s['results']),81)
        self.assertEqual(s['upstreamStatuses'],{'200':1,'304':80})
        self.assertEqual([r['role'] for r in s['results']],a.cohort('main')[1])

    def test_wrong_id_and_error_endpoints_before_body(self):
        class Poison:
            def read(self,*_):raise AssertionError('poison body read')
        for path in ['/2018-06-01/runtime/invocation/local-999/response',
                     '/2018-06-01/runtime/invocation/local-0/error','/2018-06-01/runtime/init/error']:
            s=state();s['pending']=True
            handler(s,path,b'x',Poison()).do_POST()
            self.assertEqual(s['runtimePostBodies'],0)
            self.assertEqual(s['failures'],['runtime_response_failure'])

    def test_complete_body_clocks_before_contract(self):
        s=state();s.update(pending=True,sent=time.monotonic_ns(),sentUtcNs=time.time_ns())
        data=payload('Health');reader=DelayedFinalByte(data)
        original=a.response_contract
        def contract(*args):
            self.assertIsNotNone(reader.finishedNs)
            return original(*args)
        with patch.object(a,'response_contract',contract):
            handler(s,'/2018-06-01/runtime/invocation/local-0/response',data,reader).do_POST()
        self.assertEqual(s['failures'],[])
        self.assertGreaterEqual(s['results'][0]['completeBodyNs'],reader.finishedNs)
        self.assertGreaterEqual(s['results'][0]['completeUtcNs'],reader.finishedUtcNs)

    def test_header_schema_status_encoding_drift(self):
        for case in ['Health',*a.CASES]:
            golden=json.loads(payload(case));self.assertIn(a.response_contract(payload(case),case),(200,304))
            for mutate in [lambda p:p['headers'].pop('access-control-allow-origin'),
                           lambda p:p['headers'].update({'extra':'bad'}),lambda p:p.update(statusCode=500),
                           lambda p:p.update(isBase64Encoded=True),lambda p:p.update(cookies=['bad'])]:
                changed=copy.deepcopy(golden);mutate(changed)
                with self.assertRaises(a.Rejected):a.response_contract(json.dumps(changed).encode(),case)

    def test_revalidation_path_validators_and_caps(self):
        for path,headers in [('/bad',{}),('/v3-flatcontainer/badgesmith-fixture/index.json',{})]:
            s=state();s['upstream']=1
            u=object.__new__(a.Upstream);u.server=SimpleNamespace(state=s);u.path=path;u.headers=headers
            u.reply=lambda *args,**kwargs:None;u.do_GET()
            self.assertEqual(s['failures'],['upstream_protocol_failure'])
        for args in [(101,1,0,1,0,1),(1,1,65537,1,0,1),(1,1,0,30000000001,0,1),(1,0,0,1,0,1)]:
            with self.assertRaises(a.Rejected):a.validate_limits(*args)
        for uri in ['https://example.invalid/','http://127.0.0.1:1/','https://127.0.0.1:1/path']:
            with self.assertRaises(a.Rejected):a.validate_uri(uri)
        with self.assertRaises(a.Rejected):a.validate_environment({'AWS_PROFILE':'forbidden'},a.RUNTIME_ENV)

    def test_marker_and_cohort_rejections(self):
        self.assertEqual(a.parse_markers(b'NP|1|1|4152|4152|7\n'),[[1,1,4152,4152,7]])
        for raw in [b'bad\n',b'NP|1|1|1|1|1',b'x'*65537]:
            with self.assertRaises(a.Rejected):a.parse_markers(raw)
        with self.assertRaises(a.Rejected):a.validate_plan(['NoHeader'],['warm'])

    def test_mutation_controls_are_assertion_sensitive(self):
        with patch.object(a,'validate_protocol',lambda *_:True):
            result=unittest.TestResult();Boundaries('test_wrong_id_and_error_endpoints_before_body').run(result)
        self.assertEqual(len(result.failures),1);self.assertEqual(result.errors,[])
        self.assertIn('poison body read',result.failures[0][1])
        original=a.read_complete
        def early(stream,size):
            before=time.monotonic_ns();data,_=original(stream,size);return data,before
        with patch.object(a,'read_complete',early):
            result=unittest.TestResult();Boundaries('test_complete_body_clocks_before_contract').run(result)
        self.assertEqual(len(result.failures),1);self.assertEqual(result.errors,[])
        self.assertIn('not greater than or equal',result.failures[0][1])


_source_watch=None
guard_results=[]


def watch_source_reads(event,args):
    if event!='open' or _source_watch is None or isinstance(args[0],int):return
    if args[2]&(os.O_WRONLY|os.O_RDWR|os.O_CREAT|os.O_TRUNC|os.O_APPEND):return
    path=Path(args[0]).absolute()
    if any(root==path or root in path.parents for root in _source_watch['roots']):
        _source_watch['reads'].append(str(path))
        if path not in _source_watch['allowed']:
            _source_watch['unexpected']+=1
            raise AssertionError('unexpected source bytes opened')


sys.addaudithook(watch_source_reads)
input_spec=importlib.util.spec_from_file_location('historical_input_controls',Path(__file__).with_name('inputs.py'))
inputs=importlib.util.module_from_spec(input_spec);input_spec.loader.exec_module(inputs)


class SourceRoots(unittest.TestCase):
    """Synthetic declarations/archive are guard fixtures, never compiled or executed study artifacts."""
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='template-source-guards-')
        self.addCleanup(self.temp.cleanup)
        self.work=Path(self.temp.name);self.root=self.work/'source';self.root.mkdir()
        data=b'// synthetic source guard fixture, never compiled\n'
        (self.root/'Fixture.cs').write_bytes(data);(self.root/'Fixture.cs').chmod(0o644)
        self.files={'Fixture.cs':{'sha256':hashlib.sha256(data).hexdigest(),'size':len(data),'mode':'0o644'}}
        elf=bytearray(64);elf[:6]=b'\x7fELF\x02\x01';elf[18:20]=b'\x3e\x00'
        self.exe=self.work/'bootstrap';self.exe.write_bytes(elf);self.exe.chmod(0o755)
        self.archive=self.work/'synthetic-guard.zip'
        entry=zipfile.ZipInfo('bootstrap');entry.create_system=3;entry.external_attr=0o100755<<16
        entry.compress_type=zipfile.ZIP_DEFLATED
        with zipfile.ZipFile(self.archive,'x') as archive:archive.writestr(entry,elf)
        self.artifact=a.inspect_zip(self.archive);self.counter=0

    def arguments(self,operation,root,files=None):
        self.counter+=1;prefix=self.work/str(self.counter);prefix.mkdir()
        files=self.files if files is None else files
        source={'status':'PASS','variant':'A','frozenRoot':str(root),'files':files}
        build={'status':'PASS','buildId':'synthetic-guard-not-actual-build','sourceRoot':str(root),
               'sourceFiles':files,'sourceReceiptSha256':a.canonical(source),'artifact':self.artifact}
        for name,value in [('source',source),('build',build),('packet',{'sources':{'A':{'files':files}}})]:
            (prefix/(name+'.json')).write_text(json.dumps(value))
        if operation=='prepare':
            return a.prepare,a.Rejected,SimpleNamespace(source_receipt=str(prefix/'source.json'),
                build_receipt=str(prefix/'build.json'),zip=str(self.archive),executable=str(self.exe),
                out=str(prefix/'output'),cohort='main',role='synthetic-offline-guard',settings_json=None)
        return inputs.snapshot,inputs.a.Rejected,SimpleNamespace(packet=str(prefix/'packet.json'),variant='A',
            input_root=str(root),out=str(prefix/'output'),receipt=str(prefix/'snapshot.json'))

    def accepted(self,operation,root):
        invoke,_,args=self.arguments(operation,root)
        with contextlib.redirect_stdout(io.StringIO()):invoke(args)
        self.assertTrue(Path(args.out).is_dir())
        guard_results.append({'test':self._testMethodName,'operation':operation,'accepted':True})

    def rejected(self,operation,root,reason,files=None):
        global _source_watch
        invoke,rejection,args=self.arguments(operation,root,files)
        self.assertFalse(Path(args.out).exists())
        watch={'roots':[self.root,Path(root).absolute()],
               'allowed':{self.root/'Fixture.cs',Path(root).absolute()/'Fixture.cs'},'reads':[],'unexpected':0}
        _source_watch=watch
        try:
            with self.assertRaisesRegex(rejection,'^'+reason+'$'):
                with contextlib.redirect_stdout(io.StringIO()):invoke(args)
        finally:_source_watch=None
        self.assertFalse(Path(args.out).exists())
        if operation=='snapshot':self.assertFalse(Path(args.receipt).exists())
        self.assertEqual(watch['reads'],[], 'metadata rejection must precede all source-content reads')
        self.assertEqual(watch['unexpected'],0)
        guard_results.append({'test':self._testMethodName,'operation':operation,'accepted':False,'reason':reason,
                              'sourceByteReads':len(watch['reads']),'unlistedByteReads':watch['unexpected'],
                              'outputCreated':False})

    def test_extra_regular_file_rejects_actual_prepare_and_snapshot_before_reads(self):
        for operation in ('prepare','snapshot'):self.accepted(operation,self.root)
        (self.root/'Extra.cs').write_bytes(b'// unlisted guard fixture\n')
        for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')

    def test_pristine_root_and_ancestor_links_reject_for_lexical_guard(self):
        # No extra file: inventory-only rejection must not mask an unfixed lexical guard.
        for operation in ('prepare','snapshot'):self.accepted(operation,self.root)
        link=self.work/'clean-root-link';link.symlink_to(self.root,target_is_directory=True)
        parent=self.work/'clean-parent-link';parent.symlink_to(self.work,target_is_directory=True)
        for root in (link,parent/'source'):
            for operation in ('snapshot','prepare'):self.rejected(operation,root,'source_root_lexical')

    def test_hidden_empty_config_and_unlisted_empty_directories_reject_before_reads(self):
        for name,data in [('unknown.props',b'config guard fixture'),('.hidden',b'guard fixture'),('empty-extra',b'')]:
            with self.subTest(name=name):
                path=self.root/name;path.write_bytes(data)
                for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')
                path.unlink()
        for name in ('empty-directory','bin','obj'):
            with self.subTest(name=name):
                path=self.root/name;path.mkdir()
                for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')
                path.rmdir()

    def test_unlisted_links_and_fifo_reject_without_opening_their_contents(self):
        for name,target in [('file-link',self.exe),('directory-link',self.work),('broken-link',self.work/'absent')]:
            with self.subTest(name=name):
                path=self.root/name;path.symlink_to(target)
                for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')
                path.unlink()
        path=self.root/'unlisted-fifo';os.mkfifo(path,0o600)
        for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')
        path.unlink()

    def test_declared_nonregular_entries_reject_before_source_reads(self):
        named=self.root/'Fixture.cs';retained=self.work/'retained-fixture.cs';named.rename(retained)
        for kind in ('file-link','broken-link','fifo'):
            with self.subTest(kind=kind):
                if kind=='fifo':os.mkfifo(named,0o600)
                else:named.symlink_to(retained if kind=='file-link' else self.work/'absent')
                for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_entry_type')
                named.unlink()
        retained.rename(named)
        nested=self.work/'nested-linked-source';nested.mkdir()
        (nested/'sub').symlink_to(self.root,target_is_directory=True)
        for operation in ('prepare','snapshot'):
            self.rejected(operation,nested,'source_entry_type',{'sub/Fixture.cs':self.files['Fixture.cs']})

    def test_relative_dotdot_and_broken_roots_reject_in_lexical_phase(self):
        broken=self.work/'broken-root';broken.symlink_to(self.work/'absent',target_is_directory=True)
        for root in (os.path.relpath(self.root),str(self.root)+'/../source',str(self.root)+'\n',broken):
            with self.subTest(root=str(root)):
                for operation in ('prepare','snapshot'):self.rejected(operation,root,'source_root_lexical')

    def test_malformed_manifest_paths_reject_before_traversal(self):
        for name in ('','/absolute.cs','../Fixture.cs','./Fixture.cs','sub//Fixture.cs',
                     'sub/../Fixture.cs','sub/./Fixture.cs','Fixture\n.cs','Fixture\x00.cs'):
            with self.subTest(name=name):
                files={name:self.files['Fixture.cs']}
                for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_relative_path',files)

    def test_nested_manifest_prefix_directories_remain_valid_but_extra_directory_rejects(self):
        sub=self.root/'sub';sub.mkdir()
        (sub/'Copy.cs').write_bytes((self.root/'Fixture.cs').read_bytes());(sub/'Copy.cs').chmod(0o644)
        self.files['sub/Copy.cs']=dict(self.files['Fixture.cs'])
        for operation in ('prepare','snapshot'):self.accepted(operation,self.root)
        (sub/'unlisted-empty').mkdir()
        for operation in ('prepare','snapshot'):self.rejected(operation,self.root,'source_inventory')


if __name__=='__main__':unittest.main(verbosity=2)
