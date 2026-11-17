"""Check that exported GUI documents preserve all thirty V2.2 criteria."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    spec = json.loads((ROOT / 'tools/hil_recording/requirements.json').read_text())
    summary = dict(passed=False, hardware_actions=False, cases=30, documents=[])
    documents = list(args.output.glob('*.docx'))
    assert len(documents) == 2
    for path in documents:
        with zipfile.ZipFile(path) as archive:
            assert len(archive.namelist()) == len(set(archive.namelist()))
            root = ET.fromstring(archive.read('word/document.xml'))
        ns = {'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
        content = ''.join(root.itertext())
        for case in spec['cases']:
            assert case['acceptance'] in content, (path, case['id'])
        if '记录表' in path.name:
            assert len(root.findall('.//w:tbl',ns)) == 30
            assert {f'YL-FU-{i:03d}' for i in range(1,31)}.issubset(set(re.findall(r'YL-FU-\d{3}',content)))
            assert '历史结论：通过' not in content
        bbox = subprocess.check_output(['pdftotext','-bbox',str(path.with_suffix('.pdf')),'-'])
        pdf = ET.fromstring(bbox)
        pages = pdf.findall('.//{http://www.w3.org/1999/xhtml}page')
        assert pages
        for index, page in enumerate(pages, 1):
            words = page.findall('.//{http://www.w3.org/1999/xhtml}word')
            assert len(words) > 8, (path,'empty-page',index)
            for word in words:
                assert 0 <= float(word.get('xMin')) <= float(word.get('xMax')) <= float(page.get('width')) + 1, (path,index,word.text)
                assert 0 <= float(word.get('yMin')) <= float(word.get('yMax')) <= float(page.get('height')) + 1, (path,index,word.text)
        summary['documents'].append(dict(file=path.name, pages=len(pages), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    with (args.output / '30条需规与记录项对照.csv').open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream))
    assert [r['条款标识'] for r in rows] == [r['id'] for r in spec['cases']]
    assert [r['判据'] for r in rows] == [r['acceptance'] for r in spec['cases']]
    assert json.loads((args.output / '需规V2.2_正常五工况判据.json').read_text()) == spec
    summary['passed'] = True
    (args.output / '文档校验结果.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(summary,ensure_ascii=False))


if __name__ == '__main__':
    main()
