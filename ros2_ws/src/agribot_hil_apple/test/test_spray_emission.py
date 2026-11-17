from pathlib import Path
import shutil
import subprocess

import pytest


def test_stopped_pump_has_no_new_emission_despite_real_shaft_inertia(tmp_path):
    compiler = shutil.which('g++')
    if not compiler:
        pytest.skip('C++ compiler required')
    root = Path(__file__).resolve().parents[1]
    binary = tmp_path/'spray-emission-test'
    subprocess.run([compiler, '-std=c++17', '-I', str(root/'plugins'),
                    str(root/'test/test_spray_emission.cpp'), '-o', str(binary)], check=True)
    subprocess.run([str(binary)], check=True)
