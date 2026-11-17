"""Fill the original forms for approved V2.1 scope with traceable evidence modes."""
import argparse
from collections import Counter
from copy import deepcopy
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
sys.path.insert(0, str(HERE.parent/'20261116_hil_retest'))
from build_retest_documents import validate as validate_evidence


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def flat(requirements):
    return {section['test_prefix']+'-'+case['letter']: case
        for section in requirements['sections'] for case in section['cases']}


def intro(document, result):
    summary = 'V2.1修订范围30条记录核对满足：19条沿用已闭合实测，11条按授权后的现有条件范围核对；'
    summary += '其中圆盘耙基础功能本轮另行实板补测，其他主要复核既有实板记录，NAV-i另含明确标注的软件夹具。'
    for paragraph in document.paragraphs:
        text = paragraph.text
        if text.startswith('本计划规定'):
            text = ('本计划按委托方2026年11月16日新要求采用需规V2.1：不增加场景物体，将剩余11条调整为现有条件可执行的范围，'
                '另外19条不变。保留三类各10条、共30条及原模板编号。'+summary+
                '不表示原V2.0全部判据通过，也不表示本轮重新执行了30次实板试验。第三方现场执行和签署待完成。')
        elif text.startswith('编制日期：'):
            text = '编制日期：2026年11月16日；需规V2.1、表单V1.4现有条件版。'+summary+'原V2.0及旧失败不覆盖。'
        elif text.startswith('结果栏已经填写') or text.startswith('历史覆盖汇总：'):
            text = summary+'按每条的“本轮实板补测／既有记录复核／软件夹具”标记读取；不能按本版结果宣称未测的动态障碍或故障注入已通过。'
        elif text.startswith('依委托方V2.0需规'):
            text = ('依委托方确认的V2.1范围逐项核对。只调整明确列出的11条，保留其原V2.0内容及删减范围对照；'
                '按修订范围冻结后不再临时放宽。原安全门限、实体CAN、真车隔离和终点容差不变。')
        elif '本稿修订号1.2对应需规V2.0' in text:
            text = text.replace('本稿修订号1.2对应需规V2.0', '本稿修订号1.4对应需规V2.1，原V2.0另行保留')
        if text != paragraph.text:
            set_paragraph(paragraph, text)


