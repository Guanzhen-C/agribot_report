"""Update the existing agency-form layout with two separately recorded fresh phases."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import shutil
import sys
import zipfile

from docx import Document
from docx.shared import Cm

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent/'20261116_hil_templates'))
from fill_templates import physical_cells, set_cell, set_paragraph, set_row, update_fields, PROFILE_LETTERS


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase-a', type=Path, required=True)
    parser.add_argument('--phase-b', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    first, final = [json.loads(p.read_text()) for p in (args.phase_a, args.phase_b)]
    if first['case_count'] != 11 or final['case_count'] != 30:
        raise ValueError('Separate eleven-case and thirty-case result ledgers are required')
    args.output.mkdir(parents=True, exist_ok=False)
    requirements = json.loads((HERE/'scope_v21/requirements.json').read_text())
    current = {s['test_prefix']+'-'+c['letter']:c for s in requirements['sections'] for c in s['cases']}
    if {r['id'] for r in final['records']} != set(current):
        raise AssertionError('Case identity mismatch')
    for ledger in (first, final):
        for path, digest in ledger['input_sha256'].items():
            if sha(Path(path)) != digest:
                raise AssertionError('Evidence changed: '+path)
    source = Path('/home/cgz/Desktop/农机控制器测试计划与用例_现有条件版V1.4_20261116')
    before = {str(p):sha(p) for p in source.glob('*.docx')}
    counts = dict(Counter(r['verdict'] for r in final['records']))
    summary = ('本次先重新执行修订范围的11条，再重新执行全部30条。阶段一：'+
        '、'.join(k+str(v)+'条' for k,v in first['counts'].items())+'；阶段二：'+
        '、'.join(k+str(v)+'条' for k,v in counts.items())+'。')
    summary += ('实板按五种工况分组执行并逐条关联新运行证据，不表示30条各自独立重跑一遍完整路线。'
        'NAV-i按V2.1重新执行原生MPPI软件夹具，并核对指定历史实板失败/恢复记录；不冒充本轮实板故障注入。')
    all_evidence = sorted(set(path for r in final['records'] for path in r['evidence']))
    ids = {path:f'E{i:03d}' for i,path in enumerate(all_evidence, 1)}
    for original in sorted(source.glob('*.docx')):
        document = Document(original)
        body = document.element.body
        dropping = False
        for element in list(body):
            text = ''.join(element.itertext())
            if element.tag.endswith('}p') and '现有条件范围与执行方式' in text:
                dropping = True
            if dropping and not element.tag.endswith('}sectPr'):
                body.remove(element)
        for p in document.paragraphs:
            text = p.text
            if text.startswith('本计划按委托方'):
                text = '本计划沿用已确认需规V2.1和原三类各10条编号，不增加场景对象，不再调整判据。'+summary+'第三方审核、正式执行人员和签字待现场填写。'
            elif text.startswith('编制日期：'):
                text = '编制日期：2026年11月16日；需规V2.1，表单V1.5两阶段重新实测版。'+summary
            elif text.startswith('V2.1修订范围30条记录核对满足') or text.startswith('失败条目：'):
                text = summary+'具体结论以本轮逐项结果为准，旧失败原样保留。'
            elif text.startswith('提交测试计划'):
                text = '提交测试计划、30条测试用例、11条补测和30条整轮复测的独立结果、实际步骤、原始证据索引及缺陷/回归记录。本稿为开发方台架测试，第三方审核和签署待完成。'
            elif text.startswith('历史实际启动方式：'):
                text = ('本轮实际启动方式：在测试机/home/casia/agribot_report中顺序运行受控campaign.py。'
                        '先执行11条涉及的五种正常工况及控制专项，再以phase_b_jobs.json重新启动五种正常工况及完整控制专项。'
                        '每次经共享工作流实际烧录，失败即停，新运行编号和原日志分别保存；CLI运行不冒充Unity逐键验收。')
            elif text.startswith('每项操作描述为复测指引'):
                text = ('每项实测结果仅叙述本轮实际发生的操作；新运行编号、原始证据和SHA-256见本轮索引。'
                        'NAV-i的软件夹具与历史实板核查单独标注。未执行范围不作为通过依据；外部标准适用条款由测评中心确认。')
            elif text.startswith('委托方《三类控制器硬件在环测试需求规格说明》'):
                text = ('委托方《三类控制器硬件在环测试需求规格说明》V2.1及其V2.0原版与修订对照；'
                        '本轮11条补测、30条整轮复测记录及证据索引、工况目录和受控固件。'
                        '外部标准为模板引用，适用条款由测评中心确认，本稿不作标准符合性认证。')
            text = text.replace('本稿修订号1.4', '本稿修订号1.5')
            if text != p.text:
                set_paragraph(p, text)
        set_row(document.tables[0].rows[-1], document.tables[0], ['V1.5重新实测版',
            '冻结V2.1；先补测11条，再完整执行30条，区分实板和软件夹具。',
            '开发方测试', '第三方待审核', '2026.11.16'])
        if '测试用例' in original.name:
            if len(document.tables) != 31:
                raise AssertionError('Agency case-table layout changed')
            for index, (table, record) in enumerate(zip(document.tables[1:], final['records'])):
                case = current[record['id']]
                header = physical_cells(table.rows[0], table)
                set_cell(header[1], record['id'].removeprefix('TC-HIL-')+'\nV2.1本轮记录')
                set_cell(header[3], '开发方自动执行／复核\n第三方待登记')
                set_cell(header[-1], '2026-11-16\n运行编号见证据索引')
                set_cell(physical_cells(table.rows[2], table)[-1], record['category']+'／'+case['title']+'\n'+record['id'])
                set_cell(physical_cells(table.rows[3], table)[-1],
                    '适用：'+'、'.join(PROFILE_LETTERS[p] for p in case['profiles'])+'。'+case['preconditions']+
                    '\n执行方式：'+record['evidence_mode'])
                cells = physical_cells(table.rows[5], table)
                set_cell(cells[1], case['steps']+'\n本轮方式：'+record['evidence_mode'])
                set_cell(cells[-2], case['acceptance'])
                set_cell(cells[-1], record['observed'])
                set_cell(physical_cells(table.rows[6], table)[1], '核对：'+case['evidence'])
                selected = record['evidence']
                displayed = [ids[p] for p in selected[:8]]
                if len(selected) > 8:
                    displayed.append('等共'+str(len(selected))+'份；完整列表见索引')
                set_cell(physical_cells(table.rows[6], table)[-1], '、'.join(displayed))
                set_cell(physical_cells(table.rows[7], table)[1], '核对该条全部适用工况及本轮原始记录，正常清理，保留任何失败和未执行分支。')
                set_cell(physical_cells(table.rows[7], table)[-1], '不覆盖：'+case.get('excluded_scope', '本条原判据不变。'))
                set_cell(physical_cells(table.rows[9], table)[-1], record['verdict']+'（开发方本轮；第三方待审核签署）。\n'+record['evidence_mode'])
                if physical_cells(table.rows[5], table)[-2].text != case['acceptance']:
                    raise AssertionError('Acceptance text changed')
        else:
            matrix = document.tables[1]
            rows = [r for r in matrix.rows if len(physical_cells(r, matrix)) == 4 and physical_cells(r, matrix)[0].text.strip().isdigit()]
            for i, (row, record) in enumerate(zip(rows, final['records']), 1):
                cells = physical_cells(row, matrix)
                set_cell(cells[1], record['title'])
                set_cell(cells[2], '适用：'+'、'.join(PROFILE_LETTERS[p] for p in record['profiles'])+f'；用例YL-FU-{i:03d}')
        document.add_page_break()
        set_paragraph(document.add_paragraph(), '本轮测试与证据', size=14)
        set_paragraph(document.add_paragraph(), summary, size=10.5)
        for phase, ledger in [('先行11条补测', first), ('后续完整30条', final)]:
            set_paragraph(document.add_paragraph(), phase+'：'+str(ledger['counts']), size=11.5)
            for name, run in ledger['physical_runs'].items():
                set_paragraph(document.add_paragraph(), name+' / '+run['run_id']+
                    '；终点实际误差'+format(run['final_error_m'], '.4f')+' m；停车最大速度'+
                    format(run['final_maximum_speed_mps'], '.6f')+' m/s；正常规划'+str(run['normal_planning_actions'])+'次。', size=10.)
        set_paragraph(document.add_paragraph(), '原V2.0及以前失败记录不覆盖。本轮没有增加场景对象；没有改安全租约、Car4原0.30秒状态门限、真实半轴距停车容差或真车CAN协议。未签署第三方结果。', size=10.)
        set_paragraph(document.add_paragraph(), '本轮缺陷修复与回归', size=11.5)
        for text in [
            'Car3：修复PC HIL差速驱动插件在轮子受阻时内部轮速目标反复累积的问题；目标按上一指令向期望值限加速度逼近，复位时清零。独立HIL插件不覆盖系统插件，不改原摩擦、路线、轮廓或X5真车程序。',
            'Car0：修复PC HIL执行桥在断令恢复时可能执行队列中旧正速度的问题。沿用原0.30秒租约，超时后先零执行，收到明确中性命令后才接收新的非零目标；发送侧旧帧可被拒绝，不声明真车ECU已实施同一修复。',
            'Car3机具：第二阶段错通道恢复专项暴露ISOBUS适配器遗漏停止后的零目标握手。修复后先提交全通道中性目标，再接受新鲜合法请求；底层拒绝事件也会清除旧请求并重新握手。未修改底盘线协议或取消安全门限。该阶段失败证据保留，受影响工况另用新编号重测。',
            '测试工具：接口正向检查先匹配6750固件与PC速率，并有界等待内核报告实际安全就绪；不放宽物理接口安全检查。初始化失败、两项生产缺陷原失败轮和后续新运行证据均分开保留。',
        ]:
            set_paragraph(document.add_paragraph(), text, size=10.)
        for key, relative, caption in [
            ('tracked_sprayer:controller', 'spray-existing/original-nozzles.png', '第二阶段：原喷头实际水雾'),
            ('tracked_sprayer:controller', 'spray-existing/existing-occlusion.png', '第二阶段：使用原有场景的喷雾遮挡'),
            ('tractor_cvt:controller', 'harrow-visual/visual-work/rear-right.png', '第二阶段：下放圆盘耙及可见作业带'),
            ('tractor_cvt:controller', 'harrow-visual/visual-raised/rear-right.png', '第二阶段：抬升后保留作业历史')]:
            image = Path(final['selected_run_directories'][key])/relative
            if image.exists():
                document.add_picture(str(image), width=Cm(15.))
                set_paragraph(document.add_paragraph(), caption+'（本轮Gazebo真实窗口截图）。', size=9.)
        update_fields(document)
        target = args.output/original.name.replace('现有条件版V1.4', '重新实测版V1.5')
        document.save(target)
        with zipfile.ZipFile(target) as archive:
            if len(archive.namelist()) != len(set(archive.namelist())):
                raise AssertionError('Duplicate Word ZIP entries')
    for path, digest in before.items():
        if sha(Path(path)) != digest:
            raise AssertionError('Earlier forms were modified')
    for source_path, name in [(args.phase_a, '第一阶段_11条补测结果.json'), (args.phase_b, '第二阶段_30条复测结果.json'),
                              (HERE/'scope_v21/requirements.json', 'requirements.json')]:
        shutil.copy2(source_path, args.output/name)
    evidence = {identity:dict(path=p, sha256=sha(Path(p))) for p,identity in ids.items()}
    (args.output/'本轮证据索引.json').write_text(json.dumps(dict(evidence=evidence,
        cases={r['id']:[ids[p] for p in r['evidence']] for r in final['records']}, sources=before), ensure_ascii=False, indent=2)+'\n')
    with (args.output/'逐条结果.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['用例编号', '类别', '名称', '结论', '执行方式', '实际记录'])
        writer.writerows([r[k] for k in ('id','category','title','verdict','evidence_mode','observed')] for r in final['records'])
    print(json.dumps(dict(counts=counts, output=str(args.output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
