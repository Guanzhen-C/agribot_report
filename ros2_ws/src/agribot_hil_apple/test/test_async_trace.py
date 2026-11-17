from pathlib import Path
import threading
import time

import pytest

from agribot_hil_apple.async_trace import AsyncTrace, TraceError


class SlowFile:
    def __init__(self):
        self.entered, self.release = threading.Event(), threading.Event()
        self.records = []

    def write(self, text):
        self.entered.set()
        assert self.release.wait(5.)
        self.records.append(text)

    def flush(self):
        pass

    def close(self):
        pass


def test_slow_storage_cannot_block_the_control_writer_and_close_preserves_order(tmp_path):
    stream = SlowFile()
    trace = AsyncTrace(tmp_path/'trace', opener=lambda _: stream)
    try:
        trace.write('first\n')
        assert stream.entered.wait(1.)
        start = time.monotonic()
        for i in range(200):
            trace.write(str(i)+'\n')
            trace.flush()
        assert time.monotonic()-start < .1
    finally:
        stream.release.set()
        trace.close()
    assert stream.records == ['first\n', *[str(i)+'\n' for i in range(200)]]


def test_queue_exhaustion_is_explicit_and_cannot_become_a_pass(tmp_path):
    stream = SlowFile()
    trace = AsyncTrace(tmp_path/'trace', capacity=1, opener=lambda _: stream)
    trace.write('inflight')
    assert stream.entered.wait(1.)
    trace.write('queued')
    try:
        with pytest.raises(TraceError, match='queue exhausted'):
            trace.write('not-recorded')
    finally:
        stream.release.set()
        with pytest.raises(TraceError, match='incomplete'):
            trace.close()


def test_trace_close_drains_all_json_lines_and_refuses_overwrite(tmp_path):
    path = tmp_path/'trace.jsonl'
    trace = AsyncTrace(path)
    for i in range(300):
        trace.write('{"counter":'+str(i)+'}\n')
    trace.close()
    trace.close()
    assert len(path.read_text().splitlines()) == 300
    with pytest.raises(FileExistsError):
        AsyncTrace(path)
    with pytest.raises(ValueError, match='closed'):
        trace.write('late')


def test_storage_failure_is_reported_to_the_controller(tmp_path):
    class BrokenFile(SlowFile):
        def write(self, text):
            self.entered.set()
            raise OSError('storage unavailable')
    stream = BrokenFile()
    trace = AsyncTrace(tmp_path/'trace', opener=lambda _: stream)
    trace.write('record')
    assert stream.entered.wait(1.)
    trace._thread.join(timeout=1.)
    with pytest.raises(TraceError, match='storage unavailable'):
        trace.flush()
    with pytest.raises(TraceError, match='storage unavailable'):
        trace.close()