def append_scope(document, changes, ledger):
    document.add_page_break()
    set_paragraph(document.add_paragraph(), '现有条件范围与执行方式', size=14)
    set_paragraph(document.add_paragraph(),
        '本版保留30条编号，仅修订11条。不开启新增物体流程，不开展额外拔线、缺传感器/TF、卸载依赖或夹果强制中断。'
        '原V2.0未完整执行的范围不会因本版调整而变成原版通过。', size=10.5)
    set_paragraph(document.add_paragraph(),
        '本轮新实板补测：Car4/CVT原场景基础作业，运行20261116T115538_54d717fa。'
        '其余条目主要采用11月16日已闭合的实板记录重新核对；NAV-i另有同版本原生MPPI软件夹具。'
        '重插前CAN发送堵塞导致的启动失败原样保留，不列入成功补测；重插后补测及真车文件保护检查通过。', size=10.5)
    for change in changes['changes']:
        record = next(r for r in ledger['records'] if r['id'] == change['id'])
        set_paragraph(document.add_paragraph(), change['id']+' '+record['title'], size=11.5)
        set_paragraph(document.add_paragraph(), '本版不覆盖：'+change['reason'], size=10.)
        set_paragraph(document.add_paragraph(), '执行方式：'+record['evidence_mode']+'。实际记录：'+record['observed'], size=10.)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--harrow-directory', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    base_path = ROOT/'.validation/hil_acceptance_20261116/coverage-post-replug-r1.json'
    base = json.loads(base_path.read_text())
    original_path = ROOT/'docs/deliverables/20261112_hil_srs/requirements.json'
    scope_path = HERE/'scope_v21/requirements.json'
    changes_path = HERE/'scope_v21/11条范围调整对照.json'
    original, requirements = json.loads(original_path.read_text()), json.loads(scope_path.read_text())
    changes, review = json.loads(changes_path.read_text()), json.loads(args.review.read_text())
    old, current = flat(original), flat(requirements)
    changed = {key for key in old if old[key] != current[key]}
    assert len(old) == len(current) == 30 and changed == {v['id'] for v in changes['changes']} and len(changed) == 11
    assert {r['id'] for r in base['records'] if r['verdict'] != '通过'} == changed
    if not review['passed'] or set(review['cases']) != changed:
        raise ValueError('All eleven revised evidence assessments must be complete')
    hardware = args.harrow_directory.parents[1]/'result.json'
    if json.loads(hardware.read_text()).get('passed') is not True:
        raise ValueError('Actual current representative physical campaign did not complete')
    fresh = json.loads((args.harrow_directory/'result.json').read_text())
    if fresh.get('passed') is not True:
        raise ValueError('Actual representative work check failed')
    if json.loads((args.harrow_directory/'visual-review.json').read_text()).get('passed') is not True:
        raise ValueError('Actual current screenshots have not been reviewed')
    ledger = deepcopy(base)
    ledger.update(requirements_version='V2.1', document_version='V1.4',
        scope='用户授权的现有条件版：既有实板记录重新核对＋一组原场景基础补测，不是原V2.0全通过',
        new_30_case_physical_campaign=False, original_requirements_sha256=sha(original_path),
        revised_requirements_sha256=sha(scope_path), revised_case_ids=sorted(changed))
    ledger['figures'] = [dict(path=str((args.harrow_directory/name).resolve().relative_to(ROOT)), caption=caption)
        for name, caption in [('visual-work/rear-right.png', '本轮Car4/CVT原场景基础作业'),
                              ('visual-raised/rear-right.png', '本轮抬升后保留已作业地面显示')]]
    for record in ledger['records']:
        identity = record['id']
        record['previous_v20_verdict'] = record['verdict']
        record['original_v20_acceptance'] = old[identity]['acceptance']
        record['scope_adjusted'] = identity in changed
        record['evidence_mode'] = '既有已闭合实板记录复核'
        if identity in changed:
            value = review['cases'][identity]
            record.update(title=current[identity]['title'], profiles=current[identity]['profiles'],
                acceptance=current[identity]['acceptance'], verdict='通过', remaining=[],
                observed=value['observed'], evidence_mode=value['evidence_mode'],
                excluded_scope=current[identity]['excluded_scope'],
                assertions=[dict(path=str(args.review.resolve().relative_to(ROOT)),
                    field=['cases', identity, 'passed'], expected=True)])
            if identity == 'TC-HIL-IMP-001-i':
                record['evidence_mode'] = '本轮原场景代表性实板补测＋既有记录复核'
                record['assertions'].append(dict(path=str((args.harrow_directory/'result.json').resolve().relative_to(ROOT)),
                    field=['passed'], expected=True))
                record['assertions'].append(dict(path=str((args.harrow_directory/'visual-review.json').resolve().relative_to(ROOT)),
                    field=['passed'], expected=True))
                record['observed'] += ' 重插后20261116T115538_54d717fa再次补测同项基础功能通过，未添加场景物体。'
    evidence = validate_evidence(ledger, requirements)
    evidence_ids = {path: f'E{index:03d}' for index, path in enumerate(sorted(evidence), 1)}
    source = Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116')
    inputs = [source/('2026042-01 农机控制器测试'+name+'_填写版.docx') for name in ('计划', '用例')]
    before = {str(p): sha(p) for p in [original_path, scope_path, changes_path, base_path, *inputs]}
    outputs = []
    for original_form, is_cases in zip(inputs, (False, True)):
        document = Document(original_form)
        intro(document, review)
        set_row(document.tables[0].rows[-1], document.tables[0], ['V1.4现有条件版',
            '需规V2.1仅调整11条范围；不增场景物体；标明已有记录复核与原场景补测。',
            '开发方资料整理', '第三方待审核', '2026.11.16'])
        if is_cases:
            assert len(document.tables) == 31
            for index, (table, record) in enumerate(zip(document.tables[1:], ledger['records'])):
                identity = record['id']
                case = current[identity]
                header = physical_cells(table.rows[0], table)
                set_cell(header[1], identity.removeprefix('TC-HIL-')+'\nV2.1记录')
                set_cell(header[3], '开发方执行／复核\n第三方待登记')
                set_cell(header[-1], '2026-11-16\n具体运行ID见结果及索引')
                cells = physical_cells(table.rows[1], table)
                set_cell(cells[3], 'V2.1\n3.'+str(index//10+1)+' '+case['letter']+')')
                set_cell(physical_cells(table.rows[2], table)[-1], record['category']+'／'+case['title']+'\n'+identity)
                set_cell(physical_cells(table.rows[3], table)[-1],
                    '适用工况：'+'、'.join(PROFILE_LETTERS[p] for p in case['profiles'])+'。'+case['preconditions']+
                    '\n本次执行方式：'+record['evidence_mode']+'。')
                cells = physical_cells(table.rows[5], table)
                set_cell(cells[1], case['steps']+'\n本次采用方式：'+record['evidence_mode']+'，不表示每条操作均重新执行。')
                set_cell(cells[-2], case['acceptance'])
                set_cell(cells[-1], '实际记录：\n'+record['observed'])
                set_cell(physical_cells(table.rows[6], table)[1], '核对并保存：'+case['evidence'])
                set_cell(physical_cells(table.rows[6], table)[-1], '\n'.join(
                    evidence_ids[a['path']]+'〔SHA-256 '+evidence[a['path']]['sha256'][:12]+'〕'
                    for a in record['assertions'])+'\n完整路径见来源与证据索引。')
                set_cell(physical_cells(table.rows[7], table)[1], '核对本条V2.1覆盖范围与实际记录，保留原V2.0判据和历史结论；正常停机后确认清理。')
                set_cell(physical_cells(table.rows[7], table)[-1], '本版不覆盖：'+record.get('excluded_scope', '未修订本条原有判据；本次复核已闭合的实板记录。'))
                set_cell(physical_cells(table.rows[8], table)[-1], '仅按本条V2.1明示范围判断。证据不足不通过；删减范围在对照表中保留，不据此声称原V2.0通过；原安全门限和真车隔离不变。')
                set_cell(physical_cells(table.rows[9], table)[-1], '通过（'+record['evidence_mode']+'；第三方待审核签署）。\n原V2.0记录：'+record['previous_v20_verdict']+'，原版不覆盖。')
                assert physical_cells(table.rows[5], table)[-2].text == case['acceptance']
        else:
            matrix = document.tables[1]
            rows = [r for r in matrix.rows if len(physical_cells(r, matrix)) == 4 and physical_cells(r, matrix)[0].text.strip().isdigit()]
            assert len(rows) == 30
            for index, (row, record) in enumerate(zip(rows, ledger['records']), 1):
                cells = physical_cells(row, matrix)
                set_cell(cells[1], record['title'])
                set_cell(cells[2], '适用：'+'、'.join(PROFILE_LETTERS[p] for p in record['profiles'])+f'；用例YL-FU-{index:03d}')
        append_scope(document, changes, ledger)
        for figure in ledger.get('figures', []):
            document.add_picture(str(ROOT/figure['path']), width=Cm(15.))
            set_paragraph(document.add_paragraph(), figure['caption']+'（本轮HIL实际截图；使用原场景，无新增测试物体。）', size=9.)
        update_fields(document)
        target = args.output/(original_form.stem.replace('_填写版', '_现有条件版V1.4')+'.docx')
        document.save(target)
        with zipfile.ZipFile(target) as archive:
            assert len(archive.namelist()) == len(set(archive.namelist()))
        outputs.append(str(target))
    if any(sha(Path(p)) != digest for p, digest in before.items()):
        raise AssertionError('A baseline or approved scope source was modified')
    (args.output/'逐项结果_需规V2.1.json').write_text(json.dumps(ledger, ensure_ascii=False, indent=2)+'\n')
    for path in (scope_path, changes_path, HERE/'scope_v21/11条范围调整对照.csv', args.review):
        shutil.copy2(path, args.output/path.name)
    index = dict(requirements_version='V2.1', form_version='V1.4', case_count=30, changed_cases=11,
        record_review_satisfied=30, new_30_case_physical_campaign=False,
        new_physical_case='TC-HIL-IMP-001-i / 20261116T115538_54d717fa',
        note='Passing revised evidence assessments does not erase V2.0 gaps; no fabricated new executions or third-party signatures',
        sources=before, record_review_sha256=sha(args.review), evidence=evidence,
        evidence_reference_ids={identity:path for path,identity in evidence_ids.items()},
        raw_record_inputs=review['input_sha256'], generated=outputs)
    (args.output/'来源与证据索引_V2.1.json').write_text(json.dumps(index, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(generated=outputs, records=Counter(r['verdict'] for r in ledger['records']),
        new_30_case_physical_campaign=False), ensure_ascii=False))


if __name__ == '__main__':
    main()
