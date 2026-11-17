from pathlib import Path
import runpy


def audit():
    source=Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/sensor_audit.py'
    return runpy.run_path(str(source))['compare_images']


def test_same_stamp_render_burst_is_not_false_pixel_corruption():
    rows=[[0,'optical',640,512,'first'],[1,'optical',640,512,'a'],
          [1,'optical',640,512,'b'],[2,'optical',640,512,'last']]
    decoded=[rows[0],rows[2],rows[1],rows[3]]
    result=audit()(rows,decoded)
    assert result['passed'] and result['matched_frames']==2
    assert result['duplicate_raw_stamps']=={'1':2}


def test_actual_changed_pixels_or_missing_internal_frame_still_fail():
    rows=[[0,'optical',640,512,'first'],[1,'optical',640,512,'a'],
          [1,'optical',640,512,'b'],[2,'optical',640,512,'last']]
    decoded=[rows[0],[1,'optical',640,512,'corrupted'],rows[2],rows[3]]
    assert not audit()(rows,decoded)['passed']
    assert not audit()(rows,[rows[0],rows[1],rows[3]])['passed']
