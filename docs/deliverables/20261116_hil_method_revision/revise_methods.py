"""Revise measurement methods in the existing forms without changing requirements."""
import csv
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.shared import Pt


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent/'20261116_hil_templates'))
from fill_templates import physical_cells, set_cell, set_paragraph, set_row, update_fields

SOURCE = Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116')
OUTPUT = Path('/home/cgz/Desktop/农机控制器测试计划与用例_方法修订V1.3_待审核_20261116')
WORK = Path('/home/cgz/.local/state/agribot-hil-methods-20261116')
SUFFIX = '_方法修订V1.3_待审核'
METHODS = [
    ('M1', '正常、注入故障和结束清理分别划窗。只有事先登记并实际执行的注入才归故障专项；正常运行中自行出现的保护停机不能改称故障专项通过。'),
    ('M2', 'PC独立记录Gazebo实际状态，X5记录命令和解算结果。用同一仿真时间对齐运动状态；通信新鲜度及看门狗仍使用对应主机单调墙钟，不跨机相减。'),
    ('M3', '动力正常窗口从有效运行命令开始，到显式off/撤销授权前结束。结束后的正常制动不计非预期退出；正常窗口内实际PT_OFF、使能关闭和保护制动须保留。'),
    ('M4', '动力审计不能只依赖X5采样：X5自身停顿可能漏掉实际退出。原审计、PC独立状态及X5状态处理间隔分别保存；任一可靠证据反映违反判据，整项不能判通过。'),
    ('M5', 'Smac首次规划、MPPI失败、动作结束或确认取消、保留剩余原目标后的重规划分别计数。允许的受控恢复可支撑恢复功能，但不自动证明正常运行稳定性通过。'),
    ('M6', '实际结束位置和停车速度独立复核；仍只用原车型半轴距，不隐含额外余量。终点稳定后观察至少5秒仿真时间，缺窗口或采样严重缺失时待补测，不挪用旧轮数据。'),
    ('M7', '相机同仿真时间戳可能包含多帧。保留帧标识、尺寸及完整RGB哈希多重集，严格核对共同完整窗口；不能覆盖同stamp的不同帧，也不放宽像素一致性。'),
    ('M8', '样本数、故障次数、任务尝试数分别记载。负载逐字节匹配只说明收到的负载一致，不直接宣称零丢包。缺失记录、异常间隔和失败尝试均列明。'),
    ('M9', '测试编排使用独立后台单元并落盘日志，维护SSH中断不能伪造任务失败或通过。记录器失效时单列基础设施异常，保留已经完成的真实操作；不足部分按新编号补测。'),
    ('M10', '保留原V2.0需求、30条编号及强制判据。0.30秒状态门限、非法指令拒绝、障碍/碰撞与真实设备隔离不变；改动验收范围须另行评审，不能以本方法稿签署全部通过。'),
]
CASE_NOTES = {
    'TC-HIL-VCU-001-a': '记录每次真实Flash写入及Verified OK；烧录中取消的写入/校验完成与之后安全退出分别留证，不能只记录取消按钮。',
    'TC-HIL-VCU-001-b': '记录绑定序列号、实际USB拓扑及物理TX完成计数；接口UP、USB枚举或send()成功不能替代实际物理收发。',
    'TC-HIL-VCU-001-c': '分别核对方向、ID、DLC、序号及负载；有效匹配、未匹配与非法帧分列，不能由部分负载匹配推断整链路零丢包。',
    'TC-HIL-VCU-001-e': '按M2～M4记录实际挡位/离合器及PC独立动力状态，正常与结束窗口分别审计；X5单侧漏采不能得到通过。',
    'TC-HIL-VCU-001-f': '按M2～M4记录CVT实际传动比、速度和使能，原0.30秒门限不变；记录处理间隔，不误称为跨机传输时延。',
    'TC-HIL-VCU-001-g': '合法输入核对实板应答及配置；每个非法输入与拒绝事件和实际安全状态对应，不能仅凭车停下断定输入已被拒绝。',
    'TC-HIL-VCU-001-h': '四类反馈各自统计接收与匹配数，保留序号、校验及缺失；目标值不能补造实际状态。',
    'TC-HIL-VCU-001-i': '只在登记的注入窗口测保护与恢复；正常任务意外触发同一保护仍归稳定性问题。不能借用保护专项通过覆盖正常工况失败。',
    'TC-HIL-VCU-001-j': '任务完成、途中异常、授权撤销和清理分别给结论；任务成功、严格动力复核和独立记录必须共同满足原判据。',
    'TC-HIL-IMP-001-f': '保存实际运行的原喷头位置、遮挡及停止显示证据；静态模型或不含农具的截图不足以证明水雾效果通过。',
    'TC-HIL-IMP-001-i': '区分几何入土、实际旋转、持续痕迹与非作业区不生成痕迹；单个成功观察不替代所有必需分支。',
    'TC-HIL-IMP-001-j': '行走、喷水、碎枝、悬挂和机械臂通道分别登记适用的断令与停止，不能将某一通道保护外推到全部机具。',
    'TC-HIL-NAV-001-a': '按M2、M7记录各传感器时间推进及相机多重帧一致性，避免同时间戳覆盖造成误报；真实像素差异及内部缺帧仍拒绝。',
    'TC-HIL-NAV-001-d': '按M5统计首次规划与失败后规划，保留全部尝试；新路径必须保留未通过的原目标顺序，不得跳点。',
    'TC-HIL-NAV-001-f': '定位、路径跟踪和最终位置分别记录，用同一仿真时刻比较，并说明匹配覆盖率和缺失；不把车体参考点与农具中心混用。',
    'TC-HIL-NAV-001-h': '按M5分别记录成功行驶段、控制失败、恢复及计划停靠；最终到点不能代替全程连续性核验。',
    'TC-HIL-NAV-001-i': '原动作终态、取消确认、后继Smac请求、剩余目标与次数上限逐项关联；没有故障操作/记录的分支不能补写通过。',
    'TC-HIL-NAV-001-j': '按M6记录实际终点与至少5秒窗口，使用原半轴距和速度门限；采摘停靠及中间点门限不得套用最终容差。',
}
LATEST = {
    'TC-HIL-VCU-001-e': '2026-11-16基线复测powershift-r1：五阶段完成，最终停车复核通过；PC在正常窗口仍捕获1个带速PT_OFF样本，X5自身未采到该瞬间，状态处理最大间隔2.328555294秒。正常稳定性不通过，不能仅据原X5审计true判通过。',
    'TC-HIL-VCU-001-f': '2026-11-16基线复测cvt-r1：五阶段完成、最终停车复核通过；X5带速PT_OFF样本6，PC独立捕获1个，正常窗口状态处理最大间隔0.378402624秒，原0.30秒门限未变，判据未满足。样本数不等同故障次数。',
    'TC-HIL-VCU-001-j': '2026-11-16基线复测：喷水返程MPPI失败后恢复；动力换挡及CVT仍有正常窗口保护退出。每轮均停止并保存原始证据，真车保护指纹无变化；清理通过不替代整任务稳定性通过。',
    'TC-HIL-NAV-001-h': '2026-11-16基线复测sprayer-r3：再次出现一次MPPI失败，保留3号点重规划后最终成功，PC CAN错误及重建均0。两组拖拉机完整路线完成但仍有动力保护退出；整体稳定性仍未通过。',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def revise_document(source, target, cases, records):
    doc = Document(source)
    revision = doc.tables[0]
    set_row(revision.rows[-1], revision, ['1.3草案', '仅修订测量与复测方法，补充11月16日真实基线结果；原强制判据及不通过项保留。',
        '委托方资料整理', '待审核', '2026.11.16'])
    for p in doc.paragraphs:
        if p.text.startswith('整机控制器、智能机具控制器、自动驾驶导航控制器'):
            set_paragraph(p, '整机控制器、智能机具控制器、自动驾驶导航控制器\n测量方法修订V1.3草案／待审核',
                          size=12, align=WD_ALIGN_PARAGRAPH.CENTER)
        if '本次仅填表，没有新增实板试验' in p.text:
            set_paragraph(p, p.text.replace('本次仅填表，没有新增实板试验。',
                '附加2026年11月16日开发方台架复测基线；本方法修订稿尚未进行新版实板验收，不预填第三方通过。'))
        if p.text.startswith('本计划规定'):
            set_paragraph(p, '本计划按V2.0需规保留三类各10条、共30条测试项目。本V1.3为测量方法修订草案，'
                '补充PC/X5独立交叉复核、正常/故障/结束窗口划分和可追溯复测记录；强制判据不变。'
                '2026年11月16日已有基线复测如实列出，不作为新版全部通过结论，第三方签署待实际执行和审核。')
        if p.text.startswith('本稿修订号1.2') or '本稿修订号1.2对应' in p.text:
            set_paragraph(p, p.text.replace('本稿修订号1.2对应需规V2.0', '本稿方法修订号1.3（待审核）沿用需规V2.0'))
    if cases:
        assert len(doc.tables) == 31
        for table, record in zip(doc.tables[1:], records):
            identity = record['id']
            cells = physical_cells(table.rows[5], table)
            note = CASE_NOTES.get(identity, '按M1～M10留存输入、实际状态和覆盖边界；本条原前置、专用操作和验收条件不变。')
            set_cell(cells[1], cells[1].text+'\nV1.3测量补充：'+note)
            if identity in LATEST:
                set_cell(cells[-1], cells[-1].text+'\n最新基线复测：'+LATEST[identity])
    else:
        for table in doc.tables:
            for row in table.rows:
                for cell in physical_cells(row, table):
                    if '动力换挡2次、CVT4次非预期PT_OFF' in cell.text:
                        set_cell(cell, cell.text.replace('动力换挡2次、CVT4次非预期PT_OFF',
                            '旧记录：动力换挡2个、CVT4个非预期PT_OFF样本（不等同独立故障次数）'))
    heading = doc.add_paragraph()
    heading.paragraph_format.page_break_before = True
    heading.paragraph_format.keep_with_next = True
    heading.add_run('方法修订说明 V1.3（待审核）').bold = True
    for identity, method in METHODS:
        p = doc.add_paragraph()
        set_paragraph(p, identity+'　'+method, size=10.5)
        p.paragraph_format.space_after = Pt(5)
    p = doc.add_paragraph()
    set_paragraph(p, '本稿不预设全部通过。若改为仅做功能及故障保护专项，须另行明确缩小的范围、'
        '与原30条的差异及遗留稳定性问题，经评审后执行；不能直接更换旧失败项的结果标签。', size=10.5)
    update_fields(doc)
    doc.save(target)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=False)
    generated = WORK/'generated'
    generated.mkdir(parents=True, exist_ok=True)
    requirements = ROOT/'docs/deliverables/20261112_hil_srs/requirements.json'
    ledger_path = SOURCE/'逐项实测记录.json'
    records = json.loads(ledger_path.read_text())['records']
    assert len(records) == 30
    inputs = [SOURCE/('2026042-01 农机控制器测试'+name+'_填写版.docx') for name in ('计划', '用例')]
    before = {str(p): sha(p) for p in [*inputs, requirements, ledger_path]}
    files = []
    for source, cases in zip(inputs, (False, True)):
        target = generated/(source.stem.replace('_填写版', SUFFIX)+'.docx')
        revise_document(source, target, cases, records)
        files.append(str(target))
    summary = ROOT/'.validation/hil_acceptance_20261116/failed-case-rerun-summary.json'
    refs = [summary, *(ROOT/'.validation/hil_acceptance_20261116'/n/'powertrain-independent-audit.json'
                       for n in ('powershift-r1', 'cvt-r1'))]
    index = dict(status='draft_pending_scope_review', new_hardware_tests_performed=False,
        requirement_version='V2.0 unchanged', method_version='V1.3 draft', case_count=30,
        source_sha256=before, generated=files,
        existing_baseline_evidence={str(p): sha(p) for p in refs},
        note='Existing baseline results are not a new test run or all-pass acceptance report.')
    (OUTPUT/'修订来源与基线证据索引.json').write_text(json.dumps(index, ensure_ascii=False, indent=2)+'\n')
    (OUTPUT/'方法修订说明.txt').write_text('\n\n'.join(k+' '+v for k, v in METHODS)+'\n')
    with (OUTPUT/'30条测量方法修订对照.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['用例标识', '项目', '原基线结论', '原判据是否修改', '测量补充', '11月16日补充基线'])
        for r in records:
            writer.writerow([r['id'], r['title'], r['verdict'], '否', CASE_NOTES.get(r['id'], '公共M1～M10；原专用操作和条件不变'), LATEST.get(r['id'], '按既有证据及待补测范围记录，不预填新版通过')])
    assert all(sha(Path(p)) == digest for p, digest in before.items())
    print(json.dumps(index, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
