"""Finite JTAG identity diagnostic only: no halt, flash programming or mass erase."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    sys.path.insert(0,str(args.root.resolve()/'tools/hil'))
    from config import load,verify_flash_scripts
    config=load()
    verify_flash_scripts(config)
    env=dict(os.environ,LD_LIBRARY_PATH=config['openocd_library_path']+':'+os.environ.get('LD_LIBRARY_PATH',''))
    command=[config['openocd'],'-s',config['openocd_scripts'],'-f','probes/cmsis_dap.cfg',
             '-c','adapter serial '+config['probe_serial'],'-c','adapter speed 100',
             '-f','soc/hpm6750-single-core.cfg','-c','bindto 127.0.0.1','-c','init','-c','scan_chain','-c','shutdown']
    result=subprocess.run(command,env=env,capture_output=True,text=True,timeout=25)
    output=result.stdout+result.stderr
    identified=(result.returncode==0 and 'tap/device found: 0x1000563d' in output.lower()
                and 'examination failed' not in output.lower() and 'interrogation failed' not in output.lower())
    report=dict(scope='JTAG identity scan at 100 kHz; no firmware write',returncode=result.returncode,
                target_identified=identified,output=output)
    with args.output.open('x') as stream: json.dump(report,stream,indent=2)
    print(json.dumps(report,indent=2))
    if not identified: raise SystemExit(1)


if __name__=='__main__':
    main()
