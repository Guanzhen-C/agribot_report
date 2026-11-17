"""Read-only structural, provenance and rendered-text checks for the filled forms."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

from docx import Document
from docx.oxml.ns import qn
from docx.table import _Cell

from fill_templates import OUTPUT, WORK, PLAN, CASES, NAMES, MAPPING, format_value


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cells(row, table):
    return [_Cell(tc, table).text for tc in row._tr.tc_lst]


def text(doc):
    return '\n'.join(node.text or '' for node in doc.element.body.iter(qn('w:t')))


def normalized(value):
    return ''.join(value.split())


def plan_check(path, expected):
    doc = Document(path)
    assert len(doc.tables) == 10
    index = doc.tables[1]
    assert len(index.rows) == 34
    seen, groups = [], []
    for row in index.rows[1:]:
        columns = cells(row, index)
        if len(columns) == 1:
            groups.append(columns[0])
        else:
            number, title, ref, clause = columns
            item = expected[len(seen)]
            assert title == item['title']
            assert ref.endswith(f'YL-FU-{len(seen)+1:03d}')
            assert clause.endswith(item['requirement']['letter']+')')
            assert int(number) == len(seen)+1
            seen.append(title)
    assert all(name in group for name, group in zip(NAMES, groups))
    assert len(groups) == 3
    return doc


def check_pdf(path, expected_ids=None):
    metadata = subprocess.check_output(['pdfinfo', str(path)], text=True)
    count = int(re.search(r'Pages:\s+(\d+)', metadata).group(1))
    rendered = subprocess.check_output(['pdftotext', '-layout', str(path), '-'], text=True)
    pages = rendered.split('\f')
    if not pages[-1].strip():
        pages.pop()
    assert len(pages) == count
    for i, page in enumerate(pages, 1):
        assert len(page.strip()) > 110, (path, 'Unexpected blank page', i)
        if i > 1:
            assert re.search(rf'\b{i}\s*/\s*{count}\b', page), (path, 'Wrong footer', i)
    if expected_ids:
        raw = normalized(subprocess.check_output(['pdftotext', '-raw', str(path), '-'], text=True))
        assert set(re.findall(r'TC-HIL-(?:VCU|IMP|NAV)-001-[a-j]', raw)) == set(expected_ids)
        assert set(re.findall(r'YL-FU-\d{3}', raw)) == {f'YL-FU-{i:03d}' for i in range(1,31)}
        assert '开发方归档复核' in rendered
        assert '历史：2026-11-12' in rendered
    bbox = subprocess.check_output(['pdftotext', '-bbox', str(path), '-'])
    tree = ET.fromstring(bbox)
    outside = []
    namespace = {'h':'http://www.w3.org/1999/xhtml'}
    for i, page in enumerate(tree.findall('.//h:page', namespace), 1):
        w, h = float(page.attrib['width']), float(page.attrib['height'])
        for node in page.findall('h:word', namespace):
            if (float(node.attrib['xMin']) < -0.1 or float(node.attrib['yMin']) < -0.1
                    or float(node.attrib['xMax']) > w+0.1 or float(node.attrib['yMax']) > h+0.1):
                outside.append([i, node.text])
    assert not outside, outside
    return {'pages':count, 'all_footer_numbers_correct':True, 'unexpected_blank_pages':0, 'words_outside_page':0}


def main():
    ledger = json.loads((OUTPUT/'逐项实测记录.json').read_text())
    records = ledger['records']
    sources = json.loads((OUTPUT/'填写来源与校验.json').read_text())
    assert all(sha(Path(path)) == value for path, value in sources['source_sha256'].items())
    evidence = json.loads((OUTPUT/'证据索引.json').read_text())['entries']
    assert all(sha(OUTPUT/item['file']) == item['sha256'] for item in evidence.values())
    plan = plan_check(OUTPUT/(PLAN+'.docx'), records)
    roundtrip = plan_check(WORK/'doc-roundtrip'/(PLAN+'.docx'), records)
    assert 'i7-9750H' in text(plan) and 'Car4' in text(plan)
    assert 'HXMC-T-JH-2026042-02' in normalized(text(plan))
    assert 'HXMC-T-JH-2026042-02' in normalized(text(roundtrip))
    doc = Document(OUTPUT/(CASES+'.docx'))
    assert len(doc.tables) == 31
    conclusions = Counter()
    for number, (table, record) in enumerate(zip(doc.tables[1:], records), 1):
        assert len(table.rows) == 10
        first = cells(table.rows[0], table)
        assert first[3] == '开发方归档复核\n第三方待登记'
        assert first[-1] == '历史：2026-11-12\n复测：待登记'
        assert cells(table.rows[1], table)[1] == f'YL-FU-{number:03d}'
        section = record['category']
        assert cells(table.rows[1], table)[3] == f'V2.0\n3.{NAMES.index(section)+1} {record["requirement"]["letter"]})'
        content = normalized('\n'.join('\n'.join(cells(row, table)) for row in table.rows))
        for value in [record['id'], record['title'], record['requirement']['preconditions'],
                      record['requirement']['steps'], record['requirement']['acceptance'], record['finding'],
                      *record['actual_steps'], *record['remaining'],
                      *(format_value(m) for m in record['measurements'])]:
            assert normalized(value) in content, (record['id'], value)
        final = cells(table.rows[-1], table)[-1]
        assert final.startswith(MAPPING[record['verdict']]+'（'), (record['id'], final)
        conclusions[MAPPING[record['verdict']]] += 1
        for row in table.rows:
            for tc in row._tr.tc_lst:
                for color in tc.iter(qn('w:color')):
                    assert color.get(qn('w:val')) != 'FFFFFF', record['id']
                assert not list(tc.iter(qn('w:highlight')))
                assert not list(tc.iter(qn('w:vanish')))
        for row in table.rows:
            for height in row._tr.get_or_add_trPr().findall(qn('w:trHeight')):
                assert height.get(qn('w:hRule')) != 'exact'
    assert conclusions == Counter({'通过':6, '不通过':4, '不做判定':20})
    ids = [r['id'] for r in records]
    pdf_checks = {stem:check_pdf(OUTPUT/(stem+'.pdf'), ids if stem == CASES else None)
                  for stem in [PLAN, CASES]}
    files = {p.name:{'sha256':sha(p), 'bytes':p.stat().st_size}
             for p in OUTPUT.iterdir() if p.suffix in ('.doc','.docx','.pdf')}
    checks = {'passed':True, 'case_count':30, 'category_counts':dict(Counter(r['category'] for r in records)),
              'conclusions':dict(conclusions), 'all_source_files_unchanged':True,
              'archived_evidence_files_verified':len(evidence), 'legacy_doc_roundtrip_checked':True,
              'all_steps_requirements_facts_metrics_and_remaining_branches_retained':True,
              'no_white_text_hidden_values_or_highlights_in_case_cards':True,
              'third_party_execution_and_signatures_not_fabricated':True,
              'pdf':pdf_checks, 'output_files':files}
    (OUTPUT/'文档校验结果.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(checks,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
