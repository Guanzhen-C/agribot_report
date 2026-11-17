"""Bind an explicit completed visual review to real new captures and a run ID."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--kind', choices=('harrow', 'spray'), required=True)
    parser.add_argument('--notes', required=True)
    parser.add_argument('--approve', action='store_true', help='Only after opening and inspecting every required image')
    args = parser.parse_args()
    if not args.approve:
        raise SystemExit('No visual pass without an explicit completed inspection')
    directory = args.directory.resolve()
    output = directory/'visual-review.json'
    if output.exists():
        raise FileExistsError(output)
    controller = json.loads((directory.parent/'controller-checks.json').read_text())
    execution = json.loads((directory/'result.json').read_text())
    if controller.get('passed') is not True or execution.get('passed') is not True:
        raise RuntimeError('Physical execution must pass before visual approval')
    names = ([stage+'/'+view+'.png' for stage in ('visual-work', 'visual-raised')
              for view in ('rear-right', 'rear-left')] if args.kind == 'harrow' else
             ['original-nozzles.png', 'existing-occlusion.png', 'pump-stopped.png'])
    images, originals = {}, {}
    for name in names:
        image = directory/name
        source = image.with_suffix('.xwd')
        images[name] = hashlib.sha256(image.read_bytes()).hexdigest()
        originals[str(source.relative_to(directory))] = hashlib.sha256(source.read_bytes()).hexdigest()
    report = dict(passed=True, run_id=controller['run_id'], kind=args.kind,
        reviewed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        reviewer='AI assistant, direct inspection of the actual Gazebo window captures',
        scope='Development-side basic visual check, not a third-party signature or physical soil/fluid certification',
        images_sha256=images, original_capture_sha256=originals, observations=args.notes)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(run_id=report['run_id'], images=len(images), passed=True)))


if __name__ == '__main__':
    main()
