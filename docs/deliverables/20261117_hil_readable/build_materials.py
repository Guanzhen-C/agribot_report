"""Produce beginner-readable HIL documents without running or changing the bench."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import shutil
import sys

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.opc.constants import RELATIONSHIP_TYPE as RT
from docx.opc.parts.coreprops import CorePropertiesPart
from docx.shared import Cm, Pt


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent / '20261116_hil_templates'))
from fill_templates import physical_cells, set_cell, set_paragraph, set_row, update_fields
sys.path.insert(0, str(HERE.parent / '20261112_hil_srs'))
from build_documents import field, format_style

REQUIREMENTS = HERE.parent / '20261116_hil_existing_conditions/scope_v21/requirements.json'
ARCHIVE = Path.home() / 'Desktop/农机控制器测试计划与用例_两阶段重新实测V1.5_20261116'
SOURCE_FORM = ARCHIVE / '2026042-01 农机控制器测试用例_重新实测版V1.5.docx'
LEDGER = ARCHIVE / '第二阶段_30条复测结果.json'
INDEX = ARCHIVE / '本轮证据索引.json'
REPORT = '2026042-01 农机控制器测试用例与实测报告_操作说明版V1.6'
SRS = '三类控制器硬件在环测试需求规格说明_V2.1_配套说明版'
PROFILE_LABELS = {
    'ackermann_apple': 'A：Car0阿克曼小车＋Lite6机械臂／苹果园',
    'tracked_sprayer': 'B：Car3差速履带车＋植保喷水机／苹果园',
    'tracked_shredder': 'C：Car3差速履带车＋碎枝机／苹果园',
    'tractor_powershift': 'D：Car4拖拉机＋悬挂圆盘耙／宽地头／动力换挡',
    'tractor_cvt': 'E：Car4拖拉机＋悬挂圆盘耙／宽地头／CVT',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def instructions():
    return [
        ('重要说明', [
            ('text', '本材料给不熟悉项目的测试人员使用。先由项目人员准备好台架、软件、权限和冻结版本，测试人员再按C0至C8执行一次整套复测，最后逐条核对30张用例表。不是每打开一张用例表就重新烧录一次。'),
            ('text', '本套30条由终端脚本启动，不需要先在Unity里选择车辆、安装装备或点击启动仿真。脚本使用五种冻结工况并自行打开Gazebo。Unity逐键操作属于另一类界面验收，不在这里混测。'),
            ('text', '本报告的“历史实测结果”来自2026年11月16日V2.1范围下已通过的30条开发方记录。本次只重写说明、补齐需规，没有重新启动仿真或烧录；第三方本次执行结果、执行人和日期需另填。'),
            ('text', '仿真中的车和机具在电脑里，X5与6750是真实板卡。只接HIL台架，严禁接入真车行走、机械臂或机具执行器。不要拔线造故障、添加测试方块、删树、拖动车辆、改目标或放宽参数。'),
            ('text', '命令建议从同文件夹《新测试人员操作步骤.txt》复制整段，不从PDF的折行中复制。不要复制步骤编号，不要在命令前加“$”。英文大小写、引号和空格保持原样。'),
        ]),
        ('C0 打开终端并确认上一轮已经停止', [
            ('text', '在已接台架的惠普笔记本图形桌面，按Ctrl+Alt+T打开终端。先执行下方命令；如果提示目录不存在，交给项目人员处理，不自行改系统。'),
            ('code', 'cd "$HOME/agribot_report"\n/usr/bin/python3 tools/hil/cli.py status'),
            ('text', '输出中phase应为stopped。如果是running、completed、failed等其他状态，先确认这就是本台架的上一会话，再执行停止命令，然后反复执行status，直到phase为stopped。completed不代表场景进程已退出。'),
            ('code', '/usr/bin/python3 tools/hil/cli.py stop\n/usr/bin/python3 tools/hil/cli.py status'),
            ('text', '正在烧录时停止会等写入与校验结束。不要断电、拔烧录器或强杀进程。本步骤的stop命令确实会停止本应用管理的HIL，不能在他人的试验中误用。'),
        ]),
        ('C1 设置本轮文件位置', [
            ('text', '在同一终端复制下方整段。REF是已保存的版本与历史证据；OUT是本轮新数据文件夹。程序会打印OUT，记录这个完整路径。每次重新测试必须生成新的OUT，不能覆盖旧目录。'),
            ('code', 'REF="$PWD/.local/hil/repeat-reference-20261117"\nOUT="$PWD/.validation/hil_repeat_$(date +%Y%m%d_%H%M%S)"\nSCRIPT=docs/deliverables/20261117_hil_repeatable/repeat_suite.py\nVIEW=docs/deliverables/20261117_hil_readable/inspect_results.py\nREVIEW=docs/deliverables/20261117_hil_readable/review_images.py\nprintf "本轮数据目录：%s\\n" "$OUT"'),
            ('text', '以上REF适用于已准备的惠普测试机。若在本机cgz电脑测试，仅把REF改成下面这一行；其他步骤不变。如果两处均没有baseline.json，交给项目人员准备，不自行创建或更新基线。'),
            ('code', 'REF="$HOME/Desktop/HIL30项可重复测试_20261117"'),
            ('text', '从C1到C8始终使用同一终端。关闭终端后变量会丢失，应重新填写刚才记录的实际OUT，不得生成另一个目录后误读结果。'),
        ]),
        ('C2 先预览并检查版本，不会烧录', [
            ('code', '/usr/bin/python3 "$SCRIPT" plan --output "$OUT"\n/usr/bin/python3 "$SCRIPT" check --reference "$REF" --output "$OUT"'),
            ('text', 'plan应提示DRY RUN；check应有passed: true和hardware_outputs: false。冒号后true表示该检查满足，false表示否；这里只是检查准备版本，不是30条实测通过。'),
            ('text', '若出现Frozen code/resource differs或Frozen model/map/parameter differs，说明版本或配置不一致，立即停在这里联系项目人员。不得重建baseline.json、修改哈希或删保护文件强行继续。'),
        ]),
        ('C3 执行一次完整复测，会真实烧录', [
            ('text', '再次确认只接隔离台架且上一HIL已停止，再执行这一条。它会自动切换工况、反复烧录及校验6750、启动Gazebo/X5、采集数据并执行控制专项，不需要人员再手动发送CAN或ROS控制命令。'),
            ('code', '/usr/bin/python3 "$SCRIPT" run --reference "$REF" --output "$OUT" --execute --bench-only'),
            ('text', '执行顺序为辅助检查，然后A正常任务/专项、B正常任务/专项、C正常任务/专项、D正常任务/专项、E正常任务/专项，最后核对原始记录。不同工况的Gazebo可能关闭后重新打开，这是正常切组。'),
            ('text', '辅助检查包括烧录中取消和非法组合拒绝，专项包括计划断令和暂停反馈。因此这些窗口的取消、拒绝、停车是预期现象；正常任务中不明原因卡停仍是不通过，不能混为一谈。'),
            ('text', '等待脚本返回终端提示符。期间可看Gazebo，但不要再点Unity启动/停止、换车、改参数或运行另一个复测命令。笔记本可能慢于实时，不以等了几分钟就自行判失败；也不承诺固定完成时长。'),
        ]),
        ('C4 查看本轮进度，区分等待看图和失败', [
            ('code', '/usr/bin/python3 "$VIEW" --output "$OUT"'),
            ('text', '正常自动执行结束时显示“仍需查看七张截图”。此时尚未形成30条最终结论，查看工具返回非零退出码是提示待完成，不代表可以把结果手填为通过。'),
            ('text', '若显示本轮中断/不通过，或C3异常退出，不执行C6批准和C7汇总来凑通过。保留整个OUT和终端错误，按C9正常停止，交给项目人员分析。修复后使用新OUT重测。'),
            ('text', '如需在运行中查看进度，可另开终端运行同一查看工具，但--output必须填写C1记录的完整实际路径；不要把两轮目录混用。'),
        ]),
        ('C5 转换并逐张查看七张真实截图', [
            ('code', '/usr/bin/python3 "$SCRIPT" render --output "$OUT"\nxdg-open "$OUT/physical/b-sprayer-controls/spray-existing"\nxdg-open "$OUT/physical/b-cvt-controls/harrow-visual/visual-work"\nxdg-open "$OUT/physical/b-cvt-controls/harrow-visual/visual-raised"'),
            ('text', '文件管理器打开后双击PNG图片。喷水看3张：original-nozzles.png应从原喷头径向出雾；existing-occlusion.png应被原有明显障碍挡住；pump-stopped.png结合遥测核对停泵后不再新增发射，已有粒子可消散。'),
            ('text', '圆盘耙看4张：visual-work文件夹内rear-right.png、rear-left.png应显示下放作业及痕迹；visual-raised内同名两张应显示机具抬升，原痕迹保留。结合入土、滚筒和作业历史记录，不只凭一张静态图判断运动。'),
            ('text', '若render提示缺ffmpeg或图片缺失，先交给项目人员处理。也可由其将完整本轮目录带回有工具的审阅电脑；不得拿旧图或生成图补齐。任一张不符合就不批准该组。'),
        ]),
        ('C6 登记实际看图人员和观察，不预填通过', [
            ('text', '仅在C5全部实际看过且符合判据后执行。以下read命令会暂停，等你输入姓名或观察内容后按回车；不得只写“通过”，应分别说明喷头、遮挡、停泵和下放/抬升的实际现象。不符合或看不清时停在这里，不用--approve。'),
            ('code', 'read -r -p "实际看图人员姓名：" REVIEWER\nread -r -p "三张喷雾图的实际观察：" SPRAY_NOTES\n/usr/bin/python3 "$REVIEW" --directory "$OUT/physical/b-sprayer-controls/spray-existing" --kind spray --reviewer "$REVIEWER" --notes "$SPRAY_NOTES" --approve\nread -r -p "四张圆盘耙图的实际观察：" HARROW_NOTES\n/usr/bin/python3 "$REVIEW" --directory "$OUT/physical/b-cvt-controls/harrow-visual" --kind harrow --reviewer "$REVIEWER" --notes "$HARROW_NOTES" --approve'),
            ('text', '本版使用专门的人工登记工具，保存你填写的姓名，不沿用旧工具写死的AI审阅者身份。登记会绑定本轮运行编号和图片校验值，不覆盖已有登记；它不等于第三方签字或真实土壤/药液性能认证。'),
        ]),
        ('C7 生成本轮30条最终结果', [
            ('code', '/usr/bin/python3 "$SCRIPT" finalize --reference "$REF" --output "$OUT"\n/usr/bin/python3 "$VIEW" --output "$OUT"'),
            ('text', '只有最终审计和本轮记录都满足时，才显示30条通过。OUT内应有cases.json、cases.csv、30项步骤与结果.txt及final-audit.log。少证据、版本不符、图片未审阅或用旧运行冒充新运行都不能算通过。'),
            ('text', 'NAV-i按已批准范围使用新执行的软件测试加指定历史实板失败/恢复记录核查，不是本轮新实板故障注入。该边界必须随最终结果保留。'),
        ]),
        ('C8 逐条核对与填写本次报告', [
            ('code', '/usr/bin/python3 "$VIEW" --output "$OUT" --case YL-FU-001'),
            ('text', '将末尾编号依次替换为用例表中的YL-FU-001至YL-FU-030。工具会显示对应条款、执行方式、实际结果和原始证据路径。不是再执行一次控制动作。'),
            ('text', '用本轮实际结果填写新的执行日期、执行人、OUT和结论；本报告已有的2026-11-16开发方结果只作历史对照，不直接当作第三方本次结果。遇到任一必需工况失败、未执行或证据缺失，按实际记录，不能整条写通过。'),
        ]),
        ('C9 中断、失败与收尾', [
            ('code', '/usr/bin/python3 tools/hil/cli.py stop\n/usr/bin/python3 tools/hil/cli.py status'),
            ('text', '需要中断时另开终端进入同一项目目录后提交stop；等待phase为stopped。不要断电、拔CAN或强杀烧录器。自动流程正常结束也应确认stopped及无残留授权；不清楚进程状态交给项目人员检查。'),
            ('text', '保留失败目录，不在同一OUT重跑、不删除失败记录、不改判据或直接给速度。五工况的原始数据和真车文件保护检查均要保留。'),
        ]),
    ]


def plain_guide():
    lines = ['控制器硬件在环测试：新测试人员操作步骤', '编写日期：2026-11-17；对应V2.1的30条；不会因阅读本文件自动执行。', '']
    for title, blocks in instructions():
        lines.extend([title, ''])
        for _, value in blocks:
            lines.extend([value, ''])
    return '\n'.join(lines)


def report_intro(doc, anchor):
    def add(text, heading=False, code=False):
        p = doc.add_paragraph()
        set_paragraph(p, text, size=11 if not heading else 13, numbering=False)
        p.paragraph_format.keep_with_next = heading
        p.paragraph_format.space_after = Pt(5)
        p.paragraph_format.line_spacing = 1.2
        if heading:
            for run in p.runs:
                run.bold = True
        if code:
            p.paragraph_format.left_indent = Cm(.2)
            for run in p.runs:
                run.font.name = 'DejaVu Sans Mono'
                run.font.size = Pt(9)
        anchor.addprevious(p._p)
    add('先读这一章：新测试人员公共操作流程', heading=True)
    for title, blocks in instructions():
        add(title, heading=True)
        for kind, text in blocks:
            add(text, code=kind == 'code')
    add('名词对照', heading=True)
    for text in [
        '工况：一组固定的车、机具、场景、算法和任务。HIL：真实控制板连接电脑里的仿真车。CLI：在终端输入命令。',
        '授权：允许执行控制；没有授权必须不动。反馈：执行端实际返回的状态，不是发出去的目标。CAN：板卡之间的真实控制总线。网口：PC与X5传送ROS传感器、时钟及机械臂消息的有线网络。',
        'Smac：全局路径规划器。MPPI：根据路径和障碍生成车辆控制指令。STVL：三维障碍感知层。算法名称stvl_off中的off仅表示关闭额外视觉AI，不是关闭STVL；旧V1.5和冻结使用说明中的“STVL关闭”属于文字勘误，本版按实际启用配置更正，未修改运行程序。',
        'ISOBUS：本项目使用的农机通信网络与应用报文链路；6750将项目应用报文转换成既有机具CAN。本版不声明完整商业终端兼容或AEF认证。',
        '证据编号E001等：同文件夹《证据索引.json》中的文件条目。其路径仍指向原始归档；不复制大模型、不修改原始日志。用例实测栏的“历史”与未来新OUT必须分开。',
    ]:
        add(text)


def make_report(work, data, ledger, reader):
    doc = Document(SOURCE_FORM)
    # The LibreOffice template uses a nonstandard core-properties relation URI.
    # Normalize it before editing metadata so python-docx does not add a duplicate part.
    package = doc.part.package
    for relation in list(package.rels.values()):
        if relation.target_ref == 'docProps/core.xml' and relation.reltype != RT.CORE_PROPERTIES:
            part = relation.target_part
            replacement = CorePropertiesPart.load(part.partname, part.content_type, part.blob, package)
            package.rels.pop(relation.rId)
            package.relate_to(replacement, RT.CORE_PROPERTIES)
    original_rows = list(doc.tables[1:])
    if len(original_rows) != 30:
        raise ValueError('Original agency form must contain exactly thirty case tables')
    guide_written = False
    for p in doc.paragraphs:
        value = p.text
        if '委托方内容更新稿／2026年11月16日' in value:
            value = value.replace('委托方内容更新稿／2026年11月16日', '外行可执行操作说明版V1.6／编写2026年11月17日')
        elif value.startswith('编制日期：'):
            value = '编写日期：2026年11月17日。判据沿用需规V2.1；表单为V1.6操作说明版。历史实测日期为2026年11月16日，本次只改写说明，未重新执行实板测试。'
        elif value.startswith('本次先重新执行修订范围的11条'):
            value = '历史结果：2026年11月16日先完成11条补测，再完成V2.1范围的30条复测。按五工况分组执行，不是30次独立全程。NAV-i含软件测试与历史实板核查，不能写成本轮新故障注入。第三方本次结果需实际执行后另行填写。'
        elif value.startswith('工况A：'):
            value = '工况A：Car0机械臂采摘；B：Car3喷水；C：Car3碎枝，均为苹果园。D/E：Car4悬挂圆盘耙、宽地头，分别为动力换挡/CVT。A/B/C用FAST-LIVO2+RTK，D/E用NavSat ESKF；均用Smac Hybrid-A*、MPPI及STVL几何感知。'
        elif value.startswith(('G1：', 'G3：', 'G5：')):
            if guide_written:
                p._p.getparent().remove(p._p)
                continue
            guide_written = True
            value = '人员按下方C0至C8的公共操作执行；各用例再标明脚本自动做什么、人员看什么和查哪条结果。不需人员手发CAN或自行制造故障。'
        elif value == '本轮测试与证据':
            value = '2026-11-16历史测试与证据'
        elif value == '本轮缺陷修复与回归':
            value = '历史实测发现的缺陷与回归'
        elif value.startswith('第二阶段：') and '本轮Gazebo真实窗口截图' in value:
            value = value.replace('第二阶段：', '2026-11-16历史实测：').replace('本轮Gazebo真实窗口截图', '历史Gazebo真实窗口截图，非本次重拍')
        if value != p.text:
            set_paragraph(p, value, numbering=False)
    set_row(doc.tables[0].rows[-1], doc.tables[0], ['V1.6操作说明版',
        '补齐新手步骤和配套需规；30条编号、V2.1判据与历史结果不变。',
        '开发方整理', '第三方待复测/签署', '2026.11.17'])
    first = next(p for p in doc.paragraphs if p.text.startswith('1\u3000整机控制器'))
    report_intro(doc, first._p)
    cases = [(s, c) for s in data['sections'] for c in s['cases']]
    records = {r['id']: r for r in ledger['records']}
    for index, (table, (section, case)) in enumerate(zip(original_rows, cases), 1):
        identity = section['test_prefix'] + '-' + case['letter']
        key = identity.removeprefix('TC-HIL-').replace('-001-', '-')
        record = records[identity]
        note = reader[key]
        alias = f'YL-FU-{index:03d}'
        clause = f'3.{(index-1)//10 + 1} {case["letter"]})'
        for row_index, row in enumerate(table.rows):
            properties = row._tr.get_or_add_trPr()
            for header in list(properties.findall(qn('w:tblHeader'))):
                properties.remove(header)
            if row_index < 2:
                properties.append(OxmlElement('w:tblHeader'))
        cells = physical_cells(table.rows[0], table)
        set_cell(cells[1], key + '\nV2.1', size=10)
        set_cell(cells[3], '历史：开发方\n本次执行人：待填写', size=10)
        set_cell(cells[-1], '历史：2026-11-16\n本次复测：待填写', size=10)
        cells = physical_cells(table.rows[1], table)
        set_cell(cells[1], alias)
        set_cell(cells[3], '配套需规V2.1\n第' + clause + '条', size=10)
        set_cell(physical_cells(table.rows[2], table)[-1], section['name'] + '／' + case['title'] + '\n' + identity + '\n看懂本条：' + note['purpose'])
        profiles = '、'.join(PROFILE_LABELS[p].split('：')[0] for p in case['profiles'])
        set_cell(physical_cells(table.rows[3], table)[-1], '适用工况：' + profiles + '（车、机具和场景见前文工况说明）。\n' + case['preconditions'] +
            '\n先完成C0至C2；同一整轮只执行一次C3，不需人员自行接线或发CAN。')
        steps = '按C3整轮执行一次；本条无需另启任务。\n' + '\n'.join(f'{i}. {value}' for i, value in enumerate(note['actions'], 1))
        cells = physical_cells(table.rows[5], table)
        set_cell(cells[1], steps)
        set_cell(cells[-2], case['acceptance'])
        set_cell(cells[-1], '2026-11-16开发方历史实测：\n' + record['observed'] + '\n执行方式：' + record['evidence_mode'] + '\n本次复测不得直接照抄本栏。')
        cells = physical_cells(table.rows[6], table)
        set_cell(cells[1], '按C7完成汇总后，按C8查看本条结果：--case填写' + alias + '。完整可复制命令见C8和操作TXT；此操作不控制车辆。')
        set_cell(cells[-2], '查看工具显示本条编号、结论、执行方式和实际记录；原始证据可按打印路径或证据索引核对。必须覆盖本条全部适用工况。')
        cells = physical_cells(table.rows[7], table)
        set_cell(cells[1], '按C8填本次结果。失败按C9停止，保留目录并联系项目人员；不改门限、不覆盖失败。')
        set_cell(cells[-2], '本次结果、日期、执行人应来自新运行；历史通过不能代替本次结论。')
        set_cell(cells[-1], '本条不覆盖：' + case.get('excluded_scope', '仅验收本条明示范围，不外推到实车或未测组合。'))
        set_cell(physical_cells(table.rows[8], table)[-1], '按配套需规V2.1 ' + clause + '的完整判据，不修改数值或范围。证据不足、必需工况失败或未执行时，不得判通过。NAV-i的软件与历史核查边界保留。')
        set_cell(physical_cells(table.rows[9], table)[-1], '历史结论：' + record['verdict'] + '（2026-11-16开发方，V2.1范围）。\n本次复测结论：________；本轮OUT：________；执行人/日期：________。第三方审核和签署另行办理。')
        if physical_cells(table.rows[5], table)[-2].text != case['acceptance']:
            raise AssertionError('A frozen acceptance criterion changed')
    doc.core_properties.version = 'V1.6'
    doc.core_properties.title = '农机控制器测试用例与实测报告（外行可执行操作说明版）'
    update_fields(doc)
    doc.save(work / (REPORT + '.docx'))


def make_srs(work, data, ledger, reader):
    doc = Document()
    section = doc.sections[0]
    section.page_width, section.page_height = Cm(21), Cm(29.7)
    section.top_margin, section.bottom_margin = Cm(2), Cm(1.9)
    section.left_margin, section.right_margin = Cm(2.1), Cm(2)
    for name, size, bold in [('Normal', 10.5, False), ('Title', 22, True),
                              ('Heading 1', 15, True), ('Heading 2', 12, True), ('Heading 3', 11, True)]:
        format_style(doc.styles[name], size, bold)
    for name in ('Heading 1', 'Heading 2', 'Heading 3'):
        doc.styles[name].paragraph_format.keep_with_next = True
    section.header.paragraphs[0].text = '三类控制器硬件在环测试需求规格说明／V2.1配套说明版'
    footer = section.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
    footer.add_run('AGRIBOT-SRS-HIL-20261116-V21／说明整理20261117  |  第 ')
    field(footer, 'PAGE')
    footer.add_run(' 页 / 共 ')
    field(footer, 'NUMPAGES')
    footer.add_run(' 页')
    doc.add_paragraph('三类控制器硬件在环\n测试需求规格说明', style='Title')
    doc.add_paragraph('需规版本：V2.1（判据冻结）\n配套说明整理日期：2026年11月17日\n对应测试用例报告：V1.6操作说明版')
    doc.add_paragraph('整机控制器、智能机具控制器、自动驾驶导航控制器，各10条，共30条。本文补齐阅读和引用依据，不增删用例、不放宽既有判据，不将历史结果冒充本次执行。')
    doc.add_paragraph('编制：项目组\n审核：待填写\n批准：待填写\n第三方复测及签字：待实施')
    doc.add_page_break()
    doc.add_heading('1 文档目的、适用范围与边界', 1)
    doc.add_paragraph('“需求”规定系统必须做到什么；“测试步骤”说明人员怎样检验。测试用例报告每张表的需求条款都能在本文件第3章找到。条款a至j与原V2.1一一对应，原判据逐字保留。')
    for text in data['scope']:
        doc.add_paragraph(text)
    doc.add_paragraph('历史实测结论：2026年11月16日按V2.1范围30条通过；NAV-i为新软件测试加指定历史实板核查，不是本轮实板故障注入。本次文档整理没有重新测试；V2.0未覆盖分支、真实农艺精度和第三方签署不因此自动满足。')
    doc.add_heading('2 系统组成、测试环境和准备条件', 1)
    doc.add_heading('2.1 用通俗语言认识台架', 2)
    for text in [
        '仿真PC或惠普笔记本：显示Gazebo中的车、机具和场景，也模拟传感器和执行部件。X5：真实算法板，运行定位、规划和控制。HPM6750：真实控制/协议转换板，按工况承担整机或机具侧控制。三类控制器是功能分类，不代表三块独立ECU。',
        '控制方向：X5→X5 USB-CAN→6750 CAN1→6750 CAN0→PC USB-CAN→仿真执行对象；反馈沿相反方向返回。PC↔X5的网口承载ROS传感器、时钟和机械臂消息，不能用ROS速度命令绕过底盘实体CAN。',
        '台架必须由熟悉硬件的人准备和确认。外行测试人员无需自行编译固件、安装ROS、变更驱动或更改接线；任何身份、版本、权限不符先停止并联系项目人员。',
    ]:
        doc.add_paragraph(text)
    doc.add_heading('2.2 必需环境和安全条件', 2)
    for label, text in data['environment']:
        doc.add_paragraph(label + '：' + text)
    doc.add_paragraph('人工看图审阅电脑还需能把XWD转换为PNG；既有脚本使用ffmpeg。测试前由管理员补齐依赖或安排完整本轮记录转至审阅电脑，不能因工具缺失用旧图补证据。')
    doc.add_heading('2.3 五种冻结工况', 2)
    for name, label in PROFILE_LABELS.items():
        algorithm = 'FAST-LIVO2+RTK' if name in ('ackermann_apple', 'tracked_sprayer', 'tracked_shredder') else 'NavSat ESKF'
        doc.add_paragraph(label + '；工况ID：' + name + '；定位：' + algorithm + '；Smac Hybrid-A*规划、MPPI控制。')
    doc.add_paragraph('几何障碍感知使用STVL，预备Nav2配置中启用该层。算法ID中的stvl_off分别代表STVL和关闭额外视觉AI，并非关闭STVL。旧表单和冻结操作说明的“STVL均关闭”在本配套说明中勘误，不改动历史证据或冻结程序。')
    doc.add_heading('2.4 自动化执行与人工确认', 2)
    doc.add_paragraph('按用例报告公共流程C0至C8执行一次整轮。脚本自动切换五工况、烧录、记录和测试；人员确认台架、观察车辆、逐张审阅7张新截图并登记真实姓名和观察。只有finalize审核完成才生成30条最终结论。CLI自动化不替代Unity逐键界面验收。')
    doc.add_heading('3 功能需求与逐条验收要求', 1)
    for section_index, section in enumerate(data['sections'], 1):
        doc.add_heading(f'3.{section_index} {section["name"]}（{section["requirement_id"]}）', 2)
        doc.add_paragraph(section['purpose'])
        for case_index, case in enumerate(section['cases'], 1):
            number = (section_index - 1) * 10 + case_index
            identity = section['test_prefix'] + '-' + case['letter']
            key = identity.removeprefix('TC-HIL-').replace('-001-', '-')
            doc.add_heading(f'3.{section_index} {case["letter"]}) {case["title"]}', 3)
            doc.add_paragraph(f'对应：YL-FU-{number:03d}／{identity}。')
            doc.add_paragraph('通俗理解：' + reader[key]['purpose'])
            doc.add_paragraph('适用工况：' + '；'.join(PROFILE_LABELS[p] for p in case['profiles']))
            doc.add_paragraph('前提：' + case['preconditions'])
            doc.add_paragraph('应满足的完整判据：' + case['acceptance'])
            doc.add_paragraph('应保存的证据：' + case['evidence'])
            if case.get('excluded_scope'):
                doc.add_paragraph('不在本条验收范围内：' + case['excluded_scope'])
    doc.add_heading('4 数值、单位和数据判定规则', 1)
    for text in data['measurement_rules']:
        doc.add_paragraph(text)
    doc.add_paragraph('P95表示95%的有效样本不超过该数值。CAN报文编号和PGN是识别报文类别的编号，人员无需手工计算或发送；原始值和逐字节核对由记录器保存。悬挂0至10000是刻度，碎枝3500是控制量，均不能改写成毫米或转速。')
    doc.add_heading('5 用例、需规和证据追溯', 1)
    doc.add_paragraph('测试用例报告V1.6、条款与用例对照.csv和本文件共同使用。完整历史结果及证据索引保留在交付目录，原始记录仍使用原归档路径，不复制大型运行环境。新复测结果必须单独保存，不能用历史“通过”填补本次未执行项目。')
    table = doc.add_table(rows=1, cols=3)
    table.style = 'Table Grid'
    for cell, text in zip(table.rows[0].cells, ['用例编号', '本文件需求条款', '测试名称']):
        cell.text = text
    for number, (section, case) in enumerate([(s, c) for s in data['sections'] for c in s['cases']], 1):
        for cell, text in zip(table.add_row().cells, [f'YL-FU-{number:03d}', f'3.{(number-1)//10+1} {case["letter"]})', case['title']]):
            cell.text = text
    doc.add_heading('6 通过、不通过、未执行及签署', 1)
    doc.add_paragraph('一条用例只有在全部适用工况和必需证据均满足其完整判据时才能判通过。明确违反判据为不通过，尚未执行或证据不足应标待执行/无法判定。正常流程意外停机不能用故障保护专项通过来抵消。')
    doc.add_paragraph('物理流程结束的awaiting_visual_review仅表示等待看图；最终execution.json和cases.json仍需一致。图像或原始文件哈希不符、旧编号冒充新运行、缺少审阅时，不给出30条通过。')
    doc.add_paragraph('本文不预填第三方结果、人员签字或认证结论。测试人员在实际复测后填写新OUT、日期、姓名和结论，正式审核与签署按第三方机构流程办理。')
    doc.core_properties.title = data['title']
    doc.core_properties.version = 'V2.1'
    update_fields(doc)
    doc.save(work / (SRS + '.docx'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(REQUIREMENTS.read_text())
    ledger = json.loads(LEDGER.read_text())
    reader = json.loads((HERE / 'reader_cases.json').read_text())
    source_files = [REQUIREMENTS, LEDGER, INDEX, SOURCE_FORM]
    before = {str(path): sha(path) for path in source_files}
    cases = [(s, c) for s in data['sections'] for c in s['cases']]
    keys = {s['test_prefix'].removeprefix('TC-HIL-').removesuffix('-001') + '-' + c['letter'] for s, c in cases}
    if len(cases) != 30 or len(keys) != 30 or set(reader) != keys or ledger['counts'] != {'通过': 30}:
        raise ValueError('The approved thirty-case scope and successful historical ledger are required')
    for path, digest in ledger['input_sha256'].items():
        if sha(Path(path)) != digest:
            raise ValueError('Historical input changed: ' + path)
    args.work.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    make_report(args.work, data, ledger, reader)
    make_srs(args.work, data, ledger, reader)
    (args.output / '新测试人员操作步骤.txt').write_text(plain_guide(), encoding='utf-8')
    shutil.copy2(LEDGER, args.output / '历史30条结果_20261116.json')
    shutil.copy2(INDEX, args.output / '证据索引.json')
    shutil.copy2(REQUIREMENTS, args.output / '冻结判据_V2.1.json')
    mapping = []
    for number, (section, case) in enumerate(cases, 1):
        mapping.append(dict(number=f'YL-FU-{number:03d}', case_id=section['test_prefix'] + '-' + case['letter'],
            requirement_id=section['requirement_id'], clause=f'3.{(number-1)//10+1} {case["letter"]})',
            title=case['title'], category=section['name'], acceptance=case['acceptance']))
    with (args.output / '条款与用例对照.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream, lineterminator='\n')
        writer.writerow(['用例编号', '完整用例ID', '需求ID', '需规条款', '类别', '名称'])
        writer.writerows([row[k] for k in ('number', 'case_id', 'requirement_id', 'clause', 'category', 'title')] for row in mapping)
    manifest = dict(document_revision='V1.6', requirements_version='V2.1', cases=30,
        historical_execution_date='2026-11-16', rewritten_on='2026-11-17',
        new_physical_execution=False, hardware_actions=False, criteria_unchanged=True,
        sources=before, traceability=mapping,
        scripts=['inspect_results.py', 'review_images.py'],
        old_stvl_text_corrected_without_runtime_change=True)
    (args.output / '说明改写与条款核验.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    for path, digest in before.items():
        if sha(Path(path)) != digest:
            raise AssertionError('An earlier form, requirement or result was modified')
    print(json.dumps(dict(output=str(args.output), work=str(args.work), cases=30, hardware_actions=False), ensure_ascii=False))


if __name__ == '__main__':
    main()
