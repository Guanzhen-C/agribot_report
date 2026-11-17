import copy
import importlib.util
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

import pytest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('hil_diff_drive_overlay', ROOT/'tools/hil/diff_drive_overlay.py')
overlay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(overlay)


def test_actual_cpp_setpoint_does_not_integrate_stall_error(tmp_path):
    code = r'''
    #include <cassert>
    #include <cmath>
    #include <gazebo_plugins/wheel_velocity_limit.hpp>
    int main() {
      using gazebo_plugins::limit_wheel_velocity;
      double command = 0.;
      for (int i = 0; i < 10000; ++i) {
        const double next = limit_wheel_velocity(command, .3, .8, .01);
        assert(next >= command && next <= .3);
        assert(next-command <= .0080000001);
        command = next;
      }
      assert(std::abs(command-.3) < 1e-12);
      for (int i = 0; i < 10000; ++i) {
        const double next = limit_wheel_velocity(command, -.3, .8, .01);
        assert(next <= command && next >= -.3);
        assert(command-next <= .0080000001);
        command = next;
      }
      assert(std::abs(command+.3) < 1e-12);
      for (int i = 0; i < 10000; ++i) {
        command = limit_wheel_velocity(command, 0., .8, .01);
        assert(command <= 0. && command >= -.3);
      }
      assert(command == 0.);
      assert(limit_wheel_velocity(.2, -.4, 0., .01) == -.4);
      assert(limit_wheel_velocity(.2, -.4, .8, 0.) == .2);
      assert(limit_wheel_velocity(.2, -.4, .8, -.01) == .2);
    }
    '''
    source, executable = tmp_path/'limits.cpp', tmp_path/'limits'
    source.write_text(code)
    subprocess.run(['g++', '-std=c++14', '-Wall', '-Werror', '-I', str(ROOT/
        'ros2_ws/src/gazebo_ros_pkgs/gazebo_plugins/include'), str(source), '-o', str(executable)], check=True)
    subprocess.run([str(executable)], check=True)


def test_old_stall_accumulation_would_exceed_target_but_is_removed():
    old = 0.
    for _ in range(1000):
        old += min(.3-0., .8*.01)
    assert old > 7.9
    source = (ROOT/'ros2_ws/src/gazebo_ros_pkgs/gazebo_plugins/src/gazebo_ros_diff_drive.cpp').read_text()
    assert 'wheel_speed_instr_[index] = limit_wheel_velocity(' in source
    assert 'desired_wheel_speed_[2 * i + LEFT] - current_speed' not in source


def test_reset_clears_both_target_and_slewed_wheel_commands():
    source = (ROOT/'ros2_ws/src/gazebo_ros_pkgs/gazebo_plugins/src/gazebo_ros_diff_drive.cpp').read_text()
    reset = source.split('void GazeboRosDiffDrive::Reset()', 1)[1].split('void GazeboRosDiffDrivePrivate::OnUpdate', 1)[0]
    assert 'std::fill(impl_->wheel_speed_instr_.begin(), impl_->wheel_speed_instr_.end(), 0.0);' in reset
    assert 'std::fill(impl_->desired_wheel_speed_.begin(), impl_->desired_wheel_speed_.end(), 0.0);' in reset
    assert reset.count('SetParam("vel", 0, 0.0)') == 2


def semantic(tree):
    value = copy.deepcopy(tree)
    for node in value.iter():
        node.text = (node.text or '').strip()
        node.tail = ''
    return ET.tostring(value)


def test_overlay_only_selects_car3_plugin_without_geometry_or_parameter_changes(tmp_path):
    before = {}
    for name in ('tracked_sprayer', 'tracked_shredder'):
        source = ROOT/'resources/hil/scenarios'/name/'vehicle.sdf'
        destination = tmp_path/name/'vehicle.sdf'
        destination.parent.mkdir()
        destination.write_bytes(source.read_bytes())
        before[name] = ET.fromstring(source.read_bytes())
    first = overlay.configure(tmp_path)
    after = {name: (tmp_path/name/'vehicle.sdf').read_bytes() for name in before}
    for name in before:
        actual = ET.fromstring(after[name])
        expected = before[name]
        expected.find(".//plugin[@name='configured_diff_drive']").set('filename', overlay.LIBRARY)
        assert semantic(actual) == semantic(expected)
        assert first['models'][name]['library'] == overlay.LIBRARY
    overlay.configure(tmp_path)
    assert all((tmp_path/name/'vehicle.sdf').read_bytes() == value for name,value in after.items())


def test_wrong_or_missing_model_plugin_is_not_silently_replaced(tmp_path):
    path = tmp_path/'tracked_sprayer/vehicle.sdf'
    path.parent.mkdir()
    path.write_text('<sdf><model><plugin name="configured_diff_drive" filename="other.so"/></model></sdf>')
    with pytest.raises(ValueError, match='Unexpected Car3'):
        overlay.configure(tmp_path)


def test_invalid_second_model_does_not_leave_a_partial_overlay(tmp_path):
    first = tmp_path/'tracked_sprayer/vehicle.sdf'
    second = tmp_path/'tracked_shredder/vehicle.sdf'
    first.parent.mkdir()
    second.parent.mkdir()
    first.write_text('<sdf><model><plugin name="configured_diff_drive" filename="libgazebo_ros_diff_drive.so"/></model></sdf>')
    second.write_text('<sdf><model/></sdf>')
    before = first.read_bytes()
    with pytest.raises(ValueError, match='Unexpected Car3'):
        overlay.configure(tmp_path)
    assert first.read_bytes() == before
