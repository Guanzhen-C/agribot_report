from pathlib import Path


def test_transparent_ground_edges_do_not_write_depth_or_disable_occlusion():
    source = (Path(__file__).resolve().parents[1]/'plugins/tillage_visual.cpp').read_text()
    material = source.split('pass = soil_material_->getTechnique(0)->getPass(0);', 1)[1].split('auto manager =', 1)[0]
    assert 'setDepthWriteEnabled(false)' in material
    assert 'trail_->begin(soil_material_->getName()' in source
    assert 'clods_->begin(soil_material_->getName()' in source
    assert 'setDepthCheckEnabled(false)' not in source
