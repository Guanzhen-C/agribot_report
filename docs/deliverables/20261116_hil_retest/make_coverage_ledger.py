"""Preserve all original criteria and record the actual, incomplete retest coverage."""
import argparse
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent/'20261116_hil_method_revision'))
from revise_methods import METHODS


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--current', action='store_true', help='Include the reviewed post-replug five-run milestone')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    requirements = json.loads((HERE.parent/'20261112_hil_srs/requirements.json').read_text())
    baseline = json.loads(Path('/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116/逐项实测记录.json').read_text())
    previous = {r['id']: r for r in baseline['records']}
    records = []
    for section in requirements['sections']:
        for case in section['cases']:
            identity = section['test_prefix']+'-'+case['letter']
            records.append(dict(id=identity, title=case['title'], category=section['name'],
                profiles=case['profiles'], acceptance=case['acceptance'],
                baseline_verdict=previous[identity]['verdict'], verdict='未完整执行',
                observed='', remaining=[], assertions=[]))
    table = {r['id'].removeprefix('TC-HIL-'): r for r in records}

    def ref(path, field='passed', expected=True):
        return dict(path='.validation/hil_acceptance_20261116/'+path, field=field.split('.'), expected=expected)

    def put(group, letter, verdict, observed, remaining, *refs):
        record = table[group+'-001-'+letter]
        record.update(verdict=verdict, observed=observed, remaining=remaining, assertions=list(refs))

    apple = 'apple-native-r2/'
    spray = 'sprayer-native-r1/'
    cutter = 'shredder-native-r1/'
    powershift = 'powershift-async-r2/'
    cvt = 'cvt-async-r2/'
    controls = 'controller-after-async-r1/'
    recent = 'normal-campaign-r2/'
    cut_control = recent+'shredder-controls-r2/'
    spray_control = 'sprayer-controls-r1/'
    apple_control = 'apple-controls-r1/'

    put('VCU', 'a', '未完整执行', '五工况存在真实烧录校验日志；同一喷水工况连续两次烧录中取消均在Verified OK后清理，'
        '错误组合/算法/缺传感器/错误烧录器预检均实际拒绝。三组新原生MPPI完整任务已实测，最新Car4全程在物理CAN启动故障处阻塞。',
        ['补齐最终冻结版本五工况的闭合启动记录，不能用预检ready代替任务启动'],
        ref('flash-repeat-cancel-r1/qualification.json'), ref('negative-preflight-r1/result.json'),
        ref(apple+'mission-checks.json'), ref(spray+'mission-checks.json'), ref(cutter+'mission-checks.json'))
    put('VCU', 'b', '未完整执行', '实际1 Mbit/s和250 kbit/s配置、绑定序列号及错误身份/错误波特率拒绝已检查；'
        '两种速率的只读拒绝检查均没有新增TX。最近PC盒虽UP，但5帧诊断0帧完成，内部回环同样5发0收，未视为链路通过。',
        ['物理USB-CAN恢复后，补齐最终版本五工况两段物理初始化及就绪门控'],
        ref(apple_control+'controller-checks.json'), ref(spray_control+'controller-checks.json'),
        ref('pc-loopback-after-native-power-r1.json', 'loopback_passed', False))
    put('VCU', 'c', '未完整执行', '保留原Car0、Car3及独立Car4协议；原动力四类反馈逐字节审计通过。'
        '新增喷水/碎枝三方核对同时检查PC原生帧、X5物理USB上ISOBUS帧及返回ROS事件，收到的反馈一致；未据此宣称零丢包。',
        ['逐工况补原生非法校验、非法字段和未许可方向的真实注入与拒绝记录'],
        ref(spray_control+'feedback-independent-audit.json'), ref(cut_control+'feedback-independent-audit.json'))
    put('VCU', 'd', '未完整执行', 'Car0、Car3两机具和Car4动力换挡均在实际授权false读回后主动提交合法非零请求，实际车体保持静止；'
        'Car4悬挂保持及动力使能关闭。正常任务和专项中有真实CAN运动、停止反馈。',
        ['补Car4 CVT选定工况的禁止授权专项及全部工况的指令/实际转向对应复核'],
        ref(apple_control+'authority.json'), ref(spray_control+'authority.json'), ref(cut_control+'authority.json'),
        ref(recent+'tractor-authority-r1/authority.json'))
    put('VCU', 'e', '通过', '软件日志异步化及接收到达时钟竞争修复后，动力换挡完成默认五阶段及终点5秒。'
        '受控专项实际完成1挡、升4挡、降挡及离合器交接。正常窗口PC/X5带速PT_OFF均0，'
        '状态处理最大间隔0.137147622秒，原0.30秒门限未变。原失败轮次不覆盖。', [],
        ref(powershift+'mission-checks.json'), ref(powershift+'powertrain-audit.json', 'audit_passed'),
        ref(powershift+'powertrain-independent-audit.json'), ref(controls+'powertrain/result.json'))
    put('VCU', 'f', '通过', 'CVT修复轮完成默认五阶段、终点5秒及原/独立动力审计。实际总传动比覆盖200至900，'
        '正常窗口PC/X5带速PT_OFF均0，最大状态间隔0.142312358秒。受控改变目标速比、负载及变化率均观察实际动力响应，终态退出驱动并制动。', [],
        ref(cvt+'mission-checks.json'), ref(cvt+'powertrain-audit.json', 'audit_passed'),
        ref(cvt+'powertrain-independent-audit.json'), ref(controls+'powertrain/result.json'))
    put('VCU', 'g', '未完整执行', '停车配置专项实测5组合法边界和17种非法请求，并逐项关联X5非法输入事件与实际关闭使能/制动；'
        '另有运行中换模式拒绝及变化率设置实测。不是只有单元测试或界面回显。',
        ['补冻结最终版本两个选定传动工况的完整配置专项记录'], ref(controls+'controller-checks.json'),
        ref(controls+'powertrain/result.json'))
    put('VCU', 'h', '通过', '两种传动完整轮分别核对0x642至0x645与PGN FF74至FF77，'
        '四类返回有效负载均与PC实际原生反馈一致；保留收帧数量和匹配数量，不以此推断零丢包。实际挡位、速比、离合器及制动由动力模型反馈。', [],
        ref(powershift+'powertrain-audit.json', 'audit_passed'), ref(cvt+'powertrain-audit.json', 'audit_passed'))
    put('VCU', 'i', '未完整执行', 'Car0、喷水Car3、碎枝Car3均真实断高层输入及暂停X5隔离USB所有者1.5秒，'
        'PC按原租约零执行，实际停车，先恢复零目标/反馈，再发新目标恢复，未重放旧正目标。Car4原真实反馈暂停专项反应约0.2005秒、停稳0.4644秒。',
        ['补两个Car4选定工况和物理断线/身份错误不得普通拥塞续跑的完整专项；0.30秒不放宽'],
        ref('normal-campaign-r1/apple-watchdog-r1/chassis-watchdog.json'),
        ref(cut_control+'chassis-watchdog.json'), ref(recent+'sprayer-visual-r1/chassis-watchdog.json'),
        ref(controls+'powertrain/result.json'))
    put('VCU', 'j', '阻塞', 'Car0双果、喷水及碎枝新原生MPPI完整任务和终点5秒已完成，真实CAN无错误/重建，清理及真车保护复核通过。'
        '最新动力换挡启动时PC物理TX反复停滞，三次有界恢复后退出，任务尚未开始行驶；CVT后继任务没有执行。'
        '原异步修复轮两种传动全程成功保留，但不代替最新完整五工况结论。',
        ['现场恢复PC USB-CAN后补最终版本两种传动全程及清理；原历史不通过尚未整体关闭'],
        ref(apple+'postcheck.json'), ref(spray+'postcheck.json'), ref(cutter+'postcheck.json'),
        ref(recent+'powershift-native-r1/mission-checks.json', 'passed', False))

    put('IMP', 'a', '未完整执行', '正常工况和预检中核对机具绑定；Car3喷水/碎枝错通道请求均实际拒绝，'
        '实际行走不被篡改，新合法请求恢复。真实运行图片已取得，不使用合成效果图。',
        ['补五工况装配/尺度/挂点及Car0、Car4不匹配组合的完整记录'],
        ref(spray_control+'channel-rejection.json'), ref(cut_control+'channel-rejection.json'),
        ref('negative-preflight-r1/result.json'))
    put('IMP', 'b', '通过', '实际双果任务取得两颗不同的可执行目标并经IK、OMPL及无碰撞Cartesian路径采摘。'
        '专项对远不可达目标、无效起始关节和不完整路径均拒绝，规划应答超时夹具采用0.001秒客户端期限且未发机械臂轨迹；'
        '该夹具不作为默认20秒性能指标。树枝碰撞检查未删除。', [],
        ref(apple_control+'arm-negatives/result.json'), ref(apple+'apple-independent-audit.json'))
    put('IMP', 'c', '通过', '双果全程去程和作业后行驶均核对真实保持状态及8.9mm指尖目标，最大偏差约2.28微米，低于0.25mm原门限。'
        '实际底盘运动、陈旧关节和无模式应答专项均拒绝采摘。观测真实夹持苹果时，错误运输保持请求被真实客户端拒绝且发布次数0，未张爪；'
        '该拒绝测试的发布代理失败闭合，不向真实执行链强行发送开爪。', [],
        ref(apple_control+'arm-negatives/result.json'), ref(apple+'held-fruit-guard.json'),
        ref(apple+'apple-independent-audit.json'))
    put('IMP', 'd', '通过', '一次连续全程采摘harvest_apple_7及harvest_apple_5，各有两侧夹持垫接触同一果梗、摘离、放盘及收臂事件。'
        '行驶到3号点后，两果仍在原果盒内，独立关节和果实位置审计均通过。没有重置车辆或强行改果实位置。', [],
        ref(apple+'apple-independent-audit.json'), ref(apple+'mission-checks.json'))
    put('IMP', 'e', '通过', '实板提交风机3挡/水泵4挡，PC实际0x580为(3,4,0)，与行走0x514同时存在；真实风机和水泵旋转、停令减速。'
        '新三方审计中0x5B2、0x5B3各41个原生/ROS反馈，完整公共窗口各38个均在X5物理ISOBUS接收中匹配，未据此宣称零丢包。', [],
        ref(spray_control+'controller-checks.json'), ref(spray_control+'feedback-independent-audit.json'),
        ref(spray+'mission-checks.json'))
    put('IMP', 'f', '未完整执行', '实测原8喷头径向深度、临时障碍截断、停泵风机保持及粒子消散，四张真实Gazebo多视角图已查看。'
        '停泵后图中无可见新增喷雾；进一步发现微小残余轴速可给出极低非零发射率，已改为停泵指令关闭新增发射但保留真实惯性轴速。'
        '新C++逻辑单测及两台PC编译完成，尚未用恢复后的实板复测新构建。',
        ['PC USB-CAN恢复后复测新发射门控，确认原喷头、遮挡及停泵全链显示；原图不冒充新构建实测'],
        ref(recent+'sprayer-visual-r1/spray-visual/result.json'))
    put('IMP', 'g', '通过', '碎枝实板输入3500，在PC形成(0,0,3500)，真实刀轴旋转；错误喷水通道拒绝且合法行走保持，新合法碎枝恢复后停稳。'
        '0x5B4原生和返回ROS各48个反馈，公共窗口45个逐字节在X5物理ISOBUS接收中匹配。3500是控制量，不是转速命令。'
        '首次自编检查把1.5秒制动瞬态当稳态而失败，原记录保留；复测按真实末尾0.5秒仿真停稳窗口检查，未改原需规或通信租约。', [],
        ref(cut_control+'controller-checks.json'), ref(cut_control+'channel-rejection.json'),
        ref(cut_control+'feedback-independent-audit.json'), ref(cutter+'mission-checks.json'))
    put('IMP', 'h', '通过', '悬挂10项实板专项完成上升、上限、位置、保持、下降、下限、断令保持和新命令恢复。'
        '两种传动原修复全程都按9000/0/9000顺序定位悬挂，实际0x631位置/速度闭环及阶段到位成立；单位仍为归一化计数。', [],
        ref(controls+'hitch.json'), ref(powershift+'mission-checks.json'), ref(cvt+'mission-checks.json'))
    put('IMP', 'i', '未完整执行', '已有真实遥测显示16片圆盘入土、滚筒实际旋转及非空历史痕迹；未新增整片棕色覆盖板。'
        '历史姿态统计包含重复发布样本，不能把其条数当唯一作业段数。',
        ['补两个工况抬升地头/离开后无新增痕迹、历史保留及连杆套筒的真实多视角检查'],
        ref('powershift-async-r1/harrow-work.json'))
    put('IMP', 'j', '未完整执行', '喷水和碎枝实际机具输入停止后转子减速，行走通道保持；悬挂断令保持及新输入恢复已实测。'
        '夹果错误保持请求拒绝和正常总停机清理已记录，未强行开爪。',
        ['补Car0夹持中任务安全终止及两种Car4工况机具独立断令/总停机的完整逐工况覆盖'],
        ref(spray_control+'controller-checks.json'), ref(cut_control+'controller-checks.json'),
        ref(controls+'hitch.json'), ref(apple+'held-fruit-guard.json'))

    put('NAV', 'a', '未完整执行', 'Car0/Car3实流PNG原图与解码RGB多重集严格匹配，保留同时间戳多帧；IMU、点云、GNSS及clock推进。'
        '两种Car4原修复轮的RTK/IMU传感审计通过，不要求未使用相机参与定位。',
        ['补最终冻结版本五工况输入/外参/网口配置闭合记录及缺压缩插件实际预检拒绝；软件依赖单测单列'],
        ref(apple+'sensor-audit.json'), ref(spray+'sensor-audit.json'), ref(cutter+'sensor-audit.json'),
        ref(powershift+'sensor-audit.json'), ref(cvt+'sensor-audit.json'))
    put('NAV', 'b', '未完整执行', '五工况预检及实际正常任务使用既定FAST-LIVO2+RTK或NavSat ESKF，'
        '必要输入、定位和Nav2激活后放行；没有用真值回灌定位。',
        ['逐工况补必要定位输入缺失/无TF/未就绪的真实停机与恢复试验'],
        ref(apple+'mission-checks.json'), ref(spray+'mission-checks.json'), ref(cutter+'mission-checks.json'))
    put('NAV', 'c', '未完整执行', '原地图、原出生点、Car0/Car3的1/2/3顺序、Car4五阶段和真实轴距保持不变；'
        '未靠换目标或移车绕过失败。Car0/Car3新轮与两种传动原修复轮均保存清单和输入指纹。',
        ['补最终构建五工况两端地图/车辆/任务哈希及切组无残留的完整闭合核验；历史通过结论仍在原台账'],
        ref(apple+'mission-checks.json'), ref(spray+'mission-checks.json'), ref(cutter+'mission-checks.json'),
        ref(powershift+'mission-checks.json'), ref(cvt+'mission-checks.json'))
    put('NAV', 'd', '未完整执行', 'Car0新轮完成去程采摘/完成段两阶段，Car3新轮按原1/2/3请求Smac路径并由原生MPPI执行；'
        '未新增定时全局重规划。原规划失败及保留剩余目标后的请求继续分别记录。',
        ['补最终版本两种Car4工况完整阶段请求计数和控制器接收证据；不把新阶段算无故重规划'],
        ref(apple+'mission-checks.json'), ref(spray+'mission-checks.json'), ref(cutter+'mission-checks.json'))
    put('NAV', 'e', '未完整执行', '原生MPPI仍按Car0/Car4 Ackermann、Car3 DiffDrive配置；本轮没有改轮廓、地图、速度或转向参数。'
        '路径和运动由X5原生算法通过物理CAN执行，保存独立实际轨迹。',
        ['补五工况指令、实际轮速/转向及侧滑/反向运动的完整关联审查'],
        ref(apple+'mission-checks.json'), ref(spray+'mission-checks.json'), ref(cutter+'runtime-attestation.json'))
    put('NAV', 'f', '未完整执行', 'Car0、喷水和碎枝新轮已经按消息仿真时间匹配真值，输出定位误差、路径折线偏差、P95/最大值、覆盖率及未匹配样本。'
        '没有用终点误差替代全程精度，也未跨机相减墙钟。',
        ['补两种Car4完整路线的新原始时间戳记录及三类误差统计；启动失败轮没有路径，不作通过'],
        ref(apple+'postcheck.json'), ref(spray+'postcheck.json'), ref(cutter+'postcheck.json'))
    put('NAV', 'g', '未完整执行', '当前原车体轮廓、机具规划排除、自车滤除配置未改；新只读探针区分raw254致命和253膨胀。'
        '已准备专用新障碍、Smac/MPPI、实际完整车体间距的有限实测工具，几何计算单测通过，但未开始五工况物理试验。',
        ['执行obstacle_jobs.json五组实板新障碍/代价图/实际避让及无安全路径的停车试验，不能以单测替代'],
        ref('controller-patience-r1/result.json'))
    put('NAV', 'h', '阻塞', 'Car0新轮一次任务双果后到3号点，喷水和碎枝新轮完成1/2/3及正确作业。'
        '两种传动原异步修复轮完成五阶段，但最终原生MPPI构建的Car4新全程被物理CAN启动故障阻塞，未运行后继CVT。'
        '首次Car0新增主动场景查询记录器干扰轮保留为失败，改被动订阅后的新轮通过，不隐藏失败尝试。',
        ['恢复PC USB-CAN后补最终两种Car4连续任务，保留原历史不通过及每轮尝试'],
        ref(apple+'postcheck.json'), ref(spray+'postcheck.json'), ref(cutter+'postcheck.json'),
        ref(recent+'powershift-native-r1/mission-checks.json', 'passed', False))
    put('NAV', 'i', '未完整执行', '旧Car3实板MPPI失败后曾保留原剩余3号目标并恢复，原失败仍保留。'
        '原生MPPI异常类型修复在真实controller_server软件夹具中按原2秒patience零输出后失败，空图新动作恢复且取消终态确认。'
        '该隔离软件夹具不算五工况物理故障验收。',
        ['逐工况补首次Smac失败、执行中MPPI失败、原动作终态、剩余目标及有界超限退出的物理分支'],
        ref('controller-patience-r1/result.json'), ref('mppi-native-contract-r1/result.json'))
    put('NAV', 'j', '未完整执行', 'Car0新轮终点5秒实际误差0.228380699m、最大速度0.001260313m/s；'
        '碎枝误差0.365773791m、最大速度0.001556369m/s，喷水亦通过。全都无新规划，严格使用原半轴距，采摘仍0.10m/0.15rad。'
        'Car0固定0.3m复核和Car3隐含10cm余量已删除，单测覆盖边界及非有限误差。',
        ['补最终构建两种Car4完整任务后的5秒独立窗口；原修复轮通过记录仅作为已有证据，不冒充新轮'],
        ref(apple+'terminal.json'), ref(spray+'terminal.json'), ref(cutter+'terminal.json'),
        ref(powershift+'terminal.json'), ref(cvt+'terminal.json'))

    if args.current:
        required = ['five-native-normal-runs-review-r1.json', 'scene-attestation-r1.json',
            'normal-campaign-r4/result.json', 'sprayer-visual-review-r3.json',
            'interfaces-1m-r1.json', 'interfaces-250k-r1.json']
        for name in required:
            value = json.loads((ROOT/'.validation/hil_acceptance_20261116'/name).read_text())
            if value.get('passed') is not True:
                raise ValueError('The reviewed milestone is not complete: '+name)
        full = ref('five-native-normal-runs-review-r1.json')
        scenes = ref('scene-attestation-r1.json')
        normal3 = 'normal-campaign-r3/'
        normal4 = 'normal-campaign-r4/'
        put('VCU', 'a', '通过', '重插后五工况完整任务均实际烧录并完成正确固件、速率、模型、算法及任务绑定。'
            '同一喷水工况两次烧录中取消均等校验结束后清理；错误组合、算法、缺传感器和未绑定烧录器均实际预检拒绝。'
            '每轮保留Programming Finished、Verified OK及授权顺序。', [], full,
            ref('flash-repeat-cancel-r1/qualification.json'), ref('negative-preflight-r1/result.json'))
        put('VCU', 'b', '通过', '五种完整实板任务均通过两段实体CAN初始化及新鲜反馈门控；Car0为1M，Car3/Car4为250k。'
            '只读实际接口检查分别拒绝错误序列号、错误速率和未许可接口。重插前内部回环5发0收及任务启动失败均原样保留；'
            '重插后诊断5帧完成只作为恢复依据，不单独替代双向完整任务。', [], full,
            ref('interfaces-1m-r1.json'), ref('interfaces-250k-r1.json'), ref('neutral-user-replug-r8.json'))
        put('VCU', 'g', '通过', 'CVT及动力换挡选定工况分别完成5组合法参数和17种非法配置实板专项，'
            '包含数值边界、步长、错误类型、非法模式和不匹配挡位/速比；应答与实际使能关闭/制动逐项核对。'
            '另有运行中换模式拒绝和变化率设置实测，不改真车参数。', [],
            ref(controls+'parameters/result.json'), ref(normal4+'tractor-powershift-parameters-r1/parameters/result.json'),
            ref(normal4+'tractor-cvt-watchdog-r1/powertrain/result.json'),
            ref(normal4+'tractor-powershift-watchdog-r1/powertrain/result.json'))
        put('VCU', 'j', '通过', 'Car0双果、Car3喷水/碎枝及Car4两种传动五个正常完整任务均闭合，'
            '独立CAN样本中通信错误、故障及重建均0；原始证据SHA均一致。结束实际停车、停止机具、撤销授权及清理完成，'
            '真车保护文件未变。旧启动失效和旧运行失败不覆盖；本结论只针对已列明的正常复测轮，不宣称所有故障专项通过。', [], full)
        record = table['VCU-001-d']
        record['observed'] += ' 最新另完成Car4 CVT禁止授权专项，合法非零请求仍不能使底盘或机具执行。'
        record['remaining'] = ['补全部工况指令与实际轮速/转向对应的完整独立审查']
        record['assertions'].append(ref(normal4+'tractor-cvt-authority-r1/authority.json'))
        record = table['VCU-001-i']
        record['observed'] += ' 两种Car4工况现均完成实际反馈中断保护、停稳及新请求恢复。'
        record['remaining'] = ['补物理断线或身份错误不得作为普通拥塞自动续跑的实际故障分支；不放宽0.30秒']
        record['assertions'] += [ref(normal4+'tractor-cvt-watchdog-r1/powertrain/result.json'),
            ref(normal4+'tractor-powershift-watchdog-r1/powertrain/result.json')]
        put('IMP', 'f', '通过', '新构建实板复测原8喷头、径向射线、临时障碍截断、停泵风机保持和粒子消散均通过。'
            '停泵时实际轴速仍64.584rad/s，发射输入已严格归零；不把真实惯性反馈强改为零。'
            '四张真实Gazebo图已逐张查看，未使用合成图。首次新检查失败源于gz小消息输出缓冲，原失败保留；'
            '改行缓冲后仍按原1秒新鲜度检查，未放宽门限。', [],
            ref(normal4+'sprayer-visual-r3/spray-visual/result.json'), ref('sprayer-visual-review-r3.json'))
        put('NAV', 'c', '通过', '实读两端四套运行目录（覆盖五工况）的地图像素、mission、Nav2及传感器设置并逐文件比较，'
            '二进制资产哈希一致；仅两个配置文件的明确主机运行根路径不同，保留原SHA及路径归一化SHA。'
            '完整任务仍按原出生点、真实轴距和原目标顺序执行，切组前确认上一任务清理。', [], scenes, full)
        put('NAV', 'd', '通过', '五种完整任务的原生Smac规划动作数分别为2、1、1、5、5，与正常阶段一致，'
            '返回路径经原生MPPI执行到完成；终点后无新增规划。没有新增定时全局重规划，失败轮另行归档。', [], full)
        put('NAV', 'f', '通过', '五工况完整轮均保存消息原始仿真时间戳的定位、真值及路径，分别输出XY定位误差、'
            '折线跟踪偏差和最终位置误差，保留样本数、覆盖率、插值间隔、P95/最大值及未匹配样本。'
            '不把跨机墙钟差当延迟，不外推厘米级农艺精度。', [], full)
        put('NAV', 'h', '通过', 'Car0一次连续双果任务后到3号点，Car3两种机具按1/2/3完成，Car4两种传动各完成'
            '接近、第一作业行、抬耙地头、第二作业行及停车。五轮正常阶段无额外规划、通信故障或未解释中断。'
            '所有旧失败和测量工具失败保留，不重置车辆或跳点伪造本轮成功。', [], full)
        put('NAV', 'j', '通过', '五工况均完成终点后5秒仿真独立观察，实际速度低于原0.02/0.03m/s门限，'
            '无新增规划且位置均在各自真实半轴距内。最新动力换挡/CVT误差分别0.962807381m及0.993504949m，'
            '均小于1.092562096m。Car0采摘仍独立使用0.10m/0.15rad，不用最终容差替代。', [], full,
            ref(apple+'apple-independent-audit.json'))

    ledger = dict(date='2026-11-16', scope='开发方隔离硬件在环台架修复复测阶段记录，不是第三方全通过证书',
        requirements_version='V2.0 unchanged', case_count=30,
        fixes=[
            'Car4将日志写盘移出通信/控制循环；队列有界且错误显式返回。保持原0.30秒反馈门限。',
            '反馈使用真实接收到达单调时刻，排空事件队列后重取now，修复将本轮较晚到达帧误认未来帧的竞争错误。',
            '只在独立HIL覆盖层构建同版本Nav2 1.1.20原生MPPI，以PlannerException接入原2秒控制器patience；未更换算法、轮廓或参数。撤回的适配器原型不交付。',
            'Car3及Car0最终位置复核统一原真实半轴距，拒绝非有限误差；采摘专用位置/方向条件不放宽。',
            'PC仿真/CAN进程退出时，启动授权前及运行监控立即拒绝继续，新增单测通过；该新防护的最终实板全程因USB-CAN失效待补。',
            '停泵指令关闭新增喷雾，同时保留真实轴速惯性反馈；两台PC已编译，新实流复测待硬件恢复。',
            '测试记录修正同时间戳多图像匹配、raw253/254区分、单侧动力审计漏采、停稳窗口和只读观测干扰；原失败均保留，不改变原需规判据。',
            '当前外部阻塞：直连PC USB-CAN内部回环5发0收，单设备USB复位未恢复，6750 JTAG识别正常；已请求现场重插。所有执行授权撤销，原真车工作区未改。'],
        methods=[identity+' '+text for identity, text in METHODS], records=records)
    if args.current:
        ledger['milestone'] = 'Post-replug normal five-run and controller campaign; remaining negative branches still explicit'
        ledger['figures'] = [dict(path='.validation/hil_acceptance_20261116/normal-campaign-r4/'
            'sprayer-visual-r3/spray-visual/'+name+'.png', caption=caption) for name, caption in [
                ('before-obstacle', '喷水机具运行：原环形喷头、绿色机架、白色水箱及灰色车体'),
                ('with-obstacle', '实际加入测试障碍，并与原喷头的碰撞射线截断记录联合核对'),
                ('pump-stopped', '停泵并等待既有粒子寿命结束：无可见残留水雾，风机状态由遥测核对')]]
        ledger['fixes'][4] = 'PC仿真/CAN进程退出时，在授权前和运行监控立即拒绝继续；新防护已随两种传动完整实板轮运行。'
        ledger['fixes'][5] = '停泵立即关闭新增喷雾但保留惯性轴速，新构建实板及四张真实画面复核已通过；gz记录器改为行缓冲，不改新鲜度门限。'
        ledger['fixes'][-1] = '现场重插后PC USB-CAN恢复，两种传动及剩余正常专项均完成。旧回环0收/发送堵塞原日志保留；不能仅据重插恢复确认供电或接触的具体硬件根因。'
        ledger['fixes'].append('新只读历史观察已在两种传动完整轮确认作业增长及抬升后历史保留；渲染截图暴露白边，显示修复和专项复测另行进行，不预先把整条土壤显示判通过。')
    assert len(records) == 30 and all(r['observed'] and (r['remaining'] or r['verdict'] == '通过') for r in records)
    args.output.write_text(json.dumps(ledger, ensure_ascii=False, indent=2)+'\n')
    from collections import Counter
    print(json.dumps(dict(counts=Counter(r['verdict'] for r in records), output=str(args.output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
