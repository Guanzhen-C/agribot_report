import json
import math
import queue
import threading
from types import SimpleNamespace

import pytest

import agribot_hil_apple.isobus_work_client as module
from agribot_hil_apple.isobus_work_client import IsobusWorkClient, parse_target


def test_spraying_default_never_enables_unmounted_shredder():
    target = dict(fan_gear=8, pump_gear=8, shredder_level=0)
    assert parse_target(json.dumps(target)) == [8, 8, 0]
    target['shredder_level'] = 10000
    with pytest.raises(ValueError):
        parse_target(json.dumps(target))
    assert parse_target(json.dumps(target), True) == [8, 8, 10000]


@pytest.mark.parametrize('value', [True, -1, 9, '3', 1.5, None])
def test_invalid_gear_rejected(value):
    with pytest.raises(ValueError):
        parse_target(json.dumps(dict(fan_gear=value, pump_gear=0, shredder_level=0)))


def test_fields_must_be_explicit_and_known():
    with pytest.raises(ValueError):
        parse_target('{"fan_gear": 1}')
    with pytest.raises(ValueError):
        parse_target('[]')


def client_fixture(monkeypatch):
    writes, now = [], [10.]
    logger = SimpleNamespace(warning=lambda *a, **k: None, error=lambda *a, **k: None)
    node = SimpleNamespace(implement='sprayer', allow_shredder=False, authorized=True,
        values=[0, 0, 0], command_at=-math.inf, ready=True, last_ready=True,
        neutral_pending=False, reader_overflow=threading.Event(), events=queue.Queue(),
        write=writes.append, publisher=SimpleNamespace(publish=lambda msg: None),
        get_logger=lambda: logger)
    monkeypatch.setattr(module.time, 'monotonic', lambda: now[0])
    return node, writes, now


def command(node, values):
    IsobusWorkClient.command(node, SimpleNamespace(data=json.dumps(dict(zip(
        ('fan_gear', 'pump_gear', 'shredder_level'), values)))))


@pytest.mark.parametrize('tick_between', [False, True])
def test_wrong_channel_then_new_legal_request_primes_neutral_in_either_callback_order(monkeypatch, tick_between):
    node, writes, _ = client_fixture(monkeypatch)
    command(node, [0, 0, 3500])
    assert writes == ['STOP\n'] and node.values == [0, 0, 0]
    if tick_between:
        IsobusWorkClient.tick(node)
    command(node, [3, 4, 0])
    IsobusWorkClient.tick(node)
    IsobusWorkClient.tick(node)
    assert writes[1] == 'SET 1 0 0\nSET 2 0 0\nSET 3 0 0\n'
    assert writes[-1] == 'SET 1 3 1\nSET 2 4 1\nSET 3 0 0\n'


@pytest.mark.parametrize('event', [dict(kind='target', accepted=False),
    dict(kind='ack', data='0101000100000000')])
def test_endpoint_rejection_discards_old_target_and_requires_new_command_after_neutral(monkeypatch, event):
    node, writes, _ = client_fixture(monkeypatch)
    command(node, [3, 4, 0])
    node.events.put(event)
    IsobusWorkClient.tick(node)
    IsobusWorkClient.tick(node)
    assert node.values == [0, 0, 0] and node.command_at == -math.inf
    assert all('SET 1 3' not in value and 'SET 2 4' not in value for value in writes)
    command(node, [3, 4, 0])
    IsobusWorkClient.tick(node)
    assert writes[-1] == 'SET 1 3 1\nSET 2 4 1\nSET 3 0 0\n'


def test_neutral_handshake_never_replays_a_now_expired_new_request(monkeypatch):
    node, writes, now = client_fixture(monkeypatch)
    command(node, [0, 0, 3500])
    command(node, [3, 4, 0])
    IsobusWorkClient.tick(node)
    now[0] += .26
    IsobusWorkClient.tick(node)
    assert writes[-1] == 'SET 1 0 0\nSET 2 0 0\nSET 3 0 0\n'
