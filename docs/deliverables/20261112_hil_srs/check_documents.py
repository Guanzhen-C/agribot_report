"""Check case counts, full requirement text and PDF page bounds, not HIL results."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unicodedata
import xml.etree.ElementTree as ET

from docx import Document

from build_documents import HERE, ROOT, validate


def compact(value):
    return re.sub(r'\s+', '', unicodedata.normalize('NFKC', value))


def check(directory):
    data = json.loads((HERE/'requirements.json').read_text())
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    expected = validate(data, catalog)
    docx = directory/'三类控制器硬件在环测试需规_V2.0_20261112.docx'
    pdf = docx.with_suffix('.pdf')
    plain = docx.with_suffix('.txt')
    paragraphs = [p.text for p in Document(docx).paragraphs]
    actual = [p.removeprefix('用例标识：') for p in paragraphs if p.startswith('用例标识：')]
    assert actual == expected
    pdf_text = subprocess.check_output(['pdftotext', '-layout', str(pdf), '-'], text=True)
    ids = re.findall(r'用例标识：\s*(TC-HIL-(?:VCU|IMP|NAV)-001-[a-j])', pdf_text)
    assert ids == expected
    compact_pdf = compact(pdf_text)
    missing = []
    for group in data['sections']:
        for case in group['cases']:
            for key in ('title', 'preconditions', 'steps', 'acceptance', 'evidence'):
                if compact(case[key]) not in compact_pdf:
                    missing.append(group['test_prefix']+'-'+case['letter']+':'+key)
    assert not missing, missing
    tree = ET.fromstring(subprocess.check_output(['pdftotext', '-bbox', str(pdf), '-']))
    ns = {'h': 'http://www.w3.org/1999/xhtml'}
    pages = tree.findall('.//h:page', ns)
    outside = []
    for index, page in enumerate(pages, 1):
        width, height = float(page.get('width')), float(page.get('height'))
        for word in page.findall('.//h:word', ns):
            if (float(word.get('xMin')) < 30 or float(word.get('xMax')) > width-25 or
                    float(word.get('yMin')) < 15 or float(word.get('yMax')) > height-15):
                outside.append({'page': index, 'text': word.text, 'bounds': word.attrib})
    assert not outside, outside
    plain_ids = re.findall(r'用例标识：(TC-HIL-(?:VCU|IMP|NAV)-001-[a-j])', plain.read_text())
    assert plain_ids == expected
    result = {
        'scope': 'Document structure/content/render QA only; no new hardware tests',
        'case_count': len(expected), 'unique_case_count': len(set(expected)),
        'categories': {group['name']: len(group['cases']) for group in data['sections']},
        'pages': len(pages), 'missing_requirement_fields': missing, 'outside_margin_words': outside,
        'word_pdf_text_case_order_matches': True,
        'all_150_case_fields_present_in_pdf': True,
        'files': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (docx, pdf, plain)}
    }
    (directory/'PDF排版检查.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    check(parser.parse_args().directory)
