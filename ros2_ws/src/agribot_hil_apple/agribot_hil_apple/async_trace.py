"""Bounded, loss-intolerant trace writer for HIL control/transport loops."""
from pathlib import Path
import queue
import threading
import time


class TraceError(RuntimeError):
    pass


class AsyncTrace:
    """write/flush never wait for storage; close drains after outputs are disarmed.

    flush is a health checkpoint; storage is flushed periodically by the worker.
    Queue exhaustion or storage failure is explicit so incomplete evidence
    cannot silently produce a passing run.
    """
    _STOP = object()

    def __init__(self, path, *, capacity=8192, flush_interval=.25, close_timeout=15., opener=None):
        if capacity <= 0 or flush_interval <= 0 or close_timeout <= 0:
            raise ValueError('Trace bounds must be positive')
        self.path = Path(path)
        self._stream = (opener or (lambda p: p.open('x')))(self.path)
        self._queue = queue.Queue(maxsize=capacity)
        self._flush_interval, self._close_timeout = flush_interval, close_timeout
        self._failure = None
        self._closed = False
        self._thread = threading.Thread(target=self._run, name='hil-trace-writer', daemon=True)
        self._thread.start()

    def check(self):
        if self._failure is not None:
            raise TraceError('HIL trace is incomplete: '+str(self._failure)) from self._failure

    def write(self, text):
        self.check()
        if self._closed:
            raise ValueError('Trace is closed')
        if not isinstance(text, str):
            raise TypeError('Trace records must be text')
        try:
            self._queue.put_nowait(text)
        except queue.Full as error:
            self._failure = error
            raise TraceError('HIL trace queue exhausted; abort rather than discard evidence') from error
        return len(text)

    def flush(self):
        self.check()

    def _run(self):
        last_flush = time.monotonic()
        try:
            while True:
                try:
                    item = self._queue.get(timeout=self._flush_interval)
                except queue.Empty:
                    item = None
                if item is self._STOP:
                    break
                if item is not None:
                    self._stream.write(item)
                now = time.monotonic()
                if now-last_flush >= self._flush_interval:
                    self._stream.flush()
                    last_flush = now
            self._stream.flush()
        except BaseException as error:
            self._failure = error
        finally:
            try:
                self._stream.close()
            except BaseException as error:
                self._failure = self._failure or error

    def close(self):
        if self._closed:
            self.check()
            return
        self._closed = True
        if self._thread.is_alive():
            try:
                self._queue.put(self._STOP, timeout=self._close_timeout)
            except queue.Full as error:
                raise TraceError('HIL trace could not drain after disarm') from error
            self._thread.join(timeout=self._close_timeout)
            if self._thread.is_alive():
                raise TraceError('HIL trace writer did not stop after disarm')
        self.check()
