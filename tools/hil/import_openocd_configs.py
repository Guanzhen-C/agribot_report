"""Vendor only the six required, unmodified HPM board/probe configuration files."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

FILES = ('probes/cmsis_dap.cfg', 'soc/hpm6750-single-core.cfg', 'boards/hpm6750evk2.cfg',
         'soc/hpm_common.cfg', 'soc/hpm_common_csr.cfg', 'soc/hpm_reset.cfg')


def import_configs(sdk, output):
    if output.exists(): raise FileExistsError(output)
    source = sdk/'boards/openocd'
    for relative in FILES:
        if not (source/relative).is_file(): raise FileNotFoundError(source/relative)
    if not (sdk/'LICENSE').is_file(): raise FileNotFoundError(sdk/'LICENSE')
    output.mkdir(parents=True)
    records = {}
    for relative in FILES:
        destination = output/relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source/relative, destination)
        records[relative] = hashlib.sha256(destination.read_bytes()).hexdigest()
    shutil.copyfile(sdk/'LICENSE', output/'LICENSE')
    records['LICENSE'] = hashlib.sha256((output/'LICENSE').read_bytes()).hexdigest()
    (output/'provenance.json').write_text(json.dumps({'upstream': 'HPMicro HPM SDK boards/openocd',
        'license': 'BSD-3-Clause', 'modified': False, 'files': records}, indent=2)+'\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    import_configs(args.sdk, args.output)
