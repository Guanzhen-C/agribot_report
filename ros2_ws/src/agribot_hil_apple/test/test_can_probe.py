from collections import deque
import json
import sys

import pytest

from agribot_hil_apple import can_probe


@pytest.mark.parametrize('extra', [
    ['--rounds', '0'], ['--rounds', '1001'], ['--period', '.049'], ['--period', '1.1'],
])
def test_invalid_load_limits_fail_before_opening_hardware(monkeypatch, extra):
    monkeypatch.setattr(sys, 'argv', ['probe', 'pc', '--interface', 'can0',
                                    '--expected-usb-serial', 'test', *extra])
    def forbidden(*args, **kwargs):
        pytest.fail('Invalid probe arguments touched hardware')
    monkeypatch.setattr(can_probe, 'CanSocket', forbidden)
    with pytest.raises(SystemExit) as error:
        can_probe.main()
    assert error.value.code == 2


@pytest.mark.parametrize('rounds,extra', [(3, []), (7, ['--rounds', '7', '--period', '.05'])])
def test_original_and_extended_probes_have_exact_finite_frame_counts(monkeypatch, capsys, rounds, extra):
    pending = deque([(can_probe.COMMAND_ID, b'\0'*8)]*rounds)
    sent, closed = [], []
    class Bus:
        def __init__(self, *args): pass
        def receive(self): return pending.popleft() if pending else None
        def send(self, identifier, payload): sent.append((identifier, payload))
        def close(self): closed.append(True)
    now = [0.]
    def clock():
        now[0] += .01
        return now[0]
    monkeypatch.setattr(can_probe, 'CanSocket', Bus)
    monkeypatch.setattr(can_probe.time, 'monotonic', clock)
    monkeypatch.setattr(can_probe.time, 'sleep', lambda _: None)
    monkeypatch.setattr(sys, 'argv', ['probe', 'pc', '--interface', 'can0',
                                    '--expected-usb-serial', 'test', *extra])
    can_probe.main()
    report = json.loads(capsys.readouterr().out)
    assert report['ok'] is True and report['received'] == rounds
    assert report['sent'] == len(sent) == rounds*3
    assert closed == [True]
