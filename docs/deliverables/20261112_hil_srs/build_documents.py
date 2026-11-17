"""Render the versioned HIL requirements with exactly ten cases per category."""
import argparse
import hashlib
import json
from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
EXPECTED_NAMES = ['整机控制器', '智能机具控制器', '自动驾驶导航控制器']


def validate(data, catalog):
    assert [section['name'] for section in data['sections']] == EXPECTED_NAMES
    known = {profile['id'] for profile in catalog['profiles']}
    ids = []
    for section in data['sections']:
        assert len(section['cases']) == 10
        assert [case['letter'] for case in section['cases']] == list('abcdefghij')
        covered = set()
        for case in section['cases']:
            ids.append(section['test_prefix']+'-'+case['letter'])
            assert case['profiles'] and set(case['profiles']) <= known
            covered.update(case['profiles'])
            for field in ('title', 'preconditions', 'steps', 'acceptance', 'evidence'):
                assert case[field].strip()
        assert covered == known
    assert len(ids) == len(set(ids)) == 30
    return ids


def field(paragraph, instruction):
    value = OxmlElement('w:fldSimple')
    value.set(qn('w:instr'), instruction)
    run = OxmlElement('w:r')
    text = OxmlElement('w:t')
    text.text = '1'
    run.append(text)
    value.append(run)
    paragraph._p.append(value)


def format_style(style, size, bold=False, font='Noto Sans CJK SC'):
    style.font.name = font
    style.font.size = Pt(size)
    style.font.bold = bold
    style.font.color.rgb = RGBColor(0, 0, 0)
    style._element.get_or_add_rPr().get_or_add_rFonts().set(qn('w:eastAsia'), font)
    style.paragraph_format.space_after = Pt(5)
    style.paragraph_format.line_spacing = 1.2


