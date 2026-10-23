#!/usr/bin/env python3
"""Check local Markdown file links in the delivery documentation."""
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def main():
    errors = []
    documents = [ROOT / 'README.md', *sorted((ROOT / 'docs').glob('*.md'))]
    for document in documents:
        in_code = False
        for number, line in enumerate(document.read_text().splitlines(), 1):
            if line.lstrip().startswith('```'):
                in_code = not in_code
                continue
            if in_code:
                continue
            for match in re.finditer(r'\[[^\]]+\]\(([^)]+)\)', line):
                target = match.group(1).strip('<>')
                parsed = urlsplit(target)
                if parsed.scheme or not parsed.path:
                    continue
                path = (document.parent / unquote(parsed.path)).resolve()
                if not path.is_relative_to(ROOT) or not path.exists():
                    errors.append(f'{document.relative_to(ROOT)}:{number}: {target}')
    if errors:
        raise SystemExit('\n'.join(errors))
    print(f'{len(documents)} documents: local file links are valid.')


if __name__ == '__main__':
    main()
