"""Fill the client's Word forms from frozen requirements and archived HIL evidence.

This program performs document I/O only. It never opens a ROS, CAN or flash interface.
"""
import argparse
from collections import Counter
from copy import deepcopy
import csv
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.table import Table, _Cell
from docx.text.paragraph import Paragraph


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = ROOT / 'docs/deliverables/20261112_hil_srs'
ARCHIVE = Path('/home/cgz/Desktop/三类控制器硬件在环测试需规_20261112')
INPUT = Path('/home/cgz/Documents/xwechat_files/wxid_ndparen4xa7s29_716f/temp/RWTemp/2026-11/9cf3e645fa74f66776f6a8151de0a36e')
WORK = Path('/home/cgz/.local/state/agribot-hil-templates-20261116')
OUTPUT = Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116')
PLAN = '2026042-01 农机控制器测试计划_填写版'
CASES = '2026042-01 农机控制器测试用例_填写版'
NAMES = ['整机控制器', '智能机具控制器', '自动驾驶导航控制器']
PROFILE_LABELS = {
    'ackermann_apple': 'A：Car0机械臂采摘',
    'tracked_sprayer': 'B：Car3植保喷水',
    'tracked_shredder': 'C：Car3碎枝',
    'tractor_powershift': 'D：Car4悬挂圆盘耙／动力换挡',
    'tractor_cvt': 'E：Car4悬挂圆盘耙／CVT',
}
PROFILE_LETTERS = {key: value.split('：')[0] for key, value in PROFILE_LABELS.items()}
MAPPING = {'通过': '通过', '不通过': '不通过', '未执行': '不做判定'}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    return json.loads(path.read_text())


def set_paragraph(p, text, *, size=None, align=None, numbering=True):
    # Keep the form's paragraph/run typography, but remove stale fields and text.
    rpr = deepcopy(p.runs[0]._r.rPr) if p.runs and p.runs[0]._r.rPr is not None else None
    ppr = p._p.pPr
    if rpr is None and ppr is not None and ppr.find(qn('w:rPr')) is not None:
        rpr = deepcopy(ppr.find(qn('w:rPr')))
    for child in list(p._p):
        if child is not ppr:
            p._p.remove(child)
    if not numbering and ppr is not None and ppr.numPr is not None:
        ppr.remove(ppr.numPr)
    run = p.add_run(str(text))
    if rpr is not None:
        run._r.insert(0, rpr)
    if size:
        run.font.size = Pt(size)
        properties = run._r.get_or_add_rPr()
        cs_size = properties.find(qn('w:szCs'))
        if cs_size is None:
            cs_size = OxmlElement('w:szCs')
            properties.append(cs_size)
        cs_size.set(qn('w:val'), str(round(size*2)))
    if align is not None:
        p.alignment = align
    return p


def set_cell(cell, text, *, size=10.5, align=WD_ALIGN_PARAGRAPH.LEFT):
    p = cell.paragraphs[0]
    for child in list(cell._tc):
        if child.tag not in (qn('w:tcPr'), qn('w:p')) or (child.tag == qn('w:p') and child is not p._p):
            cell._tc.remove(child)
    set_paragraph(p, text, size=size, align=align, numbering=False)
    # Some template placeholders are white-on-white or yellow highlighted. New
    # filled values must be printable rather than inheriting those placeholders.
    for properties in [p._p.get_or_add_pPr().find(qn('w:rPr')),
                       *(run._r.get_or_add_rPr() for run in p.runs)]:
        if properties is None:
            continue
        for tag in ['w:color', 'w:highlight', 'w:shd', 'w:vanish', 'w14:textFill']:
            for child in list(properties.findall(qn(tag))):
                properties.remove(child)
        color = OxmlElement('w:color')
        color.set(qn('w:val'), '000000')
        properties.append(color)
    p.paragraph_format.first_line_indent = Pt(0)
    p.paragraph_format.left_indent = Pt(0)
    p.paragraph_format.right_indent = Pt(4)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.keep_with_next = False
    p.paragraph_format.keep_together = False
    return p


def physical_cells(row, table):
    return [_Cell(tc, table) for tc in row._tr.tc_lst]


def set_row(row, table, values, size=10.5):
    cells = physical_cells(row, table)
    assert len(cells) == len(values), (len(cells), len(values))
    for c, value in zip(cells, values):
        set_cell(c, value, size=size)


def after(p, text, *, size=10.5, heading=False):
    new = deepcopy(p._p)
    for child in list(new):
        if child.tag != qn('w:pPr'):
            new.remove(child)
    p._p.addnext(new)
    result = Paragraph(new, p._parent)
    set_paragraph(result, text, size=size, numbering=False)
    ppr = result._p.get_or_add_pPr()
    outline = ppr.find(qn('w:outlineLvl'))
    if outline is None:
        outline = OxmlElement('w:outlineLvl')
        ppr.append(outline)
    outline.set(qn('w:val'), '9')
    pp = result.paragraph_format
    pp.page_break_before = False
    pp.keep_with_next = heading
    pp.space_before = Pt(4 if heading else 0)
    pp.space_after = Pt(4)
    pp.line_spacing = 1.3
    if heading:
        pp.first_line_indent = Pt(0)
        for run in result.runs:
            run.bold = True
    return result


