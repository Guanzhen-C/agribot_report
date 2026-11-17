"""Manually authorized, stopped-bench USB reset of the single bound CAN adapter."""
import argparse
import fcntl
import json
import os
from pathlib import Path
import subprocess
import time


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--rebind',action='store_true',help='Rebind only the verified gs_usb interface instead of USB reset')
    args=parser.parse_args()
    if os.geteuid()!=0: raise PermissionError('USB device reset requires root')
    state=json.loads((args.root/'.local/hil/state.json').read_text())
    if state.get('phase')!='stopped' or state.get('session_alive'):
        raise RuntimeError('Only a fully stopped HIL bench may be reset')
    for unit in ('agribot-app-hil-session.service','agribot-app-hil-pc.service','agribot-app-hil-stop.service'):
        result=subprocess.run(['runuser','-u','casia','--','env','XDG_RUNTIME_DIR=/run/user/1000',
                               'systemctl','--user','is-active',unit],capture_output=True,text=True)
        if result.stdout.strip() in ('active','activating','deactivating'):
            raise RuntimeError('Refusing reset while a HIL unit is active: '+unit)
    interface=Path('/sys/class/net/can0')
    device=(interface/'device').resolve(strict=True)
    usb=next(p for p in (device,*device.parents) if (p/'idVendor').is_file())
    serial=(usb/'serial').read_text().strip()
    if serial!='003000224856511520343739' or (usb/'idVendor').read_text().strip()!='1d50' or \
            (usb/'idProduct').read_text().strip()!='606f' or (device/'driver').resolve().name!='gs_usb':
        raise RuntimeError('Unbound USB-CAN identity; no reset performed')
    node=Path('/dev/bus/usb')/f"{int((usb/'busnum').read_text()):03d}"/f"{int((usb/'devnum').read_text()):03d}"
    subprocess.run(['/usr/sbin/ip','link','set','can0','down'],check=True,timeout=10)
    if args.rebind:
        driver=device/'driver'
        driver=driver.resolve(strict=True)
        interface_name=device.name
        if driver.name!='gs_usb' or ':' not in interface_name:
            raise RuntimeError('Not the verified gs_usb USB interface')
        (driver/'unbind').write_text(interface_name)
        try:
            time.sleep(1.)
        finally:
            (driver/'bind').write_text(interface_name)
    else:
        with node.open('wb',buffering=0) as handle:
            fcntl.ioctl(handle.fileno(),0x5514,0)
    time.sleep(2.)
    result=subprocess.run(['/usr/local/libexec/agribot-hil-bus','pc','250000'],capture_output=True,text=True,timeout=30)
    report=dict(scope='One stopped-bench gs_usb adapter USB reset; no hub/network/probe reset',
                serial=serial,usb_node=str(node),driver_rebound=args.rebind,configuration_returncode=result.returncode,
                configuration_output=result.stdout+result.stderr)
    with args.output.open('x') as stream: json.dump(report,stream,indent=2)
    print(json.dumps(report,indent=2))
    if result.returncode: raise SystemExit(result.returncode)


if __name__=='__main__':
    main()
