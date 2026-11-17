"""TX-completion supervision for the bound physical HIL USB-CAN only."""
import errno
import json
import math
from pathlib import Path
import subprocess
import time


def temporary_tx_error(error):
    return isinstance(error, OSError) and error.errno in (errno.EAGAIN, errno.EWOULDBLOCK, errno.ENOBUFS)


def verify_adapter(interface, serial, bitrate):
    if interface != 'can0' or bitrate not in (250000, 1000000):
        raise ValueError('Recovery is restricted to the physical HIL can0 and reviewed bitrates')
    device = (Path('/sys/class/net')/interface/'device').resolve(strict=True)
    if (device/'driver').resolve().name != 'gs_usb':
        raise ValueError('HIL interface is not gs_usb')
    usb = next((p for p in (device, *device.parents) if (p/'idVendor').is_file()), None)
    if usb is None or (usb/'serial').read_text().strip() != serial:
        raise ValueError('HIL USB-CAN identity mismatch')
    link, = json.loads(subprocess.check_output(['ip', '-j', '-d', 'link', 'show', interface]))
    info = link['linkinfo']['info_data']
    if ('UP' not in link['flags'] or link['mtu'] != 16 or
            info['bittiming']['bitrate'] != bitrate or info['state'] != 'ERROR-ACTIVE'):
        raise ValueError('HIL CAN timing/state is not safe for controlled recovery')


class PhysicalTxGuard:
    def __init__(self, interface, serial, bitrate):
        self.interface, self.serial, self.bitrate = interface, serial, bitrate
        self.recoveries, self.last_attempt = 0, -math.inf
        self.reset_progress()

    def packets(self):
        return int((Path('/sys/class/net')/self.interface/'statistics/tx_packets').read_text())

    def reset_progress(self):
        self.previous = self.packets()
        self.pending_since, self.checked_at = None, -math.inf
        self.stalled, self.last_progress_at = False, -math.inf

    def sent(self):
        if self.pending_since is None:
            self.pending_since = time.monotonic()

    def check(self):
        now = time.monotonic()
        if now-self.checked_at >= .1:
            self.checked_at = now
            packets = self.packets()
            if packets != self.previous:
                self.previous, self.pending_since = packets, None
                self.stalled, self.last_progress_at = False, now
            elif self.pending_since is not None and now-self.pending_since >= .3:
                self.stalled = True
        if self.stalled:
            raise OSError(errno.ENOBUFS, 'Physical HIL CAN TX completions stalled')

    def recover(self, bus, opener):
        """Caller must hold all actuators neutral before entering this method."""
        now = time.monotonic()
        if now-self.last_attempt < 5.:
            return bus, False
        self.last_attempt = now
        verify_adapter(self.interface, self.serial, self.bitrate)
        before = self.packets()
        time.sleep(.05)
        if self.packets() != before:
            return bus, False
        if self.recoveries >= 3:
            raise RuntimeError('Repeated physical CAN stalls; inspect the adapter and bus')
        bus.close()
        subprocess.run(['sudo', '-n', '/usr/local/libexec/agribot-hil-bus', 'pc', str(self.bitrate)],
                       check=True, capture_output=True, text=True, timeout=8.)
        bus = opener()
        self.recoveries += 1
        self.reset_progress()
        return bus, True
