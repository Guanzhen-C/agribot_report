"""Read HIL progress or an existing case ledger; never control the test bench."""
import argparse
import json
from pathlib import Path


IDS = [f'TC-HIL-{group}-001-{letter}' for group in ('VCU', 'IMP', 'NAV') for letter in 'abcdefghij']
STAGES = {
    'starting': '正在准备测试',
    'auxiliary': '正在检查烧录、接口和软件保护功能',
    'physical': '正在依次执行五种工况的正常任务和控制专项',
    'independent-review': '正在核对已保存的原始记录',
    'awaiting_visual_review': '自动运行已结束，仍需查看七张截图、登记观察并执行最终汇总',
    'finalized': '已完成最终汇总',
    'failed': '本轮中断或不通过，保留本目录并联系项目人员',
}


def case_id(value):
    if value in IDS:
        return value
    if value.startswith('YL-FU-') and value[6:].isdigit():
        number = int(value[6:])
        if 1 <= number <= 30:
            return IDS[number - 1]
    raise ValueError('用例编号应为 YL-FU-001 至 YL-FU-030，或完整的 TC-HIL 编号。')


def read_ledger(path):
    value = json.loads(path.read_text(encoding='utf-8'))
    if value.get('requirements_version') != 'V2.1':
        raise ValueError('结果未明确对应V2.1，不能按本套30条判定。')
    rows = value.get('records', [])
    if len(rows) != 30 or {row['id'] for row in rows} != set(IDS):
        raise ValueError('结果不是完整且编号唯一的30条记录，不能据此宣称整轮通过。')
    return value


def show(args):
    selected = case_id(args.case) if args.case else None
    if args.ledger:
        print('阅读方式：历史归档结果，不代表刚刚又执行了一轮测试。')
        path = args.ledger.resolve()
        state = None
    else:
        folder = args.output.resolve()
        status = folder / 'execution.json'
        if not status.is_file():
            raise ValueError('尚未找到本轮 execution.json。请核对 OUT；仅执行 plan/check 不会生成实测结果。')
        state = json.loads(status.read_text(encoding='utf-8'))
        stage = state.get('stage', '')
        print('本轮目录：' + str(folder))
        print('进度：' + STAGES.get(stage, stage or '状态未登记'))
        if state.get('error'):
            print('原因：' + str(state['error']))
        if stage != 'finalized':
            print('当前不能给出30条最终通过结论。不要重复启动或把历史结果写入此目录。')
            return 2
        path = folder / 'cases.json'
    value = read_ledger(path)
    passed = value.get('passed') is True and all(row['verdict'] == '通过' for row in value['records'])
    if state is not None:
        passed = passed and state.get('passed') is True
    count = sum(row['verdict'] == '通过' for row in value['records'])
    print(f'已记录：30条；其中通过：{count}条；其余：{30-count}条。')
    print('整轮结论：' + ('按记录所列V2.1范围通过' if passed else '未取得整轮通过结论'))
    print('注意：NAV-i包含软件测试和指定历史实板记录核查，不是本轮实板故障注入。')
    for row in value['records']:
        if selected and row['id'] != selected:
            continue
        alias = f'YL-FU-{IDS.index(row["id"]) + 1:03d}'
        print(f'\n{alias} / {row["id"]} / {row["title"]}')
        print('结论：' + row['verdict'])
        print('执行方式：' + row['evidence_mode'])
        print('实际记录：' + row['observed'])
        if selected:
            print('原始证据：')
            for source in row.get('evidence', []):
                print('  ' + source)
    return 0 if passed else 2


def main(argv=None):
    parser = argparse.ArgumentParser(description='只读查看HIL测试进度、30条结论或某一条结果；不启动仿真、不发CAN、不烧录。')
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--output', type=Path, help='本次复测的 OUT 目录')
    source.add_argument('--ledger', type=Path, help='只阅读历史归档结果JSON，不作为本次复测')
    parser.add_argument('--case', help='例如 YL-FU-001；不填则列出全部30条')
    args = parser.parse_args(argv)
    try:
        return show(args)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print('无法判定：' + str(error))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
