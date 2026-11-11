#!/usr/bin/python3 -I
"""Narrow privileged helper: the reviewed bench CAN adapter and virtual HIL buses only."""
import json
import fcntl
import os
from pathlib import Path
import subprocess
import sys
import time

SERIAL = '003000224856511520343739'


def pc_device(sysfs=Path('/sys/class/net')):
    interface = sysfs/'can0'
    device = (interface/'device').resolve(strict=True)
    if (device/'driver').resolve().name != 'gs_usb':
        raise RuntimeError('can0 is not the reviewed gs_usb bench adapter')
    for ancestor in (device, *device.parents):
        if (ancestor/'idVendor').is_file():
            if (ancestor/'serial').read_text().strip() != SERIAL:
                raise RuntimeError('Wrong physical PC USB-CAN serial')
            return interface
    raise RuntimeError('Physical USB-CAN identity is unavailable')


def run(*command):
    subprocess.run(command, check=True, timeout=15)


def configure_pc(bitrate):
    interface = pc_device()
    current = json.loads(subprocess.check_output(['/usr/sbin/ip', '-json', '-details', 'link', 'show', 'can0']))[0]
    actual = current.get('linkinfo', {}).get('info_data', {}).get('bittiming', {}).get('bitrate')
    healthy = current.get('linkinfo', {}).get('info_data', {}).get('state') == 'ERROR-ACTIVE'
    if 'UP' in current.get('flags', []) and actual == int(bitrate) and healthy:
        return {'interface': 'can0', 'serial': SERIAL, 'bitrate': int(bitrate), 'reused': True}
    for attempt in range(2):
        try:
            run('/usr/sbin/ip', 'link', 'set', 'can0', 'down')
            run('/usr/sbin/ip', 'link', 'set', 'can0', 'type', 'can', 'bitrate', bitrate, 'restart-ms', '0', 'fd', 'off')
            run('/usr/sbin/ip', 'link', 'set', 'can0', 'up')
            return {'interface': 'can0', 'serial': SERIAL, 'bitrate': int(bitrate), 'usb_reset': attempt > 0}
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            if attempt: raise
            device = (interface/'device').resolve(strict=True)
            usb = next(parent for parent in (device, *device.parents) if (parent/'idVendor').is_file())
            if (usb/'serial').read_text().strip() != SERIAL:
                raise RuntimeError('Refusing to reset an unbound USB device')
            address = Path('/dev/bus/usb')/f"{int((usb/'busnum').read_text()):03d}"/f"{int((usb/'devnum').read_text()):03d}"
            print('Restarting only the bound PC HIL USB-CAN adapter after a configuration failure', file=sys.stderr)
            with address.open('wb', buffering=0) as handle:
                fcntl.ioctl(handle.fileno(), 0x5514, 0)  # Linux USBDEVFS_RESET
            time.sleep(1.)
            pc_device()


def main():
    if os.geteuid() != 0:
        raise PermissionError('Install the dedicated HIL helper before starting')
    if len(sys.argv) == 3 and sys.argv[1] == 'pc' and sys.argv[2] in ('250000', '1000000'):
        print(json.dumps(configure_pc(sys.argv[2])))
    elif len(sys.argv) == 2 and sys.argv[1] == 'rdk':
        run('/usr/sbin/modprobe', 'vcan')
        for name in ('vcan73', 'vcan74'):
            interface = Path('/sys/class/net')/name
            if not interface.exists():
                run('/usr/sbin/ip', 'link', 'add', name, 'type', 'vcan')
            if not interface.resolve().is_relative_to('/sys/devices/virtual/net') or (interface/'type').read_text().strip() != '280':
                raise RuntimeError('Unexpected nonvirtual HIL CAN interface')
            run('/usr/sbin/ip', 'link', 'set', name, 'up')
        print(json.dumps({'virtual_interfaces': ['vcan73', 'vcan74']}))
    else:
        raise ValueError('Only pc 250000, pc 1000000, or rdk are allowed')


if __name__ == '__main__':
    main()
