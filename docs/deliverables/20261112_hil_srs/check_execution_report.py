"""Independently check the rendered report, evidence hashes and observed values."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import re
import shlex
import subprocess
import unicodedata
import xml.etree.ElementTree as ET
import zipfile

from docx import Document

from build_documents import HERE, ROOT, EXPECTED_NAMES, validate
from build_execution_report import STEM, pointer, display


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compact(value):
    return re.sub(r'\s+', '', unicodedata.normalize('NFKC', value))


def check(directory):
    requirements = json.loads((HERE/'requirements.json').read_text())
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    expected = validate(requirements, catalog)
    ledger = json.loads((directory/'逐项实测记录.json').read_text())
    index = json.loads((directory/'证据索引.json').read_text())['entries']
    assert ledger['new_hardware_operations'] is False
    assert ledger['category_order'] == EXPECTED_NAMES
    assert ledger['requirement_source_sha256'] == sha(HERE/'requirements.json')
    assert [row['id'] for row in ledger['records']] == expected
    assert dict(Counter(row['verdict'] for row in ledger['records'])) == ledger['verdict_counts']
    files = {}
    for identity, entry in index.items():
        path = directory/entry['file']
        assert path.resolve().is_relative_to(directory.resolve())
        assert sha(path) == entry['sha256'], identity
        assert path.stat().st_size == entry['bytes']
        files[identity] = json.loads(path.read_text()) if path.suffix == '.json' else None
    metrics = 0
    for row in ledger['records']:
        assert bool(row['remaining']) == (row['verdict'] != '通过')
        assert all(identity in index for identity in row['evidence'])
        for metric in row['measurements']:
            original = pointer(files[metric['evidence']], metric['pointer'])
            assert original == metric['original_value']
            value = original*metric.get('factor', 1) if isinstance(original, (int, float)) and not isinstance(original, bool) else original
            assert value == metric['value']
            metrics += 1
    assert files['E21']['audit_passed'] is False and files['E26']['audit_passed'] is False
    assert files['E21']['unplanned_off_samples'] == 2
    assert files['E26']['unplanned_off_samples'] == 4
    assert files['E18']['mission_status'] == 'failed'
    docx, pdf, plain = [directory/(STEM+suffix) for suffix in ('.docx', '.pdf', '.txt')]
    document = Document(docx)
    actual = [p.text.removeprefix('用例标识：') for p in document.paragraphs if p.text.startswith('用例标识：')]
    assert actual == expected
    pdf_text = subprocess.check_output(['pdftotext', '-layout', str(pdf), '-'], text=True)
    pattern = r'用例标识：\s*(TC-HIL-(?:VCU|IMP|NAV)-001-[a-j])'
    pdf_ids = re.findall(pattern, pdf_text)
    assert pdf_ids == expected
    parts = re.split(pattern, pdf_text)
    segments = {parts[i]: compact(parts[i+1]) for i in range(1, len(parts), 2)}
    for row in ledger['records']:
        segment = segments[row['id']]
        for value in row['actual_steps']+[row['finding']]+row['remaining']:
            assert compact(value) in segment, (row['id'], value)
        for metric in row['measurements']:
            # PDF extraction interleaves adjacent columns between value lines.
            for value_line in display(metric['value']).splitlines():
                assert compact(value_line) in segment, (row['id'], metric['label'], value_line)
    rows = list(csv.DictReader((directory/'30条实测对照表.csv').open(encoding='utf-8-sig')))
    assert [row['用例ID'] for row in rows] == expected
    assert [row['完整用例结论'] for row in rows] == [row['verdict'] for row in ledger['records']]
    with zipfile.ZipFile(docx) as archive:
        media = {hashlib.sha256(archive.read(name)).hexdigest()
                 for name in archive.namelist() if name.startswith('word/media/')}
    assert media == {index['P01']['sha256'], index['P02']['sha256']}
    text = plain.read_text()
    assert re.findall(pattern, text) == expected
    commands = re.findall(r'python3 tools/hil/qualify\.py --execute .*?--timeout 2400', text, re.S)
    assert len(commands) == 2
    for command in commands:
        arguments = shlex.split(command.replace('\\\n', ' '))
        assert arguments[:3] == ['python3', 'tools/hil/qualify.py', '--execute']
        assert arguments[-2:] == ['--timeout', '2400']
    tree = ET.fromstring(subprocess.check_output(['pdftotext', '-bbox', str(pdf), '-']))
    ns = {'h': 'http://www.w3.org/1999/xhtml'}
    pages = tree.findall('.//h:page', ns)
    outside = []
    for number, page in enumerate(pages, 1):
        width, height = float(page.get('width')), float(page.get('height'))
        for word in page.findall('.//h:word', ns):
            if (float(word.get('xMin')) < 30 or float(word.get('xMax')) > width-25 or
                    float(word.get('yMin')) < 15 or float(word.get('yMax')) > height-15):
                outside.append({'page': number, 'text': word.text, 'bounds': word.attrib})
    assert not outside, outside
    result = {
        'scope': 'Saved-evidence and document QA; not new hardware execution',
        'unique_case_count': len(expected), 'verdict_counts': ledger['verdict_counts'],
        'case_count_per_category': dict(Counter(row['category'] for row in ledger['records'])),
        'pages': len(pages), 'verified_evidence_files': len(index),
        'verified_measurement_references': metrics,
        'word_pdf_csv_text_case_order_and_verdicts_match': True,
        'actual_steps_findings_pending_items_and_measurements_visible_in_pdf': True,
        'embedded_plots_match_actual_archived_images': True,
        'historical_commands_parse_without_execution': True,
        'failed_original_audits_preserved': True, 'outside_margin_words': outside,
        'files': {path.name: sha(path) for path in (docx, pdf, plain, directory/'逐项实测记录.json',
                                                  directory/'证据索引.json', directory/'30条实测对照表.csv')}
    }
    (directory/'实测报告排版核查.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    check(parser.parse_args().directory)
