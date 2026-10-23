"""Bounded ZQWL USB-CANFD bench utility, protocol V1.05. No motion commands."""
import argparse
import json
import time
from pathlib import Path
import serial
from serial.tools import list_ports


class Zqwl:
    def __init__(self, port):
        self.port = serial.Serial(port=None, baudrate=115200, timeout=0.1, write_timeout=1)
        self.port.dtr = False
        self.port.rts = False
        self.port.port = port
        self.port.open()
        self.buffer = bytearray()
        self.events = []

    def send(self, data):
        self.events.append({'direction': 'USB_TX', 'hex': data.hex(' ')})
        self.port.write(data)
        self.port.flush()

    def receive(self, seconds):
        end = time.monotonic() + seconds
        found = []
        while time.monotonic() < end:
            self.buffer.extend(self.port.read(max(1, self.port.in_waiting)))
            while len(self.buffer) >= 3:
                if self.buffer[:2] == b'I;':
                    size, tail = 22, b'E.'
                elif self.buffer[0] == 0x5a:
                    size = 17 if self.buffer[1] == 0xff else 32 if self.buffer[1] == 0xfe else (self.buffer[1] & 0x7f) + 8
                    tail = b'\xa5'
                else:
                    del self.buffer[0]
                    continue
                if len(self.buffer) < size:
                    break
                raw = bytes(self.buffer[:size])
                if not raw.endswith(tail):
                    del self.buffer[0]
                    continue
                del self.buffer[:size]
                item = {'direction': 'USB_RX', 'hex': raw.hex(' ')}
                if raw[0] == 0x5a and raw[1] not in (0xfe, 0xff):
                    item.update(kind='CAN', channel=((raw[2] >> 3) & 3) * 2 + (raw[1] >> 7), id=int.from_bytes(raw[3:7], 'big') & 0x1fffffff, fd=bool(raw[3] & 0x80), data=raw[7:-1].hex(' '))
                elif raw[:2] == b'\x5a\xff':
                    item.update(kind='heartbeat', status=raw[10], can0_tx_errors=raw[12], can0_rx_errors=raw[13], can1_tx_errors=raw[14], can1_rx_errors=raw[15])
                else:
                    item.update(kind='parameter', function=raw[2], payload=raw[4:20].hex(' '))
                self.events.append(item)
                found.append((raw, item))
        return found

    def parameter(self, function, payload=b'', write=False):
        assert len(payload) <= 16
        frame = b'I;' + bytes([function, 0x57 if write else 0x52]) + payload.ljust(16, b'\0') + b'E.'
        self.send(frame)
        for raw, item in self.receive(0.5):
            if raw[:3] == b'I;' + bytes([function]):
                return raw[4:20]
        raise RuntimeError(f'No parameter response for 0x{function:02x}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', default='COM7')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    report = {'mode': 'identify-only', 'can_transmissions': 0, 'port': args.port}
    device = None
    code = 0
    try:
        matches = [p for p in list_ports.comports() if p.device == args.port]
        if len(matches) != 1 or (matches[0].vid, matches[0].pid) != (0x3562, 0x0100):
            raise RuntimeError('Port does not match ZQWL dual-channel CANFD VID/PID')
        report['usb'] = {'vid': matches[0].vid, 'pid': matches[0].pid, 'serial': matches[0].serial_number}
        device = Zqwl(args.port)
        report['device_info'] = device.parameter(0x40).hex(' ')
        report['serial_number'] = device.parameter(0x41).hex(' ')
        report['can0_parameters'] = device.parameter(0x42, b'\x00').hex(' ')
        report['can1_parameters'] = device.parameter(0x42, b'\x01').hex(' ')
        report['result'] = 'PASS'
    except Exception as error:
        report['result'] = 'FAIL'
        report['error'] = repr(error)
        code = 1
    finally:
        if device:
            report['events'] = device.events
            device.port.close()
        text = json.dumps(report, ensure_ascii=False, indent=2)
        Path(args.output).write_text(text, encoding='utf-8')
        print(text)
    return code


if __name__ == '__main__':
    raise SystemExit(main())
