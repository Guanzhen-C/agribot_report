"""Package saved HIL evidence and render case-by-case records, without hardware I/O."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import shutil
import textwrap

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt

from build_documents import HERE, ROOT, EXPECTED_NAMES, field, format_style, validate


RUNS = [
    ('ackermann_apple', '20261112T113326_d3eff41d'),
    ('tracked_sprayer', '20261112T115629_2e6203ef'),
    ('tracked_shredder', '20261112T121003_535a1379'),
    ('tractor_powershift', '20261112T122204_aaa0f9cc'),
    ('tractor_cvt', '20261112T122746_5b86bf17')
]
STEM = '三类控制器硬件在环测试步骤与实测结果_V2.0_20261112'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pointer(value, path):
    for part in path.lstrip('/').split('/'):
        key = part.replace('~1', '/').replace('~0', '~')
        value = value[int(key)] if isinstance(value, list) else value[key]
    return value


def collect_sources(evidence_dir=None):
    if evidence_dir is not None:
        root = evidence_dir.resolve()
        archived = json.loads((root/'证据索引.json').read_text())['entries']
        sources = {}
        for identity, entry in archived.items():
            if identity == 'E91':
                continue
            path = (root/entry['file']).resolve()
            if not path.is_relative_to(root) or digest(path) != entry['sha256']:
                raise ValueError('Archived evidence mismatch: '+identity)
            sources[identity] = path
        return sources
    archive = ROOT/'.validation/hil_casia_20261112/results'
    sources = {
        'E01': HERE/'reference_run_summary.json',
        'E02': archive/'matrix-r2/qualification.json',
        'E03': archive/'matrix-r3-tractors/qualification.json',
        'E04': archive/'pc-neutral-tx-five.json',
        'E05': archive/'final-real-runtime-check.json',
        'E06': archive/'final-user-config-check.json',
        'E07': archive.parent/'source-deployment-check.json',
        'E08': archive.parent/'final-patch-hash-check.json',
        'E10': archive/RUNS[0][1]/'mission.json',
        'E11': archive/RUNS[0][1]/'harvest.json',
        'E12': archive/RUNS[0][1]/'independent-audit.json',
        'E13': archive/RUNS[0][1]/'navigation-fluency.json',
        'E14': archive/RUNS[0][1]/'transit-gripper-audit.json',
        'E15': archive/RUNS[1][1]/'x5/mission.json',
        'E16': archive/RUNS[1][1]/'recorded-traffic-summary-r2.json',
        'E17': archive/RUNS[2][1]/'x5/mission.json',
        'E18': archive/RUNS[2][1]/'recorded-traffic-summary.json',
        'E30': HERE/'implement_payload_review.json',
        'E80': archive/'matrix-r1/qualification.json',
        'E81': archive/'hardware-runs/20261112T112530_f7ff28ad/pc-runtime.log',
        'E82': archive/RUNS[2][1]/'x5/rdk-runtime.log',
        'E83': archive/RUNS[2][1]/'x5/rdk-mission.log'
    }
    for base, run in [(20, RUNS[3][1]), (25, RUNS[4][1])]:
        sources[f'E{base}'] = archive/run/'x5/mission/result.json'
        for offset, name in [(1, 'powertrain-audit.json'), (2, 'hitch-audit.json'),
                             (3, 'harrow-work.json'), (4, 'status-gap-audit.json')]:
            sources[f'E{base+offset}'] = archive/run/name
    for index, (_, run) in enumerate(RUNS):
        folder = archive/'hardware-runs'/run
        for prefix, name in [('F', 'flash.log'), ('M', 'firmware.json'),
                             ('S', 'stages.jsonl'), ('R', 'real_runtime_checks.jsonl')]:
            sources[f'{prefix}{index:02}'] = folder/name
    sources['P01'] = archive/RUNS[3][1]/'powertrain.png'
    sources['P02'] = archive/RUNS[4][1]/'powertrain.png'
    for identity, source in sources.items():
        if not source.is_file():
            raise FileNotFoundError(f'Missing actual evidence {identity}: {source}')
    return sources


def flash_review(sources, catalog):
    profiles = {item['id']: item for item in catalog['profiles']}
    firmwares = {item['id']: item for item in catalog['firmwares']}
    rows = []
    for index, (name, run) in enumerate(RUNS):
        record = json.loads(sources[f'M{index:02}'].read_text())
        expected = firmwares[profiles[name]['firmware']]
        log = sources[f'F{index:02}'].read_text()
        stages = [json.loads(line) for line in sources[f'S{index:02}'].read_text().splitlines()]
        protection = [json.loads(line) for line in sources[f'R{index:02}'].read_text().splitlines()]
        rows.append({
            'profile': name, 'run_id': run, 'firmware_id': record['id'],
            'sha256': record['sha256'], 'bitrate': record['bitrate'],
            'firmware_matches_catalog': record['sha256'] == expected['sha256'],
            'programming_finished': 'Programming Finished' in log,
            'verified_ok': 'Verified OK' in log,
            'protection_gates': len(protection),
            'all_protection_gates_unchanged': all(row['unchanged'] for row in protection),
            'phase_sequence': [row.get('phase') for row in stages],
            'phase_timestamps_are_original_unix_values': True
        })
    return {'scope': 'Saved flash and stage logs only, not a new flash test', 'runs': rows,
            'all_five_flash_verified': all(row['firmware_matches_catalog'] and
                row['programming_finished'] and row['verified_ok'] for row in rows)}


def verify_passed_branches(records, values):
    passed = {row['id'] for row in records if row['verdict'] == '通过'}
    expected = {'TC-HIL-VCU-001-h', 'TC-HIL-IMP-001-d', 'TC-HIL-IMP-001-e',
                'TC-HIL-IMP-001-h', 'TC-HIL-NAV-001-c', 'TC-HIL-NAV-001-d'}
    assert passed == expected
    for key in ('E21', 'E26'):
        audit = values[key]
        assert len(audit['feedback_payload_matching']) == 4
        assert all(v['received'] > 100 and v['received'] == v['matched']
                   for v in audit['feedback_payload_matching'].values())
        assert audit['rejected_native_frames'] == 0
        assert audit['audit_passed'] is False  # Never turn the composite audit green.
    assert values['E12']['status'] == 'passed' and all(values['E12']['checks'].values())
    assert len(set(values['E11']['harvested'])) == 2
    assert values['E16']['mission_status'] == 'succeeded'
    assert values['E16']['fan_rpm_max'] > 50 and values['E16']['pump_rpm_max'] > 50
    assert max(values['E16']['final_work_rpm']) < 1
    for identity in ('5B2', '5B3'):
        feedback = values['E30'][RUNS[1][1]]['feedback'][identity]
        assert feedback['received_isobus_frames'] > 0
        assert feedback['unmatched_received'] == 0
    for result, hitch in [('E20', 'E22'), ('E25', 'E27')]:
        assert values[result]['passed'] and len(values[result]['stages']) == 5
        assert all(stage['action'] == 'completed' and stage['planned_points'] > 0
                   for stage in values[result]['stages'])
        assert abs(values[hitch]['final_hitch_position']-9000) < 150
        assert values[hitch]['native_sensor_matching_isobus_samples'] == values[hitch]['received_isobus_sensor_samples']
    deploy = json.loads(values['E07']['stdout'])
    assert deploy['mismatches'] == [] and deploy['user_files_changed'] == []
    assert values['E08']['mismatches'] == []
    assert values['E91']['all_five_flash_verified']
    assert all(row['all_protection_gates_unchanged'] for row in values['E91']['runs'])
    assert len(values['E10']['navigation']) == 2
    assert all(row['status'] == 'succeeded' for row in values['E10']['navigation'])
    assert len(values['E16']['navigation_attempts']) == 1
    assert len(values['E18']['navigation_attempts']) == 1 and values['E18']['mission_status'] == 'failed'


def display(value):
    if value is True:
        return 'true'
    if value is False:
        return 'false'
    if isinstance(value, float):
        return f'{value:.9g}'
    if isinstance(value, dict):
        return '\n'.join(key+': '+display(item) for key, item in value.items())
    if isinstance(value, list):
        return '\n'.join(str(i)+'. '+display(item) for i, item in enumerate(value, 1)) if value else '[]'
    return textwrap.fill(str(value), width=24, break_long_words=False, break_on_hyphens=False)


def build(output, evidence_dir=None):
    requirements = json.loads((HERE/'requirements.json').read_text())
    notes = json.loads((HERE/'execution_cases.json').read_text())
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    expected = validate(requirements, catalog)
    assert [case['id'] for case in notes['cases']] == expected
    sources = collect_sources(evidence_dir)
    values = {key: json.loads(path.read_text()) for key, path in sources.items() if path.suffix == '.json'}
    flash = flash_review(sources, catalog)
    values['E91'] = flash
    records = []
    for section in requirements['sections']:
        for case in section['cases']:
            identity = section['test_prefix']+'-'+case['letter']
            note = next(row for row in notes['cases'] if row['id'] == identity)
            assert note['verdict'] in ('通过', '不通过', '未执行')
            assert bool(note['remaining']) == (note['verdict'] != '通过')
            assert all(key in sources or key == 'E91' for key in note['evidence'])
            measurements = []
            for metric in note['metrics']:
                original = pointer(values[metric['evidence']], metric['pointer'])
                observed = original*metric.get('factor', 1) if isinstance(original, (int, float)) and not isinstance(original, bool) else original
                measurements.append(dict(metric, value=observed, original_value=original))
            records.append(dict(note, category=section['name'], title=case['title'],
                                requirement=case, measurements=measurements))
    verify_passed_branches(records, values)
    counts = dict(Counter(row['verdict'] for row in records))
    output.mkdir(parents=True, exist_ok=True)
    evidence = output/'实测证据'
    evidence.mkdir(exist_ok=True)
    index = {}
    for identity, source in sources.items():
        name = source.name if source.name.startswith(identity+'-') else identity+'-'+source.name
        target = evidence/name
        if source.resolve() != target.resolve():
            shutil.copy2(source, target)
        assert digest(target) == digest(source)
        index[identity] = {'file': str(target.relative_to(output)), 'copied_from': str(source),
                           'sha256': digest(target), 'bytes': target.stat().st_size,
                           'kind': 'recorded_run_artifact'}
    generated = evidence/'E91-flash-evidence-review.json'
    generated.write_text(json.dumps(flash, ensure_ascii=False, indent=2)+'\n')
    index['E91'] = {'file': str(generated.relative_to(output)), 'sha256': digest(generated),
                    'bytes': generated.stat().st_size, 'kind': 'saved-log-review-no-new-hardware',
                    'source_ids': [f'{prefix}{i:02}' for i in range(5) for prefix in ('F', 'M', 'S', 'R')]}
    manifest = {'scope': notes['scope'], 'entries': index,
                'raw_can_note': 'Large original CAN traces are not duplicated. Original locations and '
                                'SHA-256 remain in the included audit reports; saved-log multiset review '
                                'does not certify zero packet loss or latency.'}
    (output/'证据索引.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    ledger = {'requirements_version': 'V2.0', 'record_date': notes['record_date'],
              'scope': notes['scope'], 'new_hardware_operations': False,
              'requirement_source_sha256': digest(HERE/'requirements.json'),
              'case_count': 30, 'category_order': EXPECTED_NAMES, 'verdict_counts': counts,
              'records': records, 'evidence_index': '证据索引.json'}
    (output/'逐项实测记录.json').write_text(json.dumps(ledger, ensure_ascii=False, indent=2)+'\n')
    with (output/'30条实测对照表.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['控制器类别', '用例ID', '名称', '完整用例结论', '覆盖范围', '本轮已执行操作',
                         '真实观察结果', '未覆盖与待补测', '证据编号'])
        for row in records:
            writer.writerow([row['category'], row['id'], row['title'], row['verdict'], row['coverage'],
                             '\n'.join(row['actual_steps']), row['finding'], '\n'.join(row['remaining']),
                             ', '.join(row['evidence'])])

    doc = Document()
    doc.core_properties.title = '三类控制器硬件在环测试操作步骤与实测结果'
    doc.core_properties.author = '农机配置与算法仿真软件项目组'
    doc.core_properties.subject = '对应测试需规V2.0，共30条，真实日志复核及待补测记录'
    page = doc.sections[0]
    page.page_width, page.page_height = Cm(21), Cm(29.7)
    page.top_margin, page.bottom_margin = Cm(2.0), Cm(1.9)
    page.left_margin, page.right_margin = Cm(2.2), Cm(2.0)
    format_style(doc.styles['Normal'], 10.5)
    format_style(doc.styles['Title'], 20, True)
    for style, size in [('Heading 1', 15), ('Heading 2', 12), ('Heading 3', 11)]:
        format_style(doc.styles[style], size, True)
        doc.styles[style].paragraph_format.keep_with_next = True
    format_style(doc.styles['Header'], 8)
    format_style(doc.styles['Footer'], 8)
    page.header.paragraphs[0].text = '三类控制器硬件在环测试操作步骤与实测结果  /  对应需规V2.0'
    footer = page.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
    footer.add_run('AGRIBOT-TR-HIL-20261112  |  第 ')
    field(footer, 'PAGE')
    footer.add_run(' 页 / 共 ')
    field(footer, 'NUMPAGES')
    footer.add_run(' 页')
    plain = []

    def para(value, label=None):
        p = doc.add_paragraph()
        if label:
            p.add_run(label+'：').bold = True
        p.add_run(value)
        plain.append((label+'：' if label else '')+value)
        return p

    def heading(value, level=1):
        doc.add_heading(value, level)
        plain.extend(['', value])

    def table(headers, rows, widths):
        item = doc.add_table(rows=1, cols=len(headers))
        item.style = 'Table Grid'
        item.autofit = False
        for cell, value in zip(item.rows[0].cells, headers):
            cell.text = value
            for run in cell.paragraphs[0].runs:
                run.bold = True
            shade = OxmlElement('w:shd')
            shade.set(qn('w:fill'), 'E9EEF0')
            cell._tc.get_or_add_tcPr().append(shade)
        repeat = OxmlElement('w:tblHeader')
        item.rows[0]._tr.get_or_add_trPr().append(repeat)
        for values_ in rows:
            cells = item.add_row().cells
            for cell, value in zip(cells, values_):
                cell.text = str(value)
        for row in item.rows:
            avoid = OxmlElement('w:cantSplit')
            row._tr.get_or_add_trPr().append(avoid)
            for cell, width in zip(row.cells, widths):
                cell.width = Cm(width)
                for p in cell.paragraphs:
                    p.paragraph_format.space_after = Pt(3)
                    for run in p.runs:
                        run.font.size = Pt(9.5)
        plain.append(' | '.join(headers))
        plain.extend(' | '.join(map(str, row)) for row in rows)

    doc.add_paragraph(requirements['product'], style='Subtitle')
    doc.add_heading('三类控制器硬件在环\n测试操作步骤与实测结果', 0)
    para('对应测试需规V2.0；三类各10条，共30条。')
    para(notes['record_date']+'  /  AGRIBOT-TR-HIL-20261112')
    para('本报告为已发生运行的日志复核与执行覆盖记录，不是30条全项通过证明，也不是第三方签字报告。')
    table(['控制器类别', '通过', '不通过', '未执行完整用例', '合计'], [
        [name, sum(row['verdict']=='通过' for row in records if row['category']==name),
         sum(row['verdict']=='不通过' for row in records if row['category']==name),
         sum(row['verdict']=='未执行' for row in records if row['category']==name), 10]
        for name in EXPECTED_NAMES
    ]+[['合计', counts.get('通过', 0), counts.get('不通过', 0), counts.get('未执行', 0), 30]],
          [5.0, 2.0, 2.0, 4.8, 1.5])
    para('“未执行”包括已有正常运行证据、但没有执行完该条所有专项的情况；不等于这台车从未运行。正常分支通过不替代非法值、断链、模式互锁等专项。复测指引和已执行事实在每条中分栏表述。')
    para('执行人及第三方复核签字：待相应人员核验后填写。本文件不代填签名，不把编制日期当作新增试验日期。')
    heading('1 本轮真实运行及操作流程')
    heading('1.1 台架与证据范围', 2)
    para('主机为casia惠普笔记本，Ubuntu 22.04.5及ROS 2 Humble；X5、HPM6750、两端实体USB-CAN及网口连接。Gazebo执行对象和传感器为仿真，底盘经过实体CAN；机械臂仍经网口。所有正式运行均使用独立HIL目录，不改真车工作区。')
    para('本报告读取已经归档的记录，不打开CAN或ROS控制接口，不烧录或启动新任务。只有保存日志的负载匹配及文档检查在本次新增执行；它们是只读复核，不是新的实板轮次。')
    table(['工况', '真实运行编号', '任务结果', '固件波特率'], [
        [name, run.replace('_', '_\n', 1),
         '失败：返程未完成' if name=='tracked_shredder' else
         '任务完成；严格动力检查失败' if name.startswith('tractor_') else '完成并通过原有严格检查',
         str(flash['runs'][index_]['bitrate'])+' bit/s']
        for index_, (name, run) in enumerate(RUNS)
    ], [4.2, 4.2, 4.5, 2.4])
    heading('1.2 实际执行过的启动命令', 2)
    para('以下为当轮实际CLI命令。它与Unity共用启动链路，但不表示本轮逐键执行了Unity界面验收。原输出目录不可重复使用；后续复测须换新目录并重新确认台架安全。本文件不会自动执行下列命令。')
    for command in [
        'cd /home/casia/agribot_report',
        'python3 tools/hil/qualify.py --execute \\',
        '  --profiles ackermann_apple tracked_sprayer tracked_shredder \\',
        '    tractor_powershift tractor_cvt \\',
        '  --output .validation/hil_notebook_20261112/matrix-r2 \\',
        '  --timeout 2400',
        '# 上轮在碎枝失败时停止；拖拉机随后另起同一规范流程：',
        'python3 tools/hil/qualify.py --execute \\',
        '  --profiles tractor_powershift tractor_cvt \\',
        '  --output .validation/hil_notebook_20261112/matrix-r3-tractors \\',
        '  --timeout 2400'
    ]:
        p = para(command)
        p.paragraph_format.space_after = Pt(1)
        for run in p.runs:
            run.font.size = Pt(9)
    para('matrix-r2保存失败并停止；matrix-r3任务层成功不代表其严格动力审计成功。两个判定均保留在E02、E03及E21、E26中。')
    heading('1.3 通用复测步骤G1至G6', 2)
    for value in [
        'G1：断开真实执行器，核对HIL接线、公共地、终端及绑定USB身份；记录两端软件、工况、固件和模型版本。',
        'G2：停止上一HIL，关闭行走与机具授权，启动只读CAN/定位/关节记录器；故障注入只能作用于当前隔离台架。',
        'G3：选择本条适用工况，使用新输出目录经共享工作流启动。观察实际烧录、Verified OK、CAN配置、定位与反馈就绪，再授权。',
        'G4：执行本条操作，分别记录计划输入与实际反馈；正常运行不得夹带未登记故障注入。未执行的操作不能在结果栏写成已发生。',
        'G5：按本条判据和同一时钟核对。CAN匹配、物理动作、动画和定位分别判断；保存完整失败，不只留成功截图。',
        'G6：结束或发生不安全状态后撤销授权、零执行、停止本HIL，确认释放接口后再换组，复核真车目录指纹。'
    ]:
        para(value)
    heading('1.4 本轮原样复用的只读审计', 2)
    para('采摘采用agribot_hil_apple.audit_mission、fluency和gripper_audit读取mission.json、harvest.json、independent-pc.json；动力采用summarize_powertrain.py和summarize_evidence.py读取PC CAN、X5 CAN及mission/samples.jsonl；圆盘耙使用运行中已执行的verify_harrow_work.py只读检查。其结论原样保存，不由本报告重新改阈值。')
    para('新增review_implement_payloads.py仅对保存报文作负载多重集关联。PC与X5观察窗口不同，总数不同不直接当成丢包率；本报告不凭多重集匹配给出跨机时延或零丢包认证。')

    profile_names = {p['id']: p['name'] for p in catalog['profiles']}
    for number, section in enumerate(requirements['sections'], 2):
        doc.add_page_break()
        heading(str(number)+' '+section['name']+'：10条逐项记录')
        for row in (record for record in records if record['category']==section['name']):
            req = row['requirement']
            heading(req['letter']+') '+row['title'], 2)
            para(row['id'], '用例标识')
            para(row['verdict']+'；'+row['coverage'], '完整用例结论')
            para('；'.join(profile_names[p]+'（'+p+'）' for p in req['profiles']), '适用工况')
            para(req['preconditions'], '前置条件')
            para('依次执行G1至G3，确认对应工况及安全前提；然后：'+req['steps']+
                 '按G5保存'+req['evidence']+'最后执行G6。以下是复测指引，不代表尚缺专项已经执行。', '可复现操作步骤')
            para(req['acceptance'], '预期结果与判据')
            para('以下仅列本轮有记录支撑的实际操作。', '已执行事实')
            for index_, value in enumerate(row['actual_steps'], 1):
                para(str(index_)+'. '+value)
            if row['measurements']:
                table(['实际量值', '从证据读取的结果', '单位 / 来源'], [
                    [metric['label'], display(metric['value']),
                     metric.get('unit', '原字段')+' / '+metric['evidence']]
                    for metric in row['measurements']
                ], [4.8, 7.0, 3.5])
            para(row['finding'], '实际结果与解释')
            para('；'.join(row['remaining']) if row['remaining'] else
                 '本条列明的正常操作及反馈已覆盖；不外推为其他条款、故障注入、全组稳定性或真实农艺验收通过。', '未覆盖与后续补测')
            para('；'.join(identity+'：'+Path(index[identity]['file']).name for identity in row['evidence']), '证据编号')
    heading('5 不通过问题与未完整执行清单')
    table(['类别', '不通过项', '已证实问题'], [
        ['整机控制器', 'e、f、j', '动力换挡2个、CVT4个非预期停机样本；五组整体未达到稳定完成要求。'],
        ['自动驾驶导航控制器', 'h', '碎枝返程实际失败；正常运行的短暂保护使全组连续性未通过。']
    ], [4.0, 3.3, 8.0])
    for name in EXPECTED_NAMES:
        pending = [row['id'].rsplit('-', 1)[-1] for row in records
                   if row['category']==name and row['verdict']=='未执行']
        para(name+'：'+', '.join(pending)+'。各自已有正常证据与尚缺操作见对应记录。', '待补测')
    para('当前30条记录结论为通过'+str(counts.get('通过', 0))+'条、不通过'+str(counts.get('不通过', 0))+
         '条、未执行完整用例'+str(counts.get('未执行', 0))+'条。该统计与五组任务4次完成不是同一口径；不能由一次任务成功批量填写用例通过。')
    heading('6 证据核查与归档说明')
    para('同目录“实测证据”保留本次引用的烧录日志、阶段、任务/采摘记录、严格审计、负载复核及两幅真实运行曲线；“证据索引.json”记录每个文件的来源、字节数和SHA-256。原始大体积CAN轨迹未整份复制，其原始位置与哈希保留在相应审计JSON中。')
    para('“逐项实测记录.json”保存30条结构化记录、每个实际量值的证据编号和JSON Pointer；“30条实测对照表.csv”可用表格软件核查。需要签字或开展补测时，应另生成新运行编号并关联原记录，不能覆盖当前失败。')
    para('文件哈希证明归档副本与当前引用原件一致，不独立证明原始试验环境或测量精度。脚本、配置和原始记录仍须结合核查。')
    heading('附录：两种动力工况的真实记录曲线')
    para('以下图片来自原动力审计脚本的同轮采样记录，不是示意图。动力执行对象为标称仿真模型；曲线保留实际过程，不能证明真实发动机/变速箱性能。')
    for identity, title in [('P01', '动力换挡：原始运行曲线'), ('P02', 'CVT：原始运行曲线')]:
        doc.add_page_break()
        heading(title, 2)
        doc.add_picture(str(output/index[identity]['file']), width=Cm(15.2))
        para(identity+'；对应实际运行编号：'+RUNS[3 if identity=='P01' else 4][1]+
             '。原图SHA-256：'+index[identity]['sha256'])
    target = output/(STEM+'.docx')
    doc.save(target)
    (output/(STEM+'.txt')).write_text('\n'.join(plain)+'\n')
    check = {'scope': 'Document generation and saved-evidence verification, no new hardware',
             'case_count': len(records), 'verdict_counts': counts,
             'evidence_files': len(index), 'all_copied_files_byte_identical': True,
             'all_measurements_dereferenced_from_evidence': True,
             'passed_cases_requirements_checked_without_overriding_failed_composite_audits': True}
    (output/'实测报告结构检查.json').write_text(json.dumps(check, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({'document': str(target), **check}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--evidence-dir', type=Path,
                        help='Reuse a delivered evidence index and files; no notebook or hardware needed')
    args = parser.parse_args()
    build(args.output, args.evidence_dir)
