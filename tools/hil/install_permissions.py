"""One-time PC setup: serial-bound debug probe and two exact CAN commands."""
import argparse
import grp
import os
from pathlib import Path
import pwd
import re
import shutil
import subprocess


def install(user, serial):
    if os.geteuid() != 0:
        raise PermissionError('Run this preparation step through sudo')
    if not re.fullmatch('[a-z_][a-z0-9_-]*', user) or not re.fullmatch('[a-zA-Z0-9]+', serial):
        raise ValueError('Invalid user or debug probe serial')
    account = pwd.getpwnam(user)
    group = grp.getgrnam('plugdev')
    if group.gr_gid not in os.getgrouplist(user, account.pw_gid):
        raise ValueError('The operator must already belong to plugdev; no group memberships were changed')
    destination = Path('/usr/local/libexec/agribot-hil-bus')
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(Path(__file__).with_name('admin_bus.py'), destination)
    destination.chmod(0o755)
    os.chown(destination, 0, 0)
    rule = ('SUBSYSTEM=="usb", ATTR{idVendor}=="1fc9", ATTR{idProduct}=="5601", '
            'ATTR{serial}=="'+serial+'", MODE="0660", GROUP="plugdev"\n')
    rule += ('SUBSYSTEM=="hidraw", ATTRS{idVendor}=="1fc9", ATTRS{idProduct}=="5601", '
             'ATTRS{serial}=="'+serial+'", MODE="0660", GROUP="plugdev"\n')
    udev = Path('/etc/udev/rules.d/70-agribot-hil-probe.rules')
    udev.write_text(rule)
    udev.chmod(0o644)
    sudoers = Path('/etc/sudoers.d/agribot-hil-bus')
    temporary = sudoers.with_suffix('.new')
    temporary.write_text(user+' ALL=(root) NOPASSWD: '+str(destination)+' pc 250000, '+str(destination)+' pc 1000000\n')
    temporary.chmod(0o440)
    subprocess.run(['/usr/sbin/visudo', '-cf', str(temporary)], check=True)
    temporary.replace(sudoers)
    subprocess.run(['udevadm', 'control', '--reload-rules'], check=True)
    subprocess.run(['udevadm', 'trigger', '--subsystem-match=usb', '--attr-match=serial='+serial], check=True)
    subprocess.run(['udevadm', 'trigger', '--subsystem-match=hidraw'], check=True)
    print('Installed scoped HIL permissions; no CAN configuration or firmware was changed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--user', required=True)
    parser.add_argument('--probe-serial', required=True)
    args = parser.parse_args()
    install(args.user, args.probe_serial)
