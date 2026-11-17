"""Fail-closed startup sequencing; platform-specific effects are injected and tested."""
import time

from catalog import firmware, profile, ROOT


class Startup:
    def __init__(self, backend, emit, cancelled=lambda: False, root=ROOT):
        self.backend, self.emit, self.cancelled, self.root = backend, emit, cancelled, root

    def step(self, stage, action):
        if self.cancelled():
            raise InterruptedError('HIL startup cancelled')
        self.emit(stage)
        return action()

    def run(self, identifier):
        selected = profile(identifier, self.root)
        if selected is None:
            raise ValueError('Unsupported HIL combination')
        image = firmware(selected['firmware'], self.root)
        try:
            self.step('checking', lambda: self.backend.preflight(selected, image))
            self.step('stopping_previous', self.backend.stop_previous)
            self.step('checking_protected_runtime', self.backend.verify_real_runtime)
            # Deliberately unconditional: every user-requested HIL start flashes.
            self.step('flashing', lambda: self.backend.flash(image))
            self.step('configuring_can', lambda: self.backend.configure_bus(image['bitrate']))
            self.step('starting_pc', lambda: self.backend.start_pc(selected))
            self.step('starting_x5', lambda: self.backend.start_x5(selected))
            self.step('waiting_ready', lambda: self.backend.wait_ready(selected))
            self.step('checking_protected_runtime', self.backend.verify_real_runtime)
            self.step('starting_mission', lambda: self.backend.start_mission(selected))
            self.emit('running')
        except BaseException:
            self.emit('stopping_after_error')
            try:
                self.backend.safe_stop()
            finally:
                self.emit('failed')
            raise
        return selected
