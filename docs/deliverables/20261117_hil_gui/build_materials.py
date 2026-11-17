"""Generate the GUI-only V2.2 requirements and matching thirty-case form, without hardware."""
import argparse
from copy import deepcopy
import csv
import hashlib
import json
from pathlib import Path
import sys

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.table import Table

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SPEC = ROOT / 'tools/hil_recording/requirements.json'
TEMPLATE = HERE.parent / '20261117_hil_readable/files/2026042-01 农机控制器测试用例与实测报告_操作说明版V1.6.docx'
SRS = '三类控制器硬件在环测试需求规格说明_V2.2_五工况界面操作版'
REPORT = '2026042-01 农机控制器测试用例与记录表_V1.7_五工况界面操作版'
sys.path.insert(0, str(HERE.parent / '20261116_hil_templates'))
from fill_templates import physical_cells, set_cell, set_row, update_fields
sys.path.insert(0, str(HERE.parent / '20261112_hil_srs'))
from build_documents import field, format_style


def instructions():
    return [
        ('1 准备台架与软件', '由项目人员确认仅接隔离HIL台架，X5、HPM6750、两只USB-CAN、烧录器和网口连接正确，测试机已部署配套程序。不能接真车动力或机具执行器。不升级Ubuntu 22.04和ROS 2 Humble。测试人员双击原桌面软件入口，无需打开终端或输入任何命令。'),
        ('2 工况A：阿克曼机械臂采摘', '在车库选择Car0；点击Open Bag，安装雷达、RTK、相机、IMU及Lite6机械臂后关闭装备面板。点击启动仿真进入原配置界面，作业场景选苹果园；算法配置选STVL、视觉关闭、LIVO+RTK、Hybrid-A*、MPPI，再点启动HIL仿真。不要点普通启动仿真代替HIL。软件自行烧录对应6750固件、启动Gazebo/X5并记录。'),
        ('3 工况B：履带植保喷水', '返回车库选择Car3，使用相同四类传感器，装备植保喷水机具。选择苹果园，算法仍为STVL、视觉关闭、LIVO+RTK、Hybrid-A*、MPPI，点击原启动HIL仿真按钮。观察默认1→2→3路线及行驶喷雾。不要重新手选经停点，不增加障碍或喷头。'),
        ('4 工况C：履带碎枝', 'Car3换装碎枝机，传感器、苹果园及算法与B相同，点击原启动HIL仿真按钮。默认行走及刀轴旋转由既有任务控制；这里不要求加入木料或验证真实碎枝农艺效果。'),
        ('5 工况D：拖拉机动力换挡', '选择Car4，安装所需传感器和悬挂圆盘耙，场景选择宽地头；算法选STVL、视觉关闭、NavSat、Hybrid-A*、MPPI。在原配置界面左侧HIL变速箱选择动力换挡，再点击启动HIL仿真。观察接近、第一作业行、抬耙转弯、第二作业行、停车五阶段。'),
        ('6 工况E：拖拉机CVT', '保持Car4、悬挂圆盘耙、宽地头和相同算法，只在原HIL变速箱选择CVT，点击原启动HIL仿真。不要在行驶中切换模式；每个工况使用自己的新运行编号和真实烧录记录。'),
        ('7 每个工况怎样截图与结束', '只运行一个HIL工况。等待Gazebo车辆完成默认全程，使用系统截图工具截取实际运行画面：A至少显示采摘动作及终点果盒；B显示喷水及到点停止；C显示机具与行驶；D/E显示下放作业、抬升转弯、地面作业痕迹及终点。截图应带工况字母及时间命名，不用旧图或生成图代替。到点后不要立即关闭Gazebo，软件先自动观察5秒仿真时间；看到原配置界面记录提示“观察已完成，请停止仿真”后，点击原停止仿真按钮。等待停止和自动归档完成，再启动下一工况。'),
        ('8 查看自动记录，不输入命令', '点击原仿真配置页上方的测试记录，或双击桌面“控制器HIL测试记录”文件夹入口。按日期进入本轮目录，打开30项测试记录.txt或cases.csv，逐条查看编号、工况、判据和实际结果。cases.json保存结构化数据，index.json列出本轮各次运行编号；原始记录保存在.local/hil/recordings/runs/相应运行编号中。软件不会要求人工复制CAN数值、选择旧实测结果或运行测试脚本。截图随第三方报告附上。'),
        ('9 重试与异常处理', '某工况失败时用原停止仿真正常收尾，保留失败记录；排除问题后从同一软件重新启动该工况即可。本轮每种工况以最后一次尝试汇总，旧失败目录仍保留，不挑选旧成功结果。只有全部五工况实际完成、完整记录且30条均满足才显示整轮通过。新一天或完整一轮结束后的下一次启动自动新建一轮，不混用历史数据。不可强杀烧录器、拔线造故障、改判据、拖动车辆或手工补填通过。'),
    ]