def add_lines(anchor, lines):
    for value in lines:
        anchor = after(anchor, value)
    return anchor


def update_fields(doc):
    settings = doc.settings.element
    update = settings.find(qn('w:updateFields'))
    if update is None:
        update = OxmlElement('w:updateFields')
        settings.append(update)
    update.set(qn('w:val'), 'true')
    for paragraph in doc.paragraphs:
        ppr = paragraph._p.pPr
        level = ppr.find(qn('w:outlineLvl')) if ppr is not None else None
        if level is not None and int(level.get(qn('w:val'))) < 9:
            paragraph.paragraph_format.keep_with_next = True
    for table in doc.tables:
        for row in table.rows:
            for height in row._tr.get_or_add_trPr().findall(qn('w:trHeight')):
                height.set(qn('w:hRule'), 'atLeast')


def revision(doc):
    table = doc.tables[0]
    set_row(table.rows[3], table, ['1.2', '按V2.0需规更新三类各10条；补充历史步骤、结果及待补测边界。',
                                 '委托方资料整理', '待审核', '2026.11.16'])
    for row in table.rows[1:]:
        cell = physical_cells(row, table)[-1]
        set_cell(cell, cell.text, size=9.5)


def resize_table(table, widths):
    assert len(widths) == len(table.columns)
    for col, width in zip(table._tbl.tblGrid.gridCol_lst, widths):
        col.set(qn('w:w'), str(width))
    for row in table.rows:
        offset = 0
        for cell in physical_cells(row, table):
            span = cell._tc.grid_span
            cell._tc.get_or_add_tcPr().get_or_add_tcW().set(qn('w:w'), str(sum(widths[offset:offset+span])))
            offset += span


def trim_cover_blanks(paragraphs, indices):
    # The extra subtitle occupies the space of two pre-existing blank cover lines.
    for i in indices:
        assert not paragraphs[i].text.strip()
        paragraphs[i]._p.getparent().remove(paragraphs[i]._p)


def diagram(path):
    dot = r'''digraph HIL {
 graph [rankdir=TB, bgcolor="white", pad="0.15", nodesep="0.40", ranksep="0.55", dpi=220];
 node [shape=box, style="filled", fillcolor="#f1f4f6", color="#26323d", penwidth=1.2,
       fontname="Noto Sans CJK SC", fontsize=13, margin="0.16,0.14"];
 edge [fontname="Noto Sans CJK SC", fontsize=11, color="#26323d", penwidth=1.2, arrowsize=0.7];
 subgraph cluster_net {
   label="网口：仿真传感器、时钟与机械臂接口"; fontname="Noto Sans CJK SC";
   fontsize=13; color="#6d8494"; style="dashed";
   pc [label="仿真PC／惠普笔记本\nGazebo车辆与场景\n传感器、底盘及机具执行模型", fillcolor="#e7edf1"];
   x5 [label="X5算法板\n定位、Smac、MPPI\n机具任务与ISOBUS客户端", fillcolor="#e7edf1"];
   {rank=same; pc; x5;}
   pc -> x5 [dir=both, label="ROS 2／DDS\n仿真数据与机械臂", color="#476d8a", constraint=false];
 }
 pc_can [label="PC USB-CAN\ngs_usb / can0"];
 hpm [label="HPM6750EVK2\nCAN0 ↔ CAN1\n原生中继／ISOBUS机具转换", fillcolor="#eef2e7"];
 x5_can [label="X5 USB-CAN\nZQWL / 稳定设备路径"];
 {rank=same; pc_can; hpm; x5_can;}
 pc -> pc_can [dir=both, label="USB"];
 pc_can -> hpm [dir=both, label="CAN0", constraint=false];
 hpm -> x5_can [dir=both, label="CAN1", constraint=false];
 x5 -> x5_can [dir=both, label="USB"];
 dap [label="FireDAP烧录器\n每次启动写入并校验固件"];
 pc_can -> dap [style=invis];
 pc -> dap [label="USB", constraint=false];
 dap -> hpm [label="调试接口", constraint=false];
 group [label="A：Car0＋Lite6／苹果园／1 Mbit/s\nB/C：Car3＋喷水或碎枝／苹果园／250 kbit/s\nD/E：Car4＋悬挂圆盘耙／宽地头／250 kbit/s\nD：动力换挡　E：CVT", fillcolor="white", fontsize=11];
 {rank=same; dap; group;}
 hpm -> group [style=dotted, arrowhead=none];
}'''
    dot_path = path.with_suffix('.dot')
    dot_path.write_text(dot)
    subprocess.run(['dot', '-Tpng', str(dot_path), '-o', str(path)], check=True)


def sample_description():
    return (
        '本次样品为“农机控制器”硬件在环系统，配套农机配置与算法仿真软件。'
        '按整机控制器、智能机具控制器、自动驾驶导航控制器三个功能类别验收，每类10条，共30条。'
        'PC运行Gazebo场景、车辆、传感器及执行对象；X5运行定位、Smac规划、MPPI控制及机具任务；'
        'HPM6750通过两段实体CAN完成底盘中继与机具侧ISOBUS转换。'
        '三类是逻辑功能划分，不表示三块独立实体ECU。机械臂仍通过网口控制，不改为CAN。'
    )


