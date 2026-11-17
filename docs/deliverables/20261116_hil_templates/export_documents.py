"""Update fields/TOC and export filled Word forms using a private LibreOffice instance."""
import os
from pathlib import Path
import subprocess
import time

import uno
from com.sun.star.beans import PropertyValue


WORK = Path('/home/cgz/.local/state/agribot-hil-templates-20261116')
OUTPUT = Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116')


def props(**kwargs):
    result = []
    for name, value in kwargs.items():
        item = PropertyValue()
        item.Name, item.Value = name, value
        result.append(item)
    return tuple(result)


def main():
    env = os.environ.copy()
    env['LD_LIBRARY_PATH'] = '/usr/lib/libreoffice/program:/usr/lib/x86_64-linux-gnu'
    name = f'agribot_word_{os.getpid()}'
    cmd = ['libreoffice', '-env:UserInstallation='+ (WORK/'lo-export-profile').as_uri(),
           '--headless', '--nologo', '--nodefault', '--norestore',
           f'--accept=pipe,name={name};urp;StarOffice.ComponentContext']
    process = subprocess.Popen(cmd, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    desktop = None
    try:
        local = uno.getComponentContext()
        resolver = local.ServiceManager.createInstanceWithContext('com.sun.star.bridge.UnoUrlResolver', local)
        context = None
        for _ in range(100):
            try:
                context = resolver.resolve(f'uno:pipe,name={name};urp;StarOffice.ComponentContext')
                break
            except Exception:
                time.sleep(0.15)
        if context is None:
            raise RuntimeError('Private LibreOffice listener did not start')
        desktop = context.ServiceManager.createInstanceWithContext('com.sun.star.frame.Desktop', context)
        for path in sorted((WORK/'generated').glob('*.docx')):
            document = desktop.loadComponentFromURL(path.as_uri(), '_blank', 0,
                props(Hidden=True, ReadOnly=False, UpdateDocMode=3, MacroExecutionMode=4))
            if document is None:
                raise RuntimeError('Failed to open '+str(path))
            try:
                document.refresh()
                for _ in range(2):
                    for index in document.DocumentIndexes:
                        index.update()
                    document.TextFields.refresh()
                    document.refresh()
                cursor = document.CurrentController.ViewCursor
                cursor.jumpToLastPage()
                document.TextFields.refresh()
                target = OUTPUT / path.name
                document.storeAsURL(target.as_uri(), props(FilterName='Office Open XML Text', Overwrite=True))
                document.storeToURL(target.with_suffix('.pdf').as_uri(), props(FilterName='writer_pdf_Export', Overwrite=True))
                if '测试计划' in path.name:
                    document.storeToURL(target.with_suffix('.doc').as_uri(), props(FilterName='MS Word 97', Overwrite=True))
                print(target.name, 'exported', flush=True)
            finally:
                document.close(True)
    finally:
        if desktop:
            desktop.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=10)


if __name__ == '__main__':
    main()
