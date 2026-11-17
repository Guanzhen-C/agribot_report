"""Read-only hardware/software preflight; never flash, configure CAN or authorize motion."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time


def main():
    root = Path(sys.argv[1]).resolve()
    output = Path(sys.argv[2]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, str(root/'tools/hil'))
    from backend import RuntimeBackend
    from catalog import profile, firmware
    from config import load, ssh_arguments

    config = load()
    backend = RuntimeBackend(config, output, lambda *args, **kwargs: None)
    report = {'hardware_outputs': False, 'at':time.time(), 'host':os.uname().nodename,
              'git_head':subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip(),
              'preflight':[], 'sources':{}}
    for name in ['ackermann_apple','tracked_sprayer','tracked_shredder','tractor_powershift','tractor_cvt']:
        try:
            backend.preflight(profile(name), firmware(profile(name)['firmware']))
            report['preflight'].append({'profile':name,'ready':True})
        except Exception as error:
            report['preflight'].append({'profile':name,'ready':False,'error':str(error)})
    try:
        backend.verify_real_runtime()
        report['protected_real_files_unchanged'] = True
    except Exception as error:
        report['protected_real_files_unchanged'] = False
        report['protection_error'] = str(error)
    for folder in ['tools/hil','ros2_ws/src/agribot_hil_apple','ros2_ws/src/agribot_hil_hitch']:
        for path in sorted((root/folder).rglob('*')):
            if path.is_file() and path.suffix in ('.py','.cpp','.c','.h','.xml','.yaml') and '__pycache__' not in path.parts:
                report['sources'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
    (output/'preflight.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='sources'},ensure_ascii=False,indent=2))
    if not all(row['ready'] for row in report['preflight']) or not report['protected_real_files_unchanged']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
