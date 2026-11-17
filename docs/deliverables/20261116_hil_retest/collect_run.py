"""Collect closed-run evidence via the existing strict SSH configuration."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--run',required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=args.root.resolve()
    if not args.run.replace('_','').isalnum(): raise ValueError('Invalid run id')
    pc=root/'.local/hil/runs'/args.run
    if not pc.is_dir() or not (pc/'firmware.json').is_file():
        raise FileNotFoundError('Actual local run and recorded firmware are required before creating an archive: '+args.run)
    sys.path.insert(0,str(root/'tools/hil'))
    from config import load
    config=load(validate_flash=False)
    args.output.mkdir(parents=True,exist_ok=False)
    local=args.output/'pc'
    local.mkdir()
    for name in ('flash.log','firmware.json','stages.jsonl','operations.log','real_runtime_checks.jsonl',
                 'mission-result.json','pc-can.jsonl','pc-runtime.log','error.log'):
        if (pc/name).is_file(): shutil.copy2(pc/name,local/name)
    ssh=['ssh','-i',config['identity'],'-o','IdentitiesOnly=yes','-o',
         'UserKnownHostsFile='+config['known_hosts'],'-o','StrictHostKeyChecking=yes']
    remote=config['host']+':'+config['remote_root']+'/runs/'+args.run+'/'
    import shlex
    subprocess.run(['rsync','-az','--include=*/','--include=*.json','--include=*.jsonl','--include=*.log',
                    '--exclude=*','-e',shlex.join(ssh),remote,str(args.output/'x5')+'/'],check=True)
    manifest={str(p.relative_to(args.output)):dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size)
              for p in args.output.rglob('*') if p.is_file()}
    (args.output/'manifest.json').write_text(json.dumps(dict(run_id=args.run,files=manifest),indent=2)+'\n')
    print(json.dumps(dict(run_id=args.run,files=len(manifest))))


if __name__=='__main__':
    main()