def fill_plan(converted, requirements, ledger, output):
    doc = Document(converted)
    paragraphs = list(doc.paragraphs)
    tables = list(doc.tables)
    revision(doc)
    set_paragraph(paragraphs[7], '整机控制器、智能机具控制器、自动驾驶导航控制器\n委托方内容更新稿／2026年11月16日',
                  size=12, align=WD_ALIGN_PARAGRAPH.CENTER)
    trim_cover_blanks(paragraphs, [17, 18, 19])
    replacement = {
        27: '本计划规定“农机控制器”硬件在环测试的范围、环境、方法、步骤、证据及判定规则。'
            '按2026年11月12日V2.0需规形成三类各10条测试项目，供测评中心组织复测与签署使用。'
            '本次仅整理既有证据和更新文档，没有新增烧录或实板试验。',
        29: '本计划适用于整机控制器、智能机具控制器、自动驾驶导航控制器的委托功能测试。'
            '喷水、碎枝、悬挂归入智能机具控制器，动力换挡及CVT归入整机控制器；'
            '不另设发动机标定、植保第四类或额外性能用例。',
        35: '委托方《三类控制器硬件在环测试需求规格说明》V2.0（AGRIBOT-SRS-HIL-20261112）；'
            '《三类控制器硬件在环测试步骤与实测结果》V2.0；逐项实测记录、工况目录及受控固件。'
            '上述外部标准为模板引用，具体适用条款由测评中心确认，本稿不擅自作标准符合性认证。',
        38: sample_description(),
        40: '依委托方V2.0需规逐项验收功能，正常运行、故障保护和界面操作分别记录。'
            '冻结源码、未提交补丁、模型、算法、地图、固件及阈值后执行；不得为使结果通过而临时放宽。'
            '本次历史证据来自2026年11月12日五种工况，不代表全部30条已经完整执行。',
        46: '手工测试与自动记录复核',
        47: '每条用例先执行通用准备G1至G3，再执行本条专用操作，按G5归档证据，最后执行G6清理。'
            '所有注入动作只作用于隔离台架，禁止连接真车行走或机具执行器。',
        48: '测试前评审30条用例及冻结配置，确认必要的正常、非法输入、断链、恢复和停止分支。'
            '未实际执行的操作在结果中标“待补测”，不得以代码存在或一次正常运行替代。',
        49: '记录操作人、实际日期、工况、运行编号、预期结果、实测结果、原始证据及缺陷。'
            '执行人及第三方审核签字由实际人员填写，不代填原模板人员为历史执行人。',
        51: '完整执行且所有适用工况和必需分支符合判据，判“通过”；已观察到违反判据，判“不通过”；'
            '专项未完整执行或证据不足，判“不做判定”。V2.0台账“未执行”在本模板映射为“不做判定”。'
            '安全条件或强制指标不适用“建议级异常”豁免，任务完成也不自动代表稳定性通过。',
        54: '发生失控、非预期运动、身份不符、错误固件、物理总线异常或执行器连接风险时，立即撤销授权、零执行并暂停；保留失败日志。',
        55: '需要修改代码、模型、参数或接线时，先停止本任务并登记变更；真车目录指纹变化时禁止继续。',
        56: '证据记录器失效、关键反馈缺失或版本无法追溯时暂停相关用例，不以界面显示代替实测。',
        57: '因设备、供电、场地或测评人员原因暂停时，记录原因、时间及已完成分支，不合并为一次完整通过。',
        59: '排除原因并重新确认公共地、终端电阻、设备身份、固件哈希、网口及ROS隔离域。',
        60: '复核零执行、现场安全和真车保护指纹，经负责人批准后使用新运行编号重新启动；每次均烧录并校验固件。',
        61: '变更后按影响范围回归相关用例；保留旧结果，不覆写失败记录或重新建立保护基线掩盖变化。',
        64: '按样品登记表确认软件、固件和硬件身份。环境表中的HIL-*为台架逻辑编号，不冒充厂商序列号；'
            '正式样品编号及实物标签由测评中心接收时登记。',
        66: '使用受控工况目录hil_profiles.json及三份6750固件。记录Git版本、dirty补丁哈希、'
            '应用构建、X5独立HIL目录及实际网口/DDS配置。Car0/Car3真实底盘协议及真车工作区不得变更。',
        68: '用例及记录经评审后受控；发生调整应说明需规版本、理由、影响和批准人。本稿修订号1.2对应需规V2.0，'
            '不把旧同名编号的结果直接迁移为新版通过。模板原文档标识与签字栏保留，正式编号由测评中心确认。',
        70: '沿用下表配置库结构。每轮证据目录包含工况清单、flash.log、firmware.json、stages.jsonl、'
            'CAN原始记录、mission.json、独立状态审计和停止记录。时间戳、观察窗口和证据SHA-256应可核对。',
        74: '按批准计划执行全部30条并归档，每条明确通过、不通过或不做判定及原因；'
            '遗留问题须经委托方和测评中心评审，不能将未测分支静默改为通过。达到约定结束条件后形成正式报告。',
        75: '因客户终止或资源不足提前结束，应列出已执行范围、未执行条目、失败项及测试限制，单独签署结束结论。',
        77: '提交测试计划、30条测试用例、逐项步骤与实测结果、原始证据索引、缺陷和回归记录。'
            '本稿已填的结果为开发方历史证据复核，第三方正式执行日期、人员及签字待现场填写。',
        87: '图1　三类控制器共用硬件在环台架与五种工况',
    }
    for index, value in replacement.items():
        set_paragraph(paragraphs[index], value)
    add_lines(paragraphs[46], ['采用受控人工操作、共享启动流程与只读日志核验相结合的方法。'
        'Unity界面与CLI共用HIL启动工作流，但CLI运行不能替代逐键界面验收。只读日志复核不计为新的实板轮次。'])
    add_lines(paragraphs[38], [
        '适用工况：A＝Car0＋Lite6／苹果园；B＝Car3＋植保喷水／苹果园；C＝Car3＋碎枝／苹果园；'
        'D＝Car4＋悬挂圆盘耙／宽地头／动力换挡；E＝同车型机具场景／CVT。',
        'A/B/C使用FAST-LIVO2＋RTK、Smac Hybrid-A*、MPPI；D/E使用NavSat ESKF、Smac Hybrid-A*、MPPI；STVL均关闭。'
        '仅使用这五种受控工况，不向所有车型、场景或任意商业ISOBUS终端外推。',
        'ISOBUS采用固定版AgIsoStack++网络管理和项目自定义应用PGN。'
        '不声明AEF认证、完整UT/TC兼容、真实动力性能、土壤力学、喷雾沉积或真实传感器硬同步。',
    ])
    add_lines(paragraphs[51], [
        '历史覆盖汇总：通过6条、不通过4条、不做判定20条（后者原台账为“未执行完整用例”，其中可有正常分支实测）。'
        '碎枝返程失败，动力换挡/CVT存在非预期PT_OFF，均保留；不写成30条全通过或第三方已签署。',
        '失败条目：整机e、f、j及导航h。通过条目：整机h；机具d、e、h；导航c、d。其余待补测。',
    ])
    common = [
        'G1　断开真实执行器，核对X5－USB-CAN－6750 CAN1及6750 CAN0－USB-CAN－PC接线、公共地和终端；登记设备及版本。',
        'G2　停止上一HIL，关闭行走与机具授权，开启CAN、定位、关节和独立Gazebo状态记录；故障注入只作用于隔离台架。',
        'G3　选择本条工况，用新输出目录经共享启动流程启动；确认实际烧录、Verified OK、CAN配置、定位和反馈就绪后才授权。',
        'G4　执行本条操作，分别记录输入与实际反馈；正常窗口不夹带未登记故障注入。',
        'G5　按本条判据核对同一时钟的证据，分别判CAN、动作、显示和定位，不以目标值代替实际状态。',
        'G6　完成或发生危险时撤销授权、零执行、停止本HIL；确认机具停机、进程/端口释放及真车指纹不变后再换工况。',
        '测量规则：通信租约、间隔、超时按本机单调墙钟；运动及关节按仿真时钟。未同步的跨主机墙钟不得相减得出链路时延；'
        '负载多重集匹配不能代替零丢包证明。仿真慢于实时不自动判失败，也不能延长硬件租约。',
        '最终停车仅看平面位置，容差为实际前后轴距的一半：Car0＝0.2632937431 m、Car3＝0.4185015549 m、Car4＝1.0925620956 m。'
        '不要求最终朝向；该值不得代替采摘停靠或中间点切角门限。定位误差、跟踪误差和终点误差分别统计。',
    ]
    add_lines(paragraphs[49], common)
    add_lines(paragraphs[48], [
        '历史实际启动方式：在惠普笔记本/home/casia/agribot_report中运行tools/hil/qualify.py --execute，'
        'matrix-r2按A、B、C、D、E顺序提交，遇碎枝失败停止；随后matrix-r3-tractors单独提交D、E。'
        '这属于真实CLI执行，不冒充Unity逐键验收；复测必须使用新输出目录，保持失败记录不变。',
    ])

    # Keep the source table's physical merge/border layout; replace the old 5-group index.
    table = tables[1]
    group_row, item_row = deepcopy(table.rows[1]._tr), deepcopy(table.rows[2]._tr)
    for row in list(table.rows)[1:]:
        table._tbl.remove(row._tr)
    ordinal = 0
    for category_index, section in enumerate(requirements['sections'], 1):
        table._tbl.append(deepcopy(group_row))
        set_row(table.rows[-1], table, [f'{category_index}　{section["name"]}（10条）'])
        for case in section['cases']:
            ordinal += 1
            table._tbl.append(deepcopy(item_row))
            set_row(table.rows[-1], table, [str(ordinal), case['title'],
                    '适用：'+ '、'.join(PROFILE_LETTERS[x] for x in case['profiles'])+
                    f'；用例YL-FU-{ordinal:03d}', section['requirement_id']+'\n'+case['letter']+')'])
    set_row(tables[2].rows[1], tables[2], ['—', '本轮不单列',
        '性能相关实测量并入上述功能用例记录；不沿用旧模板5次TCP误差指标，不新增第31条用例。', 'V2.0测量规则'])
    add_lines(paragraphs[79], ['下表为原模板拟安排人员，须由测评中心确认；不是2026年11月12日历史运行的执行人证明，实际签字不得代填。'])
    for i, name in enumerate(['准备阶段', '设计阶段', '实施阶段', '回归测试', '总结阶段'], 1):
        descriptions = ['确认范围、工况、版本及台架安全；本次2026.11.16完成资料更新。',
                        '评审三类各10条用例、证据及判定规则。',
                        '按本用例执行，补齐20条未完整专项，复核4条失败；归档实际步骤。',
                        '对缺陷修复及影响范围回归，另起运行编号，不覆盖原失败。',
                        '汇总真实结果和遗留项，经审核签署正式报告。']
        set_row(tables[5].rows[i], tables[5], [str(i), name, descriptions[i-1], '待确认', '待确认', '待确认'])
    hardware = tables[6]
    set_row(hardware.rows[2], hardware, ['算法板', 'D-Robotics RDK X5', 'HIL-X5-01',
        'aarch64，软件可见内存约6.9 GiB；Ubuntu 22.04.5、ROS 2 Humble；独立HIL工作区，不改真车工作区。'])
    row = deepcopy(hardware.rows[1]._tr)
    hardware.rows[1]._tr.addnext(row)
    set_row(hardware.rows[2], hardware, ['实测笔记本', '惠普笔记本（casia）', 'HIL-PC-02',
        '2026.11.12历史实测主机：i7-9750H、16 GB、RTX 2070；Ubuntu 22.04.5。台式机是另一可复现主机，不混作该轮性能数据。'])
    hardware._tbl.append(deepcopy(hardware.rows[-1]._tr))
    set_row(hardware.rows[-1], hardware, ['调试烧录器', 'FireDAP', 'HIL-DAP-01',
        '绑定串号1d4a21d76150fec8；每次启动按工况写入并校验6750固件，校验前不授权。'])
    add_lines(paragraphs[82], ['HIL-*仅为文档台架逻辑编号。实际设备资产号/出厂号由接收人员核验；'
        'USB-CAN以表中稳定序列号绑定，不能仅凭动态can0或ttyACM编号认定身份。'])
    software = tables[7]
    set_row(software.rows[1], software, ['仿真PC／笔记本', 'Ubuntu 22.04.5',
        'ROS 2 Humble、Gazebo Classic 11、RViz2、Python 3.10；农机配置与算法仿真软件及隔离HIL运行包。补丁版本以实际读回登记。', 'HIL-PC-01/02'])
    set_row(software.rows[2], software, ['X5', 'Ubuntu 22.04.5',
        'ROS 2 Humble；A/B/C：FAST-LIVO2＋RTK；D/E：NavSat ESKF；Smac Hybrid-A*＋MPPI；Lite6规划、CAN复用器和ISOBUS客户端。', 'HIL-X5-01'])
    set_row(software.rows[3], software, ['6750', '裸机固件',
        '三份冻结ELF：采摘中继、履带ISOBUS、拖拉机动力与悬挂；文件标识与SHA-256见受控工况目录。'
        'ISOBUS使用固定版AgIsoStack++及自定义PGN，不能以任意重编译镜像替代。', 'HIL-MCU-01'])
    resize_table(software, [1200, 1550, 4000, 2150])
    add_lines(paragraphs[84], [
        'PC与X5通过网口传输ROS 2仿真传感器、/clock及机械臂接口；Wi-Fi仅作已登记SSH维护，不静默接管数据链路。'
        '记录实际网卡地址、DDS接口及域，不固定复用临时热点IP。',
        'X5 USB-CAN接6750 CAN1，6750 CAN0接PC USB-CAN；A为经典CAN 1 Mbit/s，B/C/D/E为250 kbit/s。'
        'Car0/Car3 ROS域73、Car4域74；vcan73/74仅作X5内部共享，不能替代跨设备物理链路。',
    ])
    picture = output / '硬件在环测试架构.png'
    diagram(picture)
    set_paragraph(paragraphs[86], '')
    paragraphs[86].add_run().add_picture(str(picture), width=Cm(14.7))
    paragraphs[86].alignment = WD_ALIGN_PARAGRAPH.CENTER
    paragraphs[86].paragraph_format.keep_with_next = True
    extra_risks = [
        ['碎枝返程已出现MPPI失败及Smac起点轮廓碰撞', '默认流程未完整完成，不能宣称全工况通过', '保留E17/E82/E83；修复后按导航h及整机j回归，不只改成功标志。'],
        ['Car4正常区间ISOBUS状态间隔超过0.30 s', '动力换挡2次、CVT4次非预期PT_OFF', '保留E21/E24/E26/E29，核查实体链路；不得临时放宽看门狗掩盖稳定性问题。'],
        ['台式机与笔记本性能、时钟及证据范围混用', '得出错误耗时、精度或30条全通过结论', '同机单调时钟统计；按工况分列，6通过/4不通过/20待补测，复测使用新版本和新证据。'],
    ]
    for values in extra_risks:
        tables[9]._tbl.append(deepcopy(tables[9].rows[-1]._tr))
        set_row(tables[9].rows[-1], tables[9], values)
    update_fields(doc)
    doc.core_properties.title = '农机控制器测试计划'
    doc.core_properties.author = '委托方资料整理'
    path = WORK / 'generated' / (PLAN+'.docx')
    path.parent.mkdir(parents=True, exist_ok=True)
    doc.save(path)
    return path


