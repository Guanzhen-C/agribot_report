from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
import admin_bus


@pytest.mark.parametrize('bitrate', ['250000', '1000000'])
def test_each_start_flushes_only_the_bound_pc_interface(monkeypatch, bitrate):
    calls = []
    monkeypatch.setattr(admin_bus, 'pc_device', lambda: Path('/sys/class/net/can0'))
    monkeypatch.setattr(admin_bus, 'run', lambda *args: calls.append(args))
    monkeypatch.setattr(admin_bus.subprocess, 'check_output', lambda *args, **kwargs:
        ('[{"flags":["UP"],"txqlen":1024,"linkinfo":{"info_data":{"bittiming":'
         '{"bitrate":'+bitrate+',"sample_point":0.875}}}}]').encode())
    result = admin_bus.configure_pc(bitrate)
    timing = ('sample-point', '0.875') if bitrate == '1000000' else ()
    expected = [('/usr/sbin/ip', 'link', 'set', 'can0', 'down'),
        ('/usr/sbin/ip', 'link', 'set', 'can0', 'type', 'can', 'bitrate', bitrate, *timing, 'restart-ms', '0', 'fd', 'off')]
    if bitrate == '1000000':
        expected.append(('/usr/sbin/ip', 'link', 'set', 'can0', 'txqueuelen', '1024'))
    expected.append(('/usr/sbin/ip', 'link', 'set', 'can0', 'up'))
    assert calls == expected
    assert result['queues_flushed'] is True


@pytest.mark.parametrize('sample,queue', [(.75, 1024), (.875, 10)])
def test_ackermann_wrong_timing_or_queue_is_not_reported_as_success(monkeypatch, sample, queue):
    import json
    monkeypatch.setattr(admin_bus, 'pc_device', lambda: Path('/sys/class/net/can0'))
    monkeypatch.setattr(admin_bus, 'run', lambda *args: None)
    actual = [{'flags': ['UP'], 'txqlen': queue,
               'linkinfo': {'info_data': {'bittiming': {'bitrate': 1000000, 'sample_point': sample}}}}]
    monkeypatch.setattr(admin_bus.subprocess, 'check_output', lambda *args, **kwargs: json.dumps(actual).encode())
    with pytest.raises(RuntimeError, match='read-back differs'):
        admin_bus.configure_pc('1000000')


def test_ackermann_settings_match_the_original_successful_initializer():
    original = Path(__file__).parents[2]/'ros2_ws/src/agribot_hil_apple/scripts/prepare_can.sh'
    script = original.read_text()
    assert 'sample-point '+str(admin_bus.ACKERMANN_SAMPLE_POINT) in script
    assert 'txqueuelen '+str(admin_bus.ACKERMANN_TX_QUEUE_LEN) in script


def test_mismatched_usb_identity_cannot_reset_any_interface(monkeypatch):
    calls = []
    def wrong_device():
        raise RuntimeError('Wrong physical PC USB-CAN serial')
    monkeypatch.setattr(admin_bus, 'pc_device', wrong_device)
    monkeypatch.setattr(admin_bus, 'run', lambda *args: calls.append(args))
    with pytest.raises(RuntimeError):
        admin_bus.configure_pc('1000000')
    assert calls == []
