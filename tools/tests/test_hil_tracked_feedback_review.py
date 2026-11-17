import copy
import importlib.util
from pathlib import Path

import pytest

SOURCE = Path(__file__).parents[2]/'docs/deliverables/20261116_hil_retest/review_tracked.py'
spec = importlib.util.spec_from_file_location('hil_tracked_feedback_review', SOURCE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def checked(values):
    checksum = 0
    for byte in values:
        checksum ^= byte
    return bytes([*values, checksum]).hex()


def fixture():
    physical = dict(ok=True, records=[])
    usb = dict(received=[], sent=[], errors=[], decoder_invalid=0)
    for device, value in ((1, 3), (2, 4)):
        usb['sent'].append([0., 0x18ef9190, True, checked([1, 1, 1, device, value, 0, 1])])
    for stamp in (1., 2., 3.):
        for identifier in (0x5b2, 0x5b3):
            data = checked([0, int(stamp), 0, 75, 0, 0, int(stamp)])
            physical['records'] += [dict(wall=stamp, kind='native_can', value=dict(id=identifier, data=data)),
                dict(wall=stamp+.05, kind='isobus', value=dict(kind='feedback', id=identifier, data=data))]
            usb['received'].append([stamp, 0x18000000 | ((0xff52+identifier-0x5b2) << 8) | 0x91, True, data])
    return physical, usb


def test_three_way_feedback_agreement():
    assert module.analyze(*fixture(), 'sprayer')['passed']


@pytest.mark.parametrize('change', ['raw-wire', 'native', 'checksum', 'physical-failure', 'decoder'])
def test_missing_or_invalid_real_inputs_cannot_become_pass(change):
    physical, usb = copy.deepcopy(fixture())
    if change == 'raw-wire':
        usb['received'] = []
    elif change == 'native':
        physical['records'] = [row for row in physical['records'] if row['kind'] != 'native_can']
    elif change == 'checksum':
        usb['sent'][0][3] = usb['sent'][0][3][:-2]+'ff'
    elif change == 'physical-failure':
        physical['ok'] = False
    else:
        usb['decoder_invalid'] = 1
    assert not module.analyze(physical, usb, 'sprayer')['passed']
