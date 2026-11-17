"""Register a named human's completed review of new HIL captures; no hardware I/O."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path


NAMES = {
    'spray': ['original-nozzles.png', 'existing-occlusion.png', 'pump-stopped.png'],
    'harrow': [f'{stage}/{view}.png' for stage in ('visual-work', 'visual-raised')
               for view in ('rear-right', 'rear-left')],
}


def register(directory, kind, reviewer, notes, approve):
    if not approve or not reviewer.strip() or not notes.strip():
        raise ValueError('必须先实际查看全部图片，并填写真实看图人员姓名、观察内容及确认选项。')
    directory = directory.resolve()
    controller = json.loads((directory.parent / 'controller-checks.json').read_text())
    execution = json.loads((directory / 'result.json').read_text())
    if controller.get('passed') is not True or execution.get('passed') is not True:
        raise ValueError('对应实际测试尚未通过，不能用看图登记代替执行结果。')
    identity = controller['run_id']
    images, originals = {}, {}
    for name in NAMES[kind]:
        image = directory / name
        data = image.read_bytes()
        if not data.startswith(b'\x89PNG\r\n\x1a\n'):
            raise ValueError('不是有效PNG文件：' + name)
        source = image.with_suffix('.xwd')
        images[name] = hashlib.sha256(data).hexdigest()
        originals[str(source.relative_to(directory))] = hashlib.sha256(source.read_bytes()).hexdigest()
    value = dict(passed=True, run_id=identity, kind=kind,
        reviewed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        reviewer=reviewer.strip(), reviewer_type='named_human',
        scope='Named human basic visual review; not a third-party signature, soil or fluid certification',
        images_sha256=images, original_capture_sha256=originals, observations=notes.strip())
    with (directory / 'visual-review.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    return value


def main(argv=None):
    parser = argparse.ArgumentParser(description='只登记已完成的人工看图结果，不启动仿真、不发CAN、不烧录；不覆盖已有登记。')
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--kind', choices=tuple(NAMES), required=True)
    parser.add_argument('--reviewer', required=True)
    parser.add_argument('--notes', required=True)
    parser.add_argument('--approve', action='store_true', help='仅在实际看过本组全部图片且符合判据后使用')
    args = parser.parse_args(argv)
    try:
        value = register(args.directory, args.kind, args.reviewer, args.notes, args.approve)
    except (OSError, ValueError, KeyError) as error:
        print('未登记：' + str(error))
        return 2
    print(f'已登记本组{len(value["images_sha256"])}张图；审阅者：{value["reviewer"]}。仍须执行最终汇总。')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
