"""Fill the original Word layouts from a reviewed, evidence-bound 30-case ledger."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys
import zipfile

from docx import Document
from docx.shared import Cm

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent/'20261116_hil_templates'))
from fill_templates import physical_cells, set_cell, set_paragraph, set_row, update_fields


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(ledger, requirements):
    cases = {section['test_prefix']+'-'+case['letter']: case
             for section in requirements['sections'] for case in section['cases']}
    records = ledger['records']
    if len(records) != 30 or {r['id'] for r in records} != set(cases):
        raise ValueError('Exactly the original 30 case identities must be retained')
    evidence = {}
    for record in records:
        identity = record['id']
        if record['acceptance'] != cases[identity]['acceptance']:
            raise ValueError('Original acceptance text changed: '+identity)
        if record['verdict'] not in ('通过', '不通过', '未完整执行', '阻塞'):
            raise ValueError('Invalid case verdict')
        if record['verdict'] == '通过' and (record['remaining'] or not record['assertions']):
            raise ValueError('Passing a partially covered case is forbidden: '+identity)
        for assertion in record['assertions']:
            path = (ROOT/assertion['path']).resolve()
            if not path.is_relative_to(ROOT/'.validation/hil_acceptance_20261116'):
                raise ValueError('Only the explicit retest evidence tree may be indexed')
            value = json.loads(path.read_text())
            for key in assertion['field']:
                value = value[key]
            matched = value == assertion['expected']
            if not matched and record['verdict'] == '通过':
                raise ValueError('Required actual evidence assertion failed: '+identity+' '+str(path))
            evidence[assertion['path']] = dict(sha256=digest(path), bytes=path.stat().st_size)
    for figure in ledger.get('figures', []):
        path = (ROOT/figure['path']).resolve()
        if not path.is_relative_to(ROOT/'.validation/hil_acceptance_20261116'):
            raise ValueError('Figure is outside the explicit evidence tree')
        evidence[figure['path']] = dict(sha256=digest(path), bytes=path.stat().st_size)
    return evidence


def append_summary(document, ledger):
    document.add_page_break()
    p = document.add_paragraph()
    set_paragraph(p, '2026年11月16日软件修复及逐项复测状态', size=14)
    counts = Counter(r['verdict'] for r in ledger['records'])
    p = document.add_paragraph()
    set_paragraph(p, '本附录为开发方隔离台架实际执行记录，不代填第三方签字。原需规V2.0三类各10条、'
        '共30条及全部判据保持不变。当前状态：'+ '，'.join(k+str(counts[k])+'条' for k in
        ('通过', '不通过', '未完整执行', '阻塞'))+'。原始失败和原1.2历史结果保留，不覆盖、不按当前成功改写。', size=10.5)
    for item in ledger['fixes']:
        set_paragraph(document.add_paragraph(), item, size=10.5)
    set_paragraph(document.add_paragraph(), '测量与证据方法修订（原判据不变）', size=12)
    for item in ledger['methods']:
        set_paragraph(document.add_paragraph(), item, size=10.5)
    for record in ledger['records']:
        text = record['id']+' '+record['title']+'：'+record['verdict']+'。'+record['observed']
        if record['remaining']:
            text += '\n待补齐：'+'；'.join(record['remaining'])+'。'
        set_paragraph(document.add_paragraph(), text, size=10.)
    if ledger.get('figures'):
        document.add_page_break()
        set_paragraph(document.add_paragraph(), '实际运行画面', size=14)
        for figure in ledger['figures']:
            document.add_picture(str(ROOT/figure['path']), width=Cm(15.))
            set_paragraph(document.add_paragraph(), figure['caption']+
                '（隔离HIL台架Gazebo实际截图，不是真车照片；原图及SHA见证据索引。）', size=10.)


def revise_introduction(document, ledger):
    counts = Counter(r['verdict'] for r in ledger['records'])
    current = '，'.join(k+str(counts[k])+'条' for k in ('通过', '不通过', '未完整执行', '阻塞'))
    for paragraph in document.paragraphs:
        text = paragraph.text
        if '本次仅整理既有证据和更新文档，没有新增烧录或实板试验。' in text:
            text = text.replace('本次仅整理既有证据和更新文档，没有新增烧录或实板试验。',
                '原V1.2为历史资料整理；本次V1.3补充2026年11月16日隔离台架实际烧录、软件修复及复测记录。')
        if '本次历史证据来自2026年11月12日五种工况' in text:
            text = text.replace('本次历史证据来自2026年11月12日五种工况',
                '原历史证据来自2026年11月12日五种工况；本轮新增实际记录见逐项结果及附录')
        if text.startswith('历史覆盖汇总：'):
            text = ('V1.2历史覆盖汇总保留：通过6条、不通过4条、不做判定20条。'
                '本轮V1.3实际状态：'+current+'。软件修复后的成功轮与原失败分别记录，'
                '不以单个正常分支代替完整判据，不代填第三方签署。')
        if '本稿修订号1.2对应需规V2.0' in text:
            text = text.replace('本稿修订号1.2对应需规V2.0', '本稿修订号1.3对应原需规V2.0，30条判据保持不变')
        if text.startswith('编制日期：') and '本次仅填表，没有新增实板试验。' in text:
            text = ('编制日期：2026年11月16日；依据：原V2.0需规、11月12日历史证据及11月16日实际台架复测。'
                '本册保留三类各10条，共30条，YL-FU序号与TC-HIL编号一一对应。')
        if text.startswith('结果栏已经填写开发方历史实测与覆盖边界：'):
            text = ('结果栏填写本轮开发方隔离台架实际操作、证据与未覆盖分支：'+current+'。'
                '原V1.2的6通过、4不通过、20不做判定及原始失败不覆盖。'
                '第三方执行人、实际日期和签字待现场登记。')
        if text != paragraph.text:
            set_paragraph(paragraph, text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ledger', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    requirements_path = ROOT/'docs/deliverables/20261112_hil_srs/requirements.json'
    requirements = json.loads(requirements_path.read_text())
    ledger = json.loads(args.ledger.read_text())
    evidence = validate(ledger, requirements)
    source = Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116')
    sources = [source/('2026042-01 农机控制器测试'+name+'_填写版.docx') for name in ('计划', '用例')]
    before = {str(p): digest(p) for p in [requirements_path, *sources]}
    outputs = []
    for original, is_cases in zip(sources, (False, True)):
        document = Document(original)
        revise_introduction(document, ledger)
        revision = document.tables[0]
        set_row(revision.rows[-1], revision, ['1.3复测记录', '原30条判据不变，补充软件修复和真实台架复测；'
            '未完整覆盖项目不预填通过。', '开发方资料整理', '第三方待审核', '2026.11.16'])
        if is_cases:
            if len(document.tables) != 31:
                raise ValueError('Original 30-case Word layout changed')
            for table, record in zip(document.tables[1:], ledger['records']):
                cells = physical_cells(table.rows[5], table)
                if cells[-2].text != record['acceptance']:
                    raise ValueError('Original expected-result cell does not match '+record['id'])
                set_cell(cells[-1], '本轮实际操作及结果：\n'+record['observed'])
                refs = ['%s〔SHA-256 %s〕' % (a['path'], evidence[a['path']]['sha256'][:16])
                        for a in record['assertions']]
                set_cell(physical_cells(table.rows[6], table)[-1],
                    '复测证据：\n'+'\n'.join(dict.fromkeys(refs)) if refs else '本轮完整实测证据尚未取得，不作通过判定。')
                set_cell(physical_cells(table.rows[7], table)[-1],
                    '未覆盖分支：'+'；'.join(record['remaining']) if record['remaining'] else
                    '本条所列必需分支已按证据逐项复核，第三方现场执行及签署待完成。')
                set_cell(physical_cells(table.rows[9], table)[-1],
                    record['verdict']+'（开发方隔离台架复测；不代填第三方签字）。\n原历史结论：'+record['baseline_verdict']+'，原记录保留。')
                header = physical_cells(table.rows[0], table)
                set_cell(header[1], record['id'].replace('TC-HIL-', '')+'\n20261116复测')
                set_cell(header[3], '开发方台架自动执行\n第三方待登记')
                set_cell(header[-1], '2026-11-16\n'+('证据复核完成' if record['verdict'] == '通过' else '未覆盖分支待补测'))
                if physical_cells(table.rows[5], table)[-2].text != record['acceptance']:
                    raise AssertionError('Acceptance text was modified')
        append_summary(document, ledger)
        update_fields(document)
        target = args.output/(original.stem.replace('_填写版', '_保留30条判据_复测V1.3')+'.docx')
        document.save(target)
        with zipfile.ZipFile(target) as archive:
            if len(archive.namelist()) != len(set(archive.namelist())):
                raise ValueError('Duplicate DOCX archive members: '+str(target))
        outputs.append(str(target))
    if any(digest(Path(p)) != value for p, value in before.items()):
        raise AssertionError('Original requirements or Word baseline was changed')
    index = dict(case_count=30, acceptance_unchanged=True, source_sha256=before,
                 ledger_sha256=digest(args.ledger), evidence=evidence, generated=outputs,
                 note='Evidence paths are relative to agribot_report; no fabricated third-party signatures')
    (args.output/'复测来源与证据索引.json').write_text(json.dumps(index, ensure_ascii=False, indent=2)+'\n')
    (args.output/'逐项复测记录.json').write_text(json.dumps(ledger, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(generated=outputs, counts=Counter(r['verdict'] for r in ledger['records'])), ensure_ascii=False))


if __name__ == '__main__':
    main()
