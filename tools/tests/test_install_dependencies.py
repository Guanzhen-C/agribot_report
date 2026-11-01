"""Keep the declared Jammy dependency list usable with its native package names."""
from pathlib import Path
import shlex
import subprocess


def test_jammy_package_name_and_driver_headers_are_declared():
    script = Path(__file__).resolve().parents[2] / 'scripts/install_dependencies.sh'
    subprocess.run(['bash', '-n', str(script)], check=True)
    commands = script.read_text().replace('\\\n', ' ').splitlines()
    installs = [shlex.split(line) for line in commands if line.startswith('sudo apt-get install ')]
    assert len(installs) == 1
    packages = installs[0][4:]
    assert 'libgeographic-dev' in packages
    assert 'libgeographiclib-dev' not in packages
    assert 'libasio-dev' in packages
    assert 'libpcap-dev' in packages
    assert 'python3-pyproj' in packages
