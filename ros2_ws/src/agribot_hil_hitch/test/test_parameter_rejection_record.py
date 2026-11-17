"""Rejected high-level requests must remain attributable in physical test logs."""
import importlib.util
import io
import json
from pathlib import Path
import sys
from types import SimpleNamespace


def test_rejected_request_is_recorded_without_preserving_an_active_target():
    scripts=Path(__file__).resolve().parents[1]/'scripts'
    sys.path.insert(0,str(scripts))
    try:
        spec=importlib.util.spec_from_file_location('hil_rejection_record_client',scripts/'rdk_client.py')
        module=importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        calls=[]
        trace=io.StringIO()
        node=SimpleNamespace(power_target=(3,0,1600,0.,600,150.),power_at=100.,
                             write=calls.append,trace=trace)
        invalid=json.dumps(dict(mode='cvt_auto',gear=0,engine_rpm=2201,ratio=0,shift_ms=600,ratio_rate=150))
        module.Client.power(node,SimpleNamespace(data=invalid))
        event=json.loads(trace.getvalue())['event']
        assert event['kind']=='powertrain_invalid_command'
        assert event['input']==invalid and event['reason']
        assert calls==['STOP_POWERTRAIN']
        assert node.power_at == -float('inf') and node.power_target[0] == 0
    finally:
        sys.path.remove(str(scripts))
