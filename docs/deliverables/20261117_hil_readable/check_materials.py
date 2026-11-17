"""Audit the rewritten documents and archived evidence without hardware access."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

from docx import Document

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from build_materials import REPORT, SRS, instructions, physical_cells


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def compact(text):
    return ''.join(text.split())


def validate(output):
    manifest = json.loads((output / '说明改写与条款核验.json').read_text())
    requirements = json.loads((output / '冻结判据_V2.1.json').read_text())
    ledger = json.loads((output / '历史30条结果_20261116.json').read_text())
    index = json.loads((output / '证据索引.json').read_text())
    for source, expected in manifest['sources'].items():
        assert sha(Path(source)) == expected, 'Historical source changed: ' + source
    for entry in index['evidence'].values():
        assert sha(Path(entry['path'])) == entry['sha256'], entry['path']
    assert len(ledger['records']) == 30 and ledger['counts'] == {'通过': 30}
    assert manifest['new_physical_execution'] is False and manifest['criteria_unchanged'] is True
    cases = [(s, c) for s in requirements['sections'] for c in s['cases']]
    document = Document(output / (REPORT + '.docx'))
    assert len(document.tables) == 31
    spec = Document(output / (SRS + '.docx'))
    spec_text = '\n'.join(p.text for p in spec.paragraphs)
    raw = {r['id']: r for r in ledger['records']}
    for number, (table, (section, case)) in enumerate(zip(document.tables[1:], cases), 1):
        identity = section['test_prefix'] + '-' + case['letter']
        alias = f'YL-FU-{number:03d}'
        assert physical_cells(table.rows[1], table)[1].text == alias
        assert identity in physical_cells(table.rows[2], table)[-1].text
        assert compact(physical_cells(table.rows[5], table)[-2].text) == compact(case['acceptance']), identity
        assert compact(case['acceptance']) in compact(spec_text), identity
        assert compact(raw[identity]['observed']) in compact(physical_cells(table.rows[5], table)[-1].text)
        assert '本次复测结论：' in physical_cells(table.rows[9], table)[-1].text
        assert '第三方' in physical_cells(table.rows[9], table)[-1].text
        assert compact(f'3.{(number-1)//10+1} {case["letter"]})') in compact(physical_cells(table.rows[1], table)[3].text)
    for path in output.glob('*.docx'):
        with zipfile.ZipFile(path) as archive:
            assert len(archive.namelist()) == len(set(archive.namelist())), path.name
    for title, blocks in instructions():
        for kind, text in blocks:
            if kind == 'code':
                result = subprocess.run(['bash', '-n'], input=text, text=True, capture_output=True)
                assert result.returncode == 0, (title, result.stderr)
    pages = {}
    for stem in (REPORT, SRS):
        path = output / (stem + '.pdf')
        text = subprocess.check_output(['pdftotext', '-layout', str(path), '-'], text=True)
        sections = text.split('\f')
        if not sections[-1].strip():
            sections.pop()
        assert all(len(compact(part)) >= 15 for part in sections), 'Blank PDF page'
        for number in range(1, 31):
            assert f'YL-FU-{number:03d}' in compact(text), (stem, number)
        assert 'STVL关闭' not in compact(text).replace('“STVL关闭”属于文字勘误', '').replace('“STVL均关闭”在本配套说明中勘误', '')
        tree = ET.fromstring(subprocess.check_output(['pdftotext', '-bbox-layout', str(path), '-']))
        outside = []
        for i, page in enumerate(tree.iter('{http://www.w3.org/1999/xhtml}page'), 1):
            width, height = float(page.attrib['width']), float(page.attrib['height'])
            for word in page.iter('{http://www.w3.org/1999/xhtml}word'):
                x1, y1, x2, y2 = (float(word.attrib[name]) for name in ('xMin', 'yMin', 'xMax', 'yMax'))
                if min(x1, y1) < -.5 or x2 > width+.5 or y2 > height+.5:
                    outside.append((i, word.text))
        assert not outside, outside[:5]
        pages[path.name] = dict(pages=len(sections), blank_pages=0, out_of_page_words=0)
    result = dict(passed=True, cases=30, categories={s['name']: len(s['cases']) for s in requirements['sections']},
        criteria_match_original_v21=True, requirement_references_complete=True,
        historical_result_text_preserved=True, historical_sources_unchanged=True,
        evidence_hashes_verified=len(index['evidence']), command_blocks_syntax_checked=True,
        docx_duplicate_members=0, pdf_layout=pages,
        new_physical_execution=False, hardware_actions=False)
    (output / '文档与证据校验结果.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(validate(args.output.resolve()), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