def build(output):
    data = json.loads((HERE/'requirements.json').read_text())
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    ids = validate(data, catalog)
    output.mkdir(parents=True, exist_ok=True)
    stem = '三类控制器硬件在环测试需规_V2.0_20261112'
    doc = Document()
    doc.core_properties.title = data['title']
    doc.core_properties.subject = data['product']
    doc.core_properties.version = data['version']
    doc.core_properties.author = '农机配置与算法仿真软件项目组'
    section = doc.sections[0]
    section.page_width, section.page_height = Cm(21), Cm(29.7)
    section.top_margin, section.bottom_margin = Cm(2.0), Cm(1.9)
    section.left_margin, section.right_margin = Cm(2.2), Cm(2.0)
    format_style(doc.styles['Normal'], 10.5)
    format_style(doc.styles['Title'], 22, True)
    for name, size in [('Heading 1', 15), ('Heading 2', 12), ('Heading 3', 11)]:
        format_style(doc.styles[name], size, True)
        doc.styles[name].paragraph_format.keep_with_next = True
    format_style(doc.styles['Header'], 8)
    format_style(doc.styles['Footer'], 8)
    header = section.header.paragraphs[0]
    header.text = data['title']+'  '+data['version']
    footer = section.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
    footer.add_run(data['document_id']+'  |  第 ')
    field(footer, 'PAGE')
    footer.add_run(' 页 / 共 ')
    field(footer, 'NUMPAGES')
    footer.add_run(' 页')
    text = []

    def paragraph(value, label=None):
        p = doc.add_paragraph()
        if label:
            p.add_run(label+'：').bold = True
        p.add_run(value)
        text.append((label+'：' if label else '')+value)
        return p

    def heading(value, level=1):
        doc.add_heading(value, level=level)
        text.extend(['', value])

    def table(headers, rows, widths=None):
        item = doc.add_table(rows=1, cols=len(headers))
        item.style = 'Table Grid'
        item.alignment = WD_TABLE_ALIGNMENT.CENTER
        if widths:
            item.autofit = False
            for column, width in zip(item.columns, widths):
                column.width = Cm(width)
        for cell, value in zip(item.rows[0].cells, headers):
            cell.text = value
            for run in cell.paragraphs[0].runs:
                run.bold = True
            shading = OxmlElement('w:shd')
            shading.set(qn('w:fill'), 'E9EEF0')
            cell._tc.get_or_add_tcPr().append(shading)
        if widths:
            for cell, width in zip(item.rows[0].cells, widths):
                cell.width = Cm(width)
        repeat = OxmlElement('w:tblHeader')
        item.rows[0]._tr.get_or_add_trPr().append(repeat)
        for values in rows:
            cells = item.add_row().cells
            for cell, value in zip(cells, values):
                cell.text = str(value)
                for p in cell.paragraphs:
                    p.paragraph_format.space_after = Pt(3)
            if widths:
                for cell, width in zip(cells, widths):
                    cell.width = Cm(width)
            avoid_split = OxmlElement('w:cantSplit')
            item.rows[-1]._tr.get_or_add_trPr().append(avoid_split)
        text.append(' | '.join(headers))
        text.extend(' | '.join(map(str, row)) for row in rows)

    doc.add_paragraph(data['product'], style='Subtitle')
    doc.add_heading(data['title'], 0)
    paragraph(data['version']+'  /  三类各10条，共30条')
    paragraph(data['document_id'])
    paragraph(data['date'])
    paragraph('状态：重写的测试需求基线，待逐条执行与签署；不是全项测试通过证明。')
    table(['类别', '需求标识', '独立用例数'],
          [[s['name'], s['requirement_id'], 10] for s in data['sections']], [4.9, 7.4, 3.5])
    paragraph('修订说明：按用户确认名称和顺序编写。旧版独立植保组并入智能机具控制器；新增碎枝、悬挂及动力换挡/CVT纳入三类，不沿用旧版40项统计。')

    heading('1 范围与功能边界')
    for value in data['scope']:
        paragraph(value)
    heading('2 测试准备与统一判定')
    heading('2.1 台架与运行环境', 2)
    table(['项目', '要求'], data['environment'], [3.1, 12.7])
    heading('2.2 五种适用工况', 2)
    names = {profile['id']: profile['name'] for profile in catalog['profiles']}
    algorithms = {
        'Car0': 'FAST-LIVO2+RTK / Smac Hybrid-A* / MPPI Ackermann',
        'Car3': 'FAST-LIVO2+RTK / Smac Hybrid-A* / MPPI DiffDrive',
        'Car4': 'NavSat ESKF / Smac Hybrid-A* / MPPI Ackermann'
    }
    rows = [[p['id'], p['name']+'；'+p['car']+'；'+('苹果园' if p['scene']=='orchard' else '宽地头'),
             algorithms[p['car']]] for p in catalog['profiles']]
    table(['工况ID', '车辆、作业及场景', '定位 / 全局规划 / 控制'], rows, [4.8, 5.0, 6.0])
    paragraph('机械臂经网口；Car3与Car4的农具经ISOBUS及6750转换，行走和机具共用X5侧同一物理CAN。所有工况底盘执行均不允许网口速度旁路。')
    heading('2.3 默认路线及门限', 2)
    paragraph('苹果园默认出生点为(2,36)，1号点(41,36)、2号点(41,32.5)、3号点(3,32.5)，单位m；不增加4号点或自动返回出生点。Car0在完成1、2号通道后按当前清单到(30.70,31.77,π)采摘，再驶至3号点；清单中的接近辅助目标必须一并保留。')
    paragraph('宽地头出生点及朝向以清单为准；阶段目标依次为接近(-16,0)、第一作业行(14,0)、抬升转弯(14,8)、第二作业行(-16,8)、停车(-20,8)。中间阶段按当前专用到达条件，不统一替换成最终半轴距。')
    table(['车型', '最终位置容差', '使用范围'], [
        ['Car0', '0.2632937431 m', '仅最终到点停车；采摘位置0.10 m、航向0.15 rad另判'],
        ['Car3', '0.4185015549 m', '仅最终到点停车，不用于中间点切角'],
        ['Car4', '1.0925620956 m', '仅最终到点停车，作业阶段用冻结专用条件']
    ], [2.1, 4.3, 9.4])
    heading('2.4 量值与证据口径', 2)
    for value in data['measurement_rules']:
        paragraph(value)

    heading('3 测试用例')
    for index, group in enumerate(data['sections'], 1):
        doc.add_page_break()
        heading(f'3.{index} '+group['name'], 2)
        paragraph(group['requirement_id'], '需求标识')
        paragraph(group['purpose'], '测试目标')
        paragraph('本类共10条，a)至j)；方法为T（测试）与D（演示观察）结合。')
        for case in group['cases']:
            test_id = group['test_prefix']+'-'+case['letter']
            heading(case['letter']+') '+case['title'], 3)
            paragraph(test_id, '用例标识')
            paragraph('；'.join(names[p]+'（'+p+'）' for p in case['profiles']), '适用工况')
            for key, label in [('preconditions', '前置条件'), ('steps', '测试操作'),
                               ('acceptance', '预期结果与判定'), ('evidence', '必需证据')]:
                paragraph(case[key], label)

    heading('4 合格性与执行记录')
    for value in data['verdict_rules']:
        paragraph(value)
    table(['记录字段', '填写要求'], [
        ['版本与输入', '文档V2.0、用例ID、工况ID、代码/构建及固件哈希、地图/模型/参数清单'],
        ['实际过程', '执行时间、执行人、前置条件、输入动作、异常注入和恢复过程'],
        ['实际结果', '真实状态、量值、单位、墙钟/仿真时间区分、统计窗口及样本覆盖'],
        ['证据与结论', '原始文件路径及哈希、截图/录像、通过/不通过/未执行/阻塞、问题编号及复测关联']
    ], [3.1, 12.7])
    heading('5 编制时已知实测状态（非本版签署结果）')
    paragraph('以下仅作为2026年11月12日笔记本移机运行参考。五种工况均已实测，四种完成任务，仅采摘和喷水通过当轮完整严格检查；本版新增或重写的专项条件必须重新核对或执行。不能将下表转换为30条已全部通过。')
    result_path = HERE/'reference_run_summary.json'
    if not result_path.is_file():
        raise FileNotFoundError('Actual notebook results are required; do not substitute invented results')
    results = json.loads(result_path.read_text())
    assert results['profiles_tested'] == 5 and results['all_profiles_accepted'] is False
    def run_label(value):
        return value.replace('_', '_\n', 1)
    table(['工况', '实际运行编号', '既有结果与本版限制'], [
        ['阿克曼机械臂采摘', run_label(results['apple']['run_id']), '双果全流程通过；独立18项、流畅性10项、夹爪7项通过。不等于本版全部故障注入已执行。'],
        ['履带植保喷水', run_label('20261112T115629_2e6203ef'), '全程及实际风机/水泵、CAN字段检查通过；正常运行不替代全部异常与遮挡专项复测。'],
        ['履带碎枝机', run_label('20261112T121003_535a1379'), '机具指令及刀轴反馈正常；返程MPPI失败、Smac起始轮廓占障，导航全流程未通过。'],
        ['拖拉机动力换挡', run_label(results['tractors'][0]['run_id']), '五阶段完成；严格动力审计有2个非预期停机样本，不能判正常稳定性通过。'],
        ['拖拉机CVT', run_label(results['tractors'][1]['run_id']), '五阶段完成；严格动力审计有4个非预期停机样本，不能判正常稳定性通过。']
    ], [3.0, 5.1, 7.7])
    paragraph('动力换挡和CVT分别出现约0.321 s、1.797 s的X5侧ISOBUS状态接收间隔，触发原有0.30 s看门狗后恢复。保护动作不能掩盖正常工况通信抖动；本版要求记录并整改后复测，不降低门限。碎枝机动态代价地图与静态底图差异尚未完全解决，不按“刀轴转动”认定整车导航通过。')
    paragraph('原始记录位于笔记本项目.validation/hil_notebook_20261112/；主机汇总为.validation/hil_casia_20261112/results/notebook-results.json。历史证据继续保存，不覆盖为新的通过结果。')
    heading('6 需求与软件来源追踪')
    table(['类别', '主要实现与检查来源'], [
        ['整机控制器', 'tools/hil/{catalog,cli,agent,workflow,flash_output,protect_real_runtime}.py；agribot_hil_hitch/{protocol,scripts}；hil_profiles.json'],
        ['智能机具控制器', 'agribot_hil_apple/{mission,harvest,tracked_work_wire,tracked_orchard_mission}.py；喷雾及夹爪插件；agribot_hil_hitch悬挂、圆盘耙及地表插件'],
        ['自动驾驶导航控制器', 'agribot_hil_apple/navigation.py及工况launch；agribot_hil_hitch/scripts/mission.py；resources/hil/scenarios/*/rdk/{mission.json,nav2.yaml}'],
        ['原始证据及边界', 'docs/18_hil_integration.md；20261112笔记本任务、严格审计和异常记录；本版requirements.json']
    ], [3.2, 12.6])
    paragraph('本文不执行烧录或仿真；编制、转换格式及排版检查仅是文档检查，不增加任何实板测试通过记录。')

    target = output/(stem+'.docx')
    doc.save(target)
    (output/(stem+'.txt')).write_text('\n'.join(text)+'\n')
    check = {
        'document_version': data['version'], 'category_order': EXPECTED_NAMES,
        'cases_per_category': {s['name']: len(s['cases']) for s in data['sections']},
        'unique_test_case_count': len(ids), 'test_case_ids': ids,
        'profiles': sorted(names), 'requires_new_signed_execution': True,
        'source_sha256': hashlib.sha256((HERE/'requirements.json').read_bytes()).hexdigest(),
        'actual_reference_sha256': hashlib.sha256(result_path.read_bytes()).hexdigest(),
        'files': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (target, output/(stem+'.txt'))}
    }
    paragraphs = Document(target).paragraphs
    recorded = [p.text.removeprefix('用例标识：') for p in paragraphs if p.text.startswith('用例标识：')]
    assert recorded == ids
    (output/'文档结构检查.json').write_text(json.dumps(check, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({'output': str(target), 'categories': EXPECTED_NAMES,
                      'case_count': len(ids)}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    build(parser.parse_args().output)