def format_value(metric):
    value = metric['value']
    label = metric['label']
    if isinstance(value, bool):
        formatted = '是' if value else '否'
    elif isinstance(value, float):
        formatted = f'{value:.8g}'
    elif isinstance(value, dict):
        if all(isinstance(v, dict) and 'received' in v for v in value.values()):
            formatted = '；'.join(k+'：'+str(v['matched'])+'/'+str(v['received']) for k, v in value.items())
            label += '（匹配/接收）'
        elif 'unmatched_received' in value:
            formatted = (f"接收{value['received_isobus_frames']}，匹配{value['payload_multiset_matches']}，"
                         f"未匹配{value['unmatched_received']}")
        else:
            formatted = json.dumps(value, ensure_ascii=False, separators=(',', ':'))
    elif isinstance(value, list):
        if len(value) == 0:
            formatted = '无'
        elif all(isinstance(v, (float, int)) for v in value):
            formatted = '、'.join(f'{v:.8g}' for v in value)
        else:
            formatted = json.dumps(value, ensure_ascii=False, separators=(',', ':'))
    else:
        formatted = str(value)
    return label+'：'+formatted+(' '+metric['unit'] if metric.get('unit') else '')+f"〔{metric['evidence']}〕"


def case_card(base, record, number, section_index, doc):
    table = Table(deepcopy(base._tbl), doc._body)
    # Keep all seven grid columns and original field merges, but make room for
    # real results instead of the source form's mostly empty result column.
    widths = [900, 1300, 1250, 350, 1700, 1050, 3141]
    resize_table(table, widths)
    rows = list(table.rows)
    group = record['id'].split('-')[2]
    letter = record['requirement']['letter']
    set_row(rows[0], table, ['记录编号', f'{group}-{letter}\n历史复核', '测试人／岗位名称',
                            '开发方归档复核\n第三方待登记', '测试日期', '历史：2026-11-12\n复测：待登记'], size=10)
    set_row(rows[1], table, ['用例编号', f'YL-FU-{number:03d}', '需求条款',
                            f'V2.0\n3.{section_index} {letter})', '标准条款', '委托功能需规\n外标条款待确认'], size=10)
    set_cell(physical_cells(rows[1], table)[1], f'YL-FU-{number:03d}', size=9)
    set_row(rows[2], table, ['检测项目', record['category']+'／'+record['title']+'\n'+record['id']])
    profiles = '、'.join(PROFILE_LETTERS[x] for x in record['requirement']['profiles'])
    set_row(rows[3], table, ['检测条件', '适用工况：'+profiles+'（定义见前文）。'
        +record['requirement']['preconditions']+'\n先执行通用G1～G3；历史实测及待补测按结果栏分别记录。'])
    set_row(rows[4], table, ['步骤', '操作描述', '预期结果', '实测结果'])
    step_template = deepcopy(rows[5]._tr)
    table._tbl.remove(rows[5]._tr)
    step_rows = []
    for _ in range(3):
        new = deepcopy(step_template)
        rows[6]._tr.addprevious(new)
        step_rows.append(table.rows[len(step_rows)+5])
    req = record['requirement']
    facts = '\n'.join(f'{i}. {text}' for i, text in enumerate(record['actual_steps'], 1))
    set_row(step_rows[0], table, ['1',
        '按G1～G4进入本项，依次执行：'+req['steps']+'\n本栏是复测操作，不代表所有分支已执行。',
        req['acceptance'],
        '历史实际操作：\n'+facts+'\n已知结果：'+record['finding']], size=10.5)
    measured = '\n'.join(format_value(m) for m in record['measurements'])
    set_row(step_rows[1], table, ['2',
        '按G5核对并保存：'+req['evidence']+'\n证据编号对应同目录《证据索引.json》，原始文件在“实测证据”目录。',
        '输入、实际物理状态及返回负载应相互对应；必须使用正确量纲与同一时基。'
        '负载匹配不外推为零丢包或跨机时延。',
        measured if measured else '本条没有足以单列的专项量值；详见上述历史事实及归档证据。'], size=10.5)
    pending = '；'.join(record['remaining']) if record['remaining'] else (
        '本条所列正常操作及反馈有证据；不外推为其他条款、故障专项、全组稳定性或真实农艺验收通过。')
    set_row(step_rows[2], table, ['3',
        '复测时补齐本条未覆盖分支；正常与故障窗口分开留证。完成后按G6撤销授权、零执行和停止，复核残留及真车保护项。',
        '全部适用工况及必需分支均应满足判据；证据不足不得判通过。结束后进入规定安全状态。',
        '覆盖范围：'+record['coverage']+'\n未覆盖／限制：'+pending+'\n证据：'+'、'.join(record['evidence'])], size=10.5)
    set_row(rows[6], table, ['判定准则',
        '本条全部必需分支和适用工况符合预期才通过；观察到违反判据则不通过；专项未完整执行或证据不足则不做判定。'
        '强制安全条件不得按“建议级异常”豁免。'])
    set_row(rows[7], table, ['检测结果', MAPPING[record['verdict']]
        +'（开发方历史证据复核；第三方现场执行及签署待完成）'
        +('。V2.0台账记为“未执行完整用例”，不等于没有正常运行记录。' if record['verdict']=='未执行' else '')])
    # Repeat the form's identity and column labels when a detailed record spans pages.
    for i, row in enumerate(table.rows):
        pr = row._tr.get_or_add_trPr()
        for fixed_height in list(pr.findall(qn('w:trHeight'))):
            fixed_height.set(qn('w:hRule'), 'atLeast')
        if i < 5:
            if pr.find(qn('w:tblHeader')) is None:
                pr.append(OxmlElement('w:tblHeader'))
        else:
            for node in list(pr.findall(qn('w:cantSplit'))):
                pr.remove(node)
            for cell in physical_cells(row, table):
                cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.TOP
    return table


