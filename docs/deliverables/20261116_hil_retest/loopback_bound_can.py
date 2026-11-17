"""Finite adapter-only diagnostic; always restore normal physical CAN mode."""
import argparse
import json
import os
from pathlib import Path
import socket
import struct
import subprocess
import time


def run(*command):
    return subprocess.run(command,check=True,capture_output=True,text=True,timeout=15)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    if os.geteuid()!=0: raise PermissionError('Root required for controlled adapter mode change')
    state=json.loads((args.root/'.local/hil/state.json').read_text())
    if state.get('phase')!='stopped' or state.get('session_alive'):
        raise RuntimeError('Only a stopped HIL bench may be diagnosed')
    for unit in ('agribot-app-hil-session.service','agribot-app-hil-pc.service','agribot-app-hil-stop.service'):
        result=subprocess.run(['runuser','-u','casia','--','env','XDG_RUNTIME_DIR=/run/user/1000',
                               'systemctl','--user','is-active',unit],capture_output=True,text=True)
        if result.stdout.strip() in ('active','activating','deactivating'):
            raise RuntimeError('HIL unit is active: '+unit)
    device=Path('/sys/class/net/can0/device').resolve(strict=True)
    usb=next(p for p in (device,*device.parents) if (p/'idVendor').is_file())
    if (usb/'serial').read_text().strip()!='003000224856511520343739' or \
            (device/'driver').resolve().name!='gs_usb':
        raise RuntimeError('Wrong physical adapter; mode change refused')
    report=dict(scope='PC adapter internal loopback only; NOT end-to-end HIL',sent=0,received=0)
    bus=None
    try:
        run('/usr/sbin/ip','link','set','can0','down')
        run('/usr/sbin/ip','link','set','can0','type','can','bitrate','250000','loopback','on')
        run('/usr/sbin/ip','link','set','can0','up')
        bus=socket.socket(socket.PF_CAN,socket.SOCK_RAW,socket.CAN_RAW)
        bus.setsockopt(socket.SOL_CAN_RAW,socket.CAN_RAW_RECV_OWN_MSGS,1)
        bus.bind(('can0',));bus.settimeout(.05)
        counter=Path('/sys/class/net/can0/statistics/tx_packets')
        before=int(counter.read_text())
        frame=struct.Struct('=IB3x8s')
        for sequence in range(5):
            payload=bytes([0,0,0,0,1,0,sequence,0])
            bus.send(frame.pack(0x631,8,payload));report['sent']+=1
            time.sleep(.1)
        deadline=time.monotonic()+1.
        frames=[]
        while time.monotonic()<deadline:
            try:
                raw=bus.recv(frame.size)
            except socket.timeout:
                continue
            identity,size,data=frame.unpack(raw)
            if identity==0x631:
                report['received']+=1
                frames.append(data[:size].hex())
        report.update(physically_completed=int(counter.read_text())-before,received_payloads=frames)
        report['loopback_passed']=report['physically_completed']==5 and report['received']>=5
    except Exception as error:
        report['error']=str(error)
        report['loopback_passed']=False
    finally:
        if bus: bus.close()
        run('/usr/sbin/ip','link','set','can0','down')
        run('/usr/sbin/ip','link','set','can0','type','can','loopback','off')
        run('/usr/local/libexec/agribot-hil-bus','pc','250000')
        config=json.loads(run('/usr/sbin/ip','-j','-d','link','show','can0').stdout)[0]
        report['restored_interface']=config
        report['normal_mode_restored']='LOOPBACK' not in config['linkinfo']['info_data'].get('ctrlmode',[])
        with args.output.open('x') as stream: json.dump(report,stream,indent=2)
        print(json.dumps(report,indent=2))
        if not report['normal_mode_restored']: raise RuntimeError('Normal physical CAN mode not restored')


if __name__=='__main__':
    main()
