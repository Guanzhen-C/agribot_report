"""Export only the two rewritten documents with the existing private UNO workflow."""
import argparse
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile


NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}


def export_texts(output):
    def paragraph_text(paragraph):
        tokens = []
        for node in paragraph.iter():
            tag = node.tag.rsplit('}', 1)[-1]
            if tag == 't':
                tokens.append(node.text or '')
            elif tag in ('br', 'cr'):
                tokens.append('\n')
            elif tag == 'tab':
                tokens.append('\t')
        return ''.join(tokens)

    def paragraphs(element):
        return '\n'.join(paragraph_text(p) for p in element.findall('.//w:p', NS))

    for path in output.glob('*.docx'):
        with zipfile.ZipFile(path) as archive:
            root = ET.fromstring(archive.read('word/document.xml'))
        lines = []
        for child in root.find('w:body', NS):
            if child.tag.endswith('}p'):
                text = paragraph_text(child)
                if text.strip():
                    lines.extend([text, ''])
            elif child.tag.endswith('}tbl'):
                rows = [[paragraphs(cell) for cell in row.findall('w:tc', NS)] for row in child.findall('w:tr', NS)]
                if len(rows) == 10 and rows[0][0].startswith('记录'):
                    for label, row, col in [('用例编号', 1, 1), ('需求条款', 1, 3), ('检测项目', 2, -1),
                            ('检测条件', 3, -1), ('操作描述', 5, 1), ('完整预期结果', 5, -2),
                            ('历史实测结果', 5, -1), ('证据和结果查看', 6, 1), ('历史证据编号', 6, -1),
                            ('收尾', 7, 1), ('不覆盖范围', 7, -1), ('判定准则', 8, -1), ('历史及本次结论', 9, -1)]:
                        lines.extend([label + '：', rows[row][col], ''])
                else:
                    lines.extend([' | '.join(row) for row in rows] + [''])
        path.with_suffix('.txt').write_text('\n'.join(lines), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True, help='Prepared generated/ directory')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    work = args.work.resolve()
    if work.name != 'generated' or len(list(work.glob('*.docx'))) != 2:
        raise ValueError('The two newly generated Word documents are required')
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / '20261116_hil_templates'))
    import export_documents
    export_documents.WORK = work.parent
    export_documents.OUTPUT = args.output.resolve()
    export_documents.main()
    export_texts(args.output.resolve())


if __name__ == '__main__':
    main()