def fill_cases(template, requirements, ledger):
    doc = Document(template)
    old = list(doc.paragraphs)
    revision(doc)
    set_paragraph(old[7], '整机控制器、智能机具控制器、自动驾驶导航控制器\n委托方内容更新稿／2026年11月16日',
                  size=12, align=WD_ALIGN_PARAGRAPH.CENTER)
    trim_cover_blanks(old, [17, 18, 20])
    base = doc.tables[1]
    base_copy = Table(deepcopy(base._tbl), doc._body)
    body = doc.element.body
    marker = base._tbl
    started = False
    for element in list(body):
        if element is marker:
            started = True
        if started and element.tag != qn('w:sectPr'):
            body.remove(element)
    intro = old[25]
    intro = add_lines(intro, [
        '编制日期：2026年11月16日；依据：委托方V2.0需规及2026年11月12日历史运行证据。'
        '本次仅填表，没有新增实板试验。本册三类各10条，共30条，YL-FU序号与TC-HIL编号一一对应。',
        '结果栏已经填写开发方历史实测与覆盖边界：通过6条、不通过4条、不做判定20条。'
        '“不做判定”对应原台账“未执行完整用例”，可包含正常分支证据。第三方执行人、实际日期和签字待现场登记。',
        '工况A：Car0＋Lite6／苹果园；B：Car3＋植保喷水／苹果园；C：Car3＋碎枝／苹果园；'
        'D：Car4＋悬挂圆盘耙／宽地头／动力换挡；E：同车型机具场景／CVT。'
        'A/B/C用FAST-LIVO2＋RTK，D/E用NavSat ESKF；均用Smac Hybrid-A*＋MPPI，STVL关闭。',
        '实体链路：X5 USB-CAN－6750 CAN1／CAN0－PC USB-CAN。A为1 Mbit/s，B～E为250 kbit/s经典CAN。'
        'PC与X5网口承载ROS 2仿真传感器、时钟及机械臂，不以ROS速度旁路底盘。三类为逻辑类别，并非三块独立ECU。',
        'G1：断开真实执行器，确认接线、公共地、终端、绑定设备及版本。G2：停止上一HIL并关闭授权，开启两端CAN及独立状态记录。',
        'G3：用新目录启动本条工况，观察实际烧录、Verified OK、总线配置、定位和反馈就绪后授权。'
        'G4：执行本条操作，正常窗口不夹带未登记故障注入。',
        'G5：核对输入、实际状态与证据，保留失败。G6：撤销行走及机具授权，零执行并停止；确认端口释放和真车指纹不变后换组。',
        '时基：通信租约和间隔用本机单调墙钟，运动和关节用仿真时钟。最终位置容差分别为Car0 0.2632937431 m、'
        'Car3 0.4185015549 m、Car4 1.0925620956 m，仅用于最终停车，不替代采摘停靠及中间点门限。',
        '每项操作描述为复测指引，实测结果仅叙述归档中实际发生的操作；缺少分支均明确待补测。'
        '证据E/F/M/S/R编号见随附索引及实测证据。外部标准具体适用条款由测评中心确认，不沿用未论证的模板条款号。',
    ])
    records = ledger['records']
    number = 0
    for section_index, section in enumerate(requirements['sections'], 1):
        for row in (x for x in records if x['category'] == section['name']):
            number += 1
            p = doc.add_paragraph()
            p.paragraph_format.page_break_before = True
            p.paragraph_format.keep_with_next = True
            p.paragraph_format.space_after = Pt(5)
            run = p.add_run(f'{section_index}　{section["name"]}　{row["requirement"]["letter"]})')
            run.bold = True
            run.font.size = Pt(12)
            run.font.name = '宋体'
            run._r.get_or_add_rPr().get_or_add_rFonts().set(qn('w:eastAsia'), '宋体')
            table = case_card(base_copy, row, number, section_index, doc)
            doc.element.body.insert(len(doc.element.body)-1, table._tbl)
    update_fields(doc)
    for section in doc.sections[1:]:
        number = section._sectPr.find(qn('w:pgNumType'))
        if number is not None and number.get(qn('w:start')) is not None:
            del number.attrib[qn('w:start')]
    doc.core_properties.title = '农机控制器测试用例'
    doc.core_properties.author = '委托方资料整理'
    path = WORK / 'generated' / (CASES+'.docx')
    doc.save(path)
    return path


