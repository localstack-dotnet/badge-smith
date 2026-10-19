"""Historical input preparation only: manifest copy and artifact binding; never builds or launches."""
import argparse
import importlib.util
import json
from pathlib import Path
import shutil
import stat

spec=importlib.util.spec_from_file_location('adapter',Path(__file__).with_name('runtime_adapter.py'))
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)


def snapshot(args):
    packet=json.loads(Path(args.packet).read_bytes())
    expected=packet['sources'][args.variant]['files']
    out=Path(args.out)
    a.need(out.is_absolute() and not out.exists(),'new_absolute_snapshot')
    source=a.validate_source_root(args.input_root,expected)
    out.mkdir(mode=0o700)
    for name,row in expected.items():
        path=out/name;path.parent.mkdir(parents=True,exist_ok=True)
        with path.open('xb') as f:f.write((source/name).read_bytes())
        path.chmod(int(row['mode'],8));a.need(a.sha(path)==row['sha256'],'snapshot_copy')
    a.write_new(args.receipt,{'status':'PASS','variant':args.variant,'frozenRoot':str(out),
                            'files':expected,'identity':'new explicit historical-source copy, not original study receipt'})


def bind(args):
    source=json.loads(Path(args.source_receipt).read_bytes())
    record=json.loads(Path(args.build_record).read_bytes())
    sdk=json.loads(Path(args.sdk_receipt).read_bytes())
    a.need(record['Status']=='completed' and record['Target']=='export-zip' and record['Platform']==['linux/amd64'] and
           record['Context']==source['frozenRoot'] and record['BuildArgs']==[{'Name':'RID','Value':'linux-x64'}], 'normal_build_record')
    a.need(sdk['selectedSdk']=='10.0.402' and any(m['Digests']==[sdk['materialDigest']] and 'dotnet/sdk' in m['URI']
           for m in record['Materials']), 'selected_sdk_material')
    # Caller must separately retain the actual normal CLI invocation/diagnostics/chronology. This binder cannot recreate those facts.
    a.write_new(args.receipt,{'status':'PASS','buildId':record['Ref'],'sourceRoot':source['frozenRoot'],
        'sourceFiles':source['files'],'sourceReceiptSha256':a.canonical(source),'artifact':a.inspect_zip(args.zip),
        'buildRecordSha256':a.sha(args.build_record),'sdkReceiptSha256':a.sha(args.sdk_receipt),
        'bindingLimit':'Artifact/record/declared source binding only; not independent verification of how the supplied record was captured.'})


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    commands=parser.add_subparsers(dest='command',required=True)
    p=commands.add_parser('snapshot')
    for name in ['packet','variant','input-root','out','receipt']:p.add_argument('--'+name,required=True)
    p=commands.add_parser('bind')
    for name in ['source-receipt','build-record','sdk-receipt','zip','receipt']:p.add_argument('--'+name,required=True)
    args=parser.parse_args()
    {'snapshot':snapshot,'bind':bind}[args.command](args)
    print(json.dumps({'status':'PASS','nativeExecuted':False,'buildExecuted':False}))


if __name__=='__main__':main()
