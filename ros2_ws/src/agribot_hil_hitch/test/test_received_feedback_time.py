import importlib.util
import io
from pathlib import Path
import queue
import struct
import sys
import threading
from types import SimpleNamespace


def load():
    scripts = Path(__file__).resolve().parents[1]/'scripts'
    sys.path.insert(0, str(scripts))
    spec = importlib.util.spec_from_file_location('hil_received_time_client', scripts/'rdk_client.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_reader_stamps_arrival_instead_of_relabelling_delayed_feedback(monkeypatch):
    module = load()
    monkeypatch.setattr(module.time, 'monotonic', lambda: 10.)
    node = SimpleNamespace(endpoint=SimpleNamespace(stdout=io.StringIO('{"kind":"network","ready":true}\n')),
                           events=queue.Queue(), reader_failed=threading.Event())
    module.Client.read(node)
    at, event = node.events.get_nowait()
    assert at == 10. and event == dict(kind='network', ready=True)


def test_queued_old_feedback_keeps_its_original_freshness():
    module = load()
    node = SimpleNamespace(power_data={})
    event = dict(kind='powertrain_status', data='2203000000000062')
    from wire import seal
    event['data'] = seal(bytes.fromhex('22030000000000')).hex()
    module.Client.power_event(node, event, 10.)
    assert node.power_data['powertrain_status']['at'] == 10.
    from powertrain_wire import feedback_fresh
    assert not feedback_fresh(node.power_data, ['powertrain_status'], 11.)


def test_feedback_arriving_during_tick_is_not_misclassified_as_future(monkeypatch):
    module = load()
    clock = iter([10., 10.0015, 10.002])
    monkeypatch.setattr(module.time, 'monotonic', lambda: next(clock))
    received = 10.001
    calls = []
    def healthy(data, now):
        calls.append(now)
        return 0 <= now-received < .3
    monkeypatch.setattr(module, 'healthy_feedback', healthy)
    sink = SimpleNamespace(publish=lambda _: None)
    commands = []
    def blocked(_):
        raise BlockingIOError()
    node = SimpleNamespace(last_tick_at=9.99, trace=io.StringIO(), reader_failed=threading.Event(),
        events=queue.Queue(), power_data={'valid': True}, ready=True, was_ready=True,
        power_publish_at=10., power_pub=sink, state_pub=sink, power_event=lambda *args: None,
        powertrain_enabled=True, prime_until=0., work_authorized=False, work_at=0.,
        target=(0, 0, 0), drive_authorized=True, drive_at=9.99, require_localization=False,
        power_at=9.99, power_target=(2, 0, 1600, 0., 600, 150.), last_power_gate=None,
        write=commands.append, drive_target=(.2, 0.), frame=struct.Struct('=IB3x8s'), sequence=0,
        bus=SimpleNamespace(send=lambda _: None, recv=blocked))
    node.events.put((received, {'kind': 'other'}))
    node.update_feedback_priming = lambda now: module.Client.update_feedback_priming(node, now)
    module.Client.tick(node)
    assert calls and all(now >= received for now in calls)
    assert node.prime_until == 0.
    assert any(command.startswith('POWER 2 ') for command in commands)
