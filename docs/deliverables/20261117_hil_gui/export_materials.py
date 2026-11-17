"""Export the two GUI-flow documents with the existing private LibreOffice workflow."""
import argparse
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    work, output = args.work.resolve(), args.output.resolve()
    if work.name != 'generated' or len(list(work.glob('*.docx'))) != 2:
        raise ValueError('Expected the two generated GUI-flow Word files')
    output.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / '20261116_hil_templates'))
    import export_documents
    export_documents.WORK, export_documents.OUTPUT = work.parent, output
    export_documents.main()
    namespace = {'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    for path in output.glob('*.docx'):
        with zipfile.ZipFile(path) as archive:
            root = ET.fromstring(archive.read('word/document.xml'))
        lines = []
        for paragraph in root.findall('.//w:p', namespace):
            tokens = []
            for node in paragraph.iter():
                if node.tag == '{' + namespace['w'] + '}t': tokens.append(node.text or '')
                elif node.tag in ('{' + namespace['w'] + '}br', '{' + namespace['w'] + '}cr'): tokens.append('\n')
            if ''.join(tokens).strip(): lines.append(''.join(tokens))
        path.with_suffix('.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    for path in work.iterdir():
        if path.suffix in ('.txt','.csv','.json'): shutil.copy2(path, output/path.name)


if __name__ == '__main__':
    main()
