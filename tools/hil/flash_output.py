"""Stream OpenOCD output into real stage progress, never elapsed-time estimates."""
import codecs
import os
from pathlib import Path
import selectors
import subprocess
import time

STAGES = (
    ('Target successfully examined', .15, '烧录器已连接'),
    ('Programming Started', .25, '正在写入固件'),
    ('Programming Finished', .70, '固件写入完成'),
    ('Verify Started', .75, '正在校验固件'),
    ('Verified OK', .95, '校验完成，正在复位'),
)


def run_flash(command, environment, output, progress, timeout=180.):
    decoder = codecs.getincrementaldecoder('utf-8')(errors='replace')
    captured, tail, completed = [], '', 0.
    start = time.monotonic()
    with subprocess.Popen(list(map(str, command)), env=environment, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, bufsize=0) as process:
        with selectors.DefaultSelector() as selector, Path(output).open('w') as stream:
            selector.register(process.stdout, selectors.EVENT_READ)
            while selector.get_map():
                if time.monotonic()-start > timeout:
                    process.kill()
                    process.wait()
                    raise TimeoutError('6750烧录工具超时，未确认固件校验完成。')
                for key, _ in selector.select(.1):
                    raw = os.read(key.fd, 65536)
                    text = decoder.decode(raw, final=not raw)
                    if not raw: selector.unregister(key.fileobj)
                    if not text: continue
                    captured.append(text)
                    stream.write(text); stream.flush()
                    tail = (tail+text)[-4096:]
                    for marker, amount, label in STAGES:
                        if marker in tail and amount > completed:
                            completed = amount
                            progress(amount, label)
            try:
                process.wait(timeout=max(.001, timeout-(time.monotonic()-start)))
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
                raise TimeoutError('6750烧录工具超时，未确认固件校验完成。') from None
    return subprocess.CompletedProcess(command, process.returncode, ''.join(captured))