def setup_document(title, version):
    doc = Document()
    section = doc.sections[0]
    section.page_width, section.page_height = Cm(21), Cm(29.7)
    section.top_margin, section.bottom_margin = Cm(2), Cm(1.9)
    section.left_margin, section.right_margin = Cm(2.1), Cm(2)
    for name, size, bold in [('Normal',10.5,False),('Title',21,True),('Heading 1',15,True),('Heading 2',12,True)]:
        format_style(doc.styles[name], size, bold)
    section.header.paragraphs[0].text = title + '／' + version
    footer = section.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
    footer.add_run('第 '); field(footer, 'PAGE'); footer.add_run(' 页 / 共 '); field(footer, 'NUMPAGES'); footer.add_run(' 页')
    doc.add_paragraph(title, style='Title')
    doc.add_paragraph(version + '／2026年11月17日\n五种原有HIL工况；软件正常启动流程；各类10条，共30条。')
    doc.core_properties.title = title
    doc.core_properties.version = version
    return doc


def common(doc, data):
    doc.add_heading('范围和结果真实性', 1)
    doc.add_paragraph(data['scope'])
    doc.add_paragraph(data['revision'])
    doc.add_paragraph('本次交付为界面启动后的自动记录功能和修订版文档。软件离线回归、历史记录重放、本次新实板运行及第三方签署分别标明。没有在本次交付中预填30条新实板通过；实际结果以测试人员随后运行这五种工况得到的本轮自动记录为准。')
    doc.add_paragraph('ISOBUS采用AgIsoStack++网络管理与项目应用PGN，不声明AEF认证或完整UT/TC通用兼容。机械臂仍通过网口传递规划及关节消息；底盘和非机械臂机具经实际USB-CAN及6750控制，不改真机CAN协议。整机、机具、导航是功能划分，不表示三块独立ECU。')
    doc.add_heading('五种工况及环境', 1)
    for value in data['profiles'].values():
        doc.add_paragraph(value)
    doc.add_paragraph('仿真PC/已部署惠普笔记本：Ubuntu 22.04、ROS 2 Humble、Gazebo Classic 11；X5在独立agribot_app_hil_ws运行算法，HPM6750使用各工况对应的已校验固件。新记录器仅运行在仿真电脑，被动订阅或复制已结束日志，不重新烧录、不启动另一任务、不发控制/CAN报文、不修改X5真车工作区。正常HIL启动本身仍会按原逻辑烧录6750。')
    doc.add_heading('测试人员公共操作', 1)
    for title, text in instructions():
        doc.add_heading(title, 2)
        doc.add_paragraph(text)


def make_srs(output, data):
    doc = setup_document('三类控制器硬件在环测试需求规格说明', 'V2.2 五工况界面操作版')
    common(doc, data)
    doc.add_heading('功能需求与逐条判据', 1)
    for group_index, (group, label) in enumerate(data['groups'].items(), 1):
        doc.add_heading(f'3.{group_index} {label}', 2)
        for case in (r for r in data['cases'] if r['id'].startswith(group + '-')):
            clause = f'3.{group_index} {case["id"][-1]})'
            doc.add_paragraph(clause + ' ' + case['title'], style='Heading 2')
            profiles = list(data['profiles']) if case['profiles'] == 'all' else case['profiles']
            doc.add_paragraph('适用：' + '；'.join(data['profiles'][p] for p in profiles))
            doc.add_paragraph(case['acceptance'])
            doc.add_paragraph('自动记录检查项：' + case['check'] + '。对应TC-HIL-' + group + '-001-' + case['id'][-1] + '；人员仅按公共流程启动适用工况、截图并正常停止。')
    doc.add_heading('范围变更及不覆盖事项', 1)
    for text in ['VCU-a/b/d/g/i调整为正常启动、反馈、正常换挡和正常结束可观察项目；不再以五次正常行驶推断烧录取消、未授权拒绝、非法参数或看门狗故障处理已经测试。',
                 'IMP-a/b/c/f/g/h调整为实际挂载、合法采摘保持、真实旋转及正常停止；去除错误通道、非法关节、断令恢复等额外操作分支。喷雾外观、果盒和翻土外观通过人员真实截图记录，不用静态图推断药液或土壤物理精度。',
                 'NAV-g为默认路径执行顺序，不把正常行驶当成额外障碍故障专项。NAV-i改为正常终点位置与停止状态，不再混入软件夹具或历史失败恢复记录。本版本不读取旧归档充当新结果。',
                 '完整原始数据缺失、采集进程异常、任务失败、用户提前终止或缺少适用工况时，按实际显示不通过、记录中或未运行，不通过修改判据自动凑齐结论。新范围应由委托方和第三方审核确认。']:
        doc.add_paragraph(text)
    update_fields(doc)
    doc.save(output / (SRS + '.docx'))


