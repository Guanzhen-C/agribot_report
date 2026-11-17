"""Validate delivered forms, exact scope differences and actual evidence assertions."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

from docx import Document

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent/'20261116_hil_templates'))
from fill_templates import physical_cells
sys.path.insert(0, str(HERE.parent/'20261116_hil_retest'))
from build_retest_documents import validate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    directory = args.directory.resolve()
    ledger = json.loads((directory/'逐项结果_需规V2.1.json').read_text())
    requirements = json.loads((directory/'requirements.json').read_text())
    index = json.loads((directory/'来源与证据索引_V2.1.json').read_text())
    assert ledger['new_30_case_physical_campaign'] is False
    assert index['new_30_case_physical_campaign'] is False
    assert len(ledger['records']) == 30 and all(r['verdict'] == '通过' for r in ledger['records'])
    assert sum(r['scope_adjusted'] for r in ledger['records']) == 11
    assert sum('本轮原场景' in r['evidence_mode'] for r in ledger['records']) == 1
    evidence = validate(ledger, requirements)
    for path, expected in index['sources'].items():
        assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected, path
    for relative, expected in index['raw_record_inputs'].items():
        path = ROOT/'.validation/hil_acceptance_20261116'/relative
        assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, relative
    cases = Document(next(directory.glob('*测试用例*.docx')))
    assert len(cases.tables) == 31
    for table, record in zip(cases.tables[1:], ledger['records']):
        assert physical_cells(table.rows[5], table)[-2].text == record['acceptance'], record['id']
        assert record['id'] in physical_cells(table.rows[2], table)[-1].text
        assert record['evidence_mode'] in physical_cells(table.rows[9], table)[-1].text
    word_files = list(directory.glob('*.docx'))
    for path in word_files:
        with zipfile.ZipFile(path) as archive:
            assert len(archive.namelist()) == len(set(archive.namelist()))
        text = '\n'.join(p.text for p in Document(path).paragraphs)
        assert '本次仅填表，没有新增实板试验' not in text
        assert '原V2.0' in text and '现有条件' in text
    pages = {}
    for path in directory.glob('*.pdf'):
        info = subprocess.check_output(['pdfinfo', str(path)], text=True)
        pages[path.name] = int(next(line.split(':', 1)[1] for line in info.splitlines() if line.startswith('Pages:')))
        text = subprocess.check_output(['pdftotext', '-layout', str(path), '-'], text=True)
        compact = ''.join(text.split())
        assert 'V2.1' in compact and 'V1.4' in compact
        assert '不表示' in text or '不代表' in text
    assert len(word_files) == 2 and len(pages) == 2
    result = dict(passed=True, requirements_version='V2.1', form_version='V1.4',
        cases=30, changed_criteria=11, unchanged_criteria=19,
        exact_word_criteria_match=True, source_and_raw_evidence_hashes_match=True,
        evidence_assertions_checked=len(evidence), docx_no_duplicate_members=True,
        new_30_case_physical_campaign=False, new_physical_case_count=1, pdf_pages=pages,
        scope='Document structure, explicitly revised scope and evidence checks; not a new physical campaign')
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