def collect_evidence(output, ledger):
    index = read_json(ARCHIVE/'证据索引.json')
    for entry in index['entries'].values():
        source = ARCHIVE / entry['file']
        assert digest(source) == entry['sha256'], source
        target = output / entry['file']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    (output/'证据索引.json').write_text(json.dumps(index, ensure_ascii=False, indent=2)+'\n')
    shutil.copy2(ARCHIVE/'逐项实测记录.json', output/'逐项实测记录.json')
    with (output/'30条用例编号与结果对照.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['模板用例编号', '控制器类别', 'V2.0用例标识', '项目', '台账结论', '模板结论', '覆盖范围', '待补测', '证据'])
        for n, row in enumerate(ledger['records'], 1):
            writer.writerow([f'YL-FU-{n:03d}', row['category'], row['id'], row['title'], row['verdict'],
                             MAPPING[row['verdict']], row['coverage'], '；'.join(row['remaining']), '、'.join(row['evidence'])])
    return index


def validate_inputs(requirements, ledger):
    assert [s['name'] for s in requirements['sections']] == NAMES
    assert len(ledger['records']) == 30
    assert Counter(r['verdict'] for r in ledger['records']) == Counter({'通过':6, '不通过':4, '未执行':20})
    expected = [s['test_prefix']+'-'+c['letter'] for s in requirements['sections'] for c in s['cases']]
    assert [r['id'] for r in ledger['records']] == expected
    for s in requirements['sections']:
        assert len(s['cases']) == 10
        for c in s['cases']:
            record = next(r for r in ledger['records'] if r['id']==s['test_prefix']+'-'+c['letter'])
            assert c == record['requirement']
    assert ledger['requirement_source_sha256'] == digest(SOURCE/'requirements.json')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    requirements = read_json(SOURCE/'requirements.json')
    ledger = read_json(ARCHIVE/'逐项实测记录.json')
    validate_inputs(requirements, ledger)
    args.output.mkdir(parents=True, exist_ok=True)
    plan_source = INPUT/'2026042-01 农机控制器测试计划.doc'
    cases_source = INPUT/'2026042-01 农机控制器测试用例.docx'
    sources = {str(path): digest(path) for path in [plan_source, cases_source, SOURCE/'requirements.json', ARCHIVE/'逐项实测记录.json']}
    paths = [fill_plan(WORK/'converted'/plan_source.with_suffix('.docx').name, requirements, ledger, args.output),
             fill_cases(cases_source, requirements, ledger)]
    collect_evidence(args.output, ledger)
    assert all(digest(Path(path)) == sha for path, sha in sources.items())
    metadata = {'compiled_date':'2026-11-16', 'historical_evidence_date':'2026-11-12',
                'hardware_operations':False, 'source_sha256':sources,
                'generated':[str(path) for path in paths],
                'counts':{'categories':3, 'cases_per_category':10, 'passed':6, 'failed':4, 'no_verdict':20},
                'preserved_template_identifiers':['HXMC-T-JH-2026042-02', 'HXMC-T-YL-2026042-02']}
    (args.output/'填写来源与校验.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n')
    (args.output/'填写说明.txt').write_text(
        '农机控制器测试计划与测试用例（按用户提供模板填写）\n'
        '编制日期：2026-11-16；修订号：1.2；依据需规：V2.0。\n\n'
        '原始模板未覆盖或修改，文档内部机构标识、页眉页脚、表格及签字位置沿用。'
        '源文件名为2026042-01，原模板内部标识为2026042-02，未擅自统一，正式编号由机构确认。\n'
        '整机控制器、智能机具控制器、自动驾驶导航控制器各10条。'
        '喷水、碎枝及悬挂并入智能机具；动力换挡、CVT并入整机。不再单列旧发动机标定/植保或新增第31条性能用例。\n'
        '结果来自2026-11-12真实运行及其日志复核：6通过、4不通过、20未完整执行。'
        '模板中后者填写“不做判定”，不是“从未做过正常测试”。\n'
        '本次仅整理文档，不连接ROS/CAN，不烧录，不补造实验、日期、人员签字或数值。'
        '第三方实际执行及审核签字需现场填写。原模板人员安排作为待确认计划，不能视为历史执行人。\n'
        '文件：测试计划.doc用于原版式交付；同名.docx为可编辑版本；测试用例.docx为可编辑填写版；PDF用于版式核对。\n'
        '证据索引.json及实测证据目录仅包含既有运行的原始和只读复核记录；SHA-256可验证。'
        '30条用例编号与结果对照.csv供检索。填写来源与校验.json记录输入摘要与统计。\n'
    )
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