def make_cases(output, data):
    template = Document(TEMPLATE)
    originals = template.tables[1:]
    if len(originals) != 30:
        raise ValueError('Expected thirty agency case tables')
    doc = setup_document('农机控制器测试用例与记录表', 'V1.7 五工况界面操作版／对应需规V2.2')
    common(doc, data)
    doc.add_heading('30条测试用例与记录表', 1)
    for number, (case, source) in enumerate(zip(data['cases'], originals), 1):
        group, letter = case['id'].split('-')
        alias = f'YL-FU-{number:03d}'
        clause = f'3.{list(data["groups"]).index(group)+1} {letter})'
        identity = 'TC-HIL-' + group + '-001-' + letter
        profiles = list(data['profiles']) if case['profiles'] == 'all' else case['profiles']
        doc.add_paragraph(alias + ' ' + data['groups'][group] + '／' + case['title'], style='Heading 2')
        element = deepcopy(source._tbl)
        doc._body._body.insert(len(doc._body._body)-1, element)
        table = Table(element, doc._body)
        for row_index, row in enumerate(table.rows):
            properties = row._tr.get_or_add_trPr()
            for header in list(properties.findall(qn('w:tblHeader'))): properties.remove(header)
            if row_index < 2: properties.append(OxmlElement('w:tblHeader'))
            for cell in physical_cells(row, table):
                set_cell(cell, '', size=10)
        set_row(table.rows[0], table, ['记录标识',case['id']+'／V2.2','执行人','________','日期','________'], size=10)
        set_row(table.rows[1], table, ['用例编号',alias,'需求条款','需规V2.2第'+clause+'条','标准条款','委托功能需规／正常工况范围'], size=10)
        set_row(table.rows[2], table, ['检测项目',data['groups'][group]+'／'+case['title']+'\n'+identity], size=10)
        set_row(table.rows[3], table, ['检测条件','适用：'+'；'.join(data['profiles'][p] for p in profiles)+'。\n已部署新版软件与台架；不需要终端、不手发CAN、不额外造故障。'], size=10)
        set_row(table.rows[4], table, ['序号','操作描述','预期结果','实际结果'], size=10)
        steps = '1. 按前文公共流程，在原软件依次启动本条适用工况。\n2. 等默认全程完成，在Gazebo真实截图。\n3. 等5秒仿真观察提示后，点击原停止仿真，等待自动归档。'
        set_row(table.rows[5], table, ['1',steps,case['acceptance'],'本次实际结果以自动记录为准：________'], size=10)
        set_row(table.rows[6], table, ['2','点击配置页上方测试记录，打开本轮30项测试记录.txt或cases.csv，找到'+alias+'。','查看本条每个适用工况、实际运行编号、测量/核对结果。只看启动成功或截图不能代替后台实测。','本轮目录/运行编号：________'], size=10)
        set_row(table.rows[7], table, ['3','把真实截图随本轮记录附上；复测用新运行，失败记录不删除。','截图来自本次仿真，不能借用历史图片；不符合或缺数据按实际记录，不另加命令行步骤。','截图文件名：________'], size=10)
        set_row(table.rows[8], table, ['判定准则','按需规V2.2第'+clause+'条；后台检查项：'+case['check']+'。本条所有适用工况均需满足，未运行或记录未完整不能写通过。'], size=10)
        set_row(table.rows[9], table, ['结论','本次：________；执行人/日期：________。本表未预填新实板结果；不引用V2.1历史通过代替本次。'], size=10)
    update_fields(doc)
    doc.save(output / (REPORT + '.docx'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    data = json.loads(SPEC.read_text())
    make_srs(args.output, data)
    make_cases(args.output, data)
    guide = ['控制器HIL五工况界面操作说明／需规V2.2／用例V1.7', data['scope'], data['revision'], '']
    for title, text in instructions(): guide.extend([title, text, ''])
    (args.output / '测试人员操作步骤_无需命令行.txt').write_text('\n'.join(guide), encoding='utf-8')
    with (args.output / '30条需规与记录项对照.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream, lineterminator='\n')
        writer.writerow(['用例编号','条款标识','名称','自动检查项','判据'])
        for index, case in enumerate(data['cases'],1): writer.writerow([f'YL-FU-{index:03d}',case['id'],case['title'],case['check'],case['acceptance']])
    (args.output / '需规V2.2_正常五工况判据.json').write_text(SPEC.read_text(), encoding='utf-8')
    print(json.dumps(dict(documents=2, cases=30, requirements_sha256=hashlib.sha256(SPEC.read_bytes()).hexdigest(), hardware_actions=False)))


if __name__ == '__main__':
    main()
