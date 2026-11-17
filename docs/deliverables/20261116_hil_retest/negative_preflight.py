"""Exercise rejection paths without changing or interrupting the active HIL run."""
import argparse
import hashlib
import json
from pathlib import Path
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    sys.path.insert(0,str(root/'tools/hil'))
    from backend import RuntimeBackend
    from catalog import profile, firmware
    from cli import queue_start
    from config import load
    args.output.mkdir(parents=True,exist_ok=False)
    state = root/'.local/hil/state.json'
    before = hashlib.sha256(state.read_bytes()).hexdigest()
    records = []
    selected = profile('tracked_shredder')
    for name, call, expected in [
        ('unsupported_profile',lambda:queue_start('not-a-supported-hil-profile',selected['algorithm'],['Radar','RTK','Camera','Imu']),'尚未开放HIL'),
        ('mismatched_algorithm',lambda:queue_start(selected['id'],'not-the-frozen-algorithm',['Radar','RTK','Camera','Imu']),'默认算法'),
        ('missing_sensor',lambda:queue_start(selected['id'],selected['algorithm'],['Radar','RTK','Imu']),'请先安装'),
    ]:
        try:
            call()
        except ValueError as error:
            records.append(dict(name=name,passed=expected in str(error),reason=str(error)))
        else:
            records.append(dict(name=name,passed=False,reason='Unexpectedly accepted'))
    config = dict(load(),probe_serial='deliberately-unbound-probe')
    try:
        RuntimeBackend(config,args.output/'identity',lambda *_args,**_kwargs:None).preflight(selected,firmware(selected['firmware']))
    except RuntimeError as error:
        records.append(dict(name='unbound_physical_probe',passed='绑定的6750烧录器' in str(error),reason=str(error)))
    else:
        records.append(dict(name='unbound_physical_probe',passed=False,reason='Unexpectedly accepted'))
    report = dict(scope='Actual validation code and USB identity read; no flash/CAN/authorization',
                  cases=records,active_session_unchanged=before==hashlib.sha256(state.read_bytes()).hexdigest())
    report['passed'] = all(x['passed'] for x in records) and report['active_session_unchanged']
    (args.output/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))
    if not report['passed']: raise SystemExit(1)


if __name__=='__main__':
    main()
