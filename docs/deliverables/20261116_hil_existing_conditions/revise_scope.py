"""Version the user-approved, existing-scene scope without overwriting V2.0."""
import argparse
from copy import deepcopy
import csv
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]

REVISIONS = {
    'VCU-c': dict(title='正常通信与代表性反馈一致性核对',
        preconditions='使用已部署的五种冻结工况及正常物理CAN链路，不接入真实执行器，不注入非法原生帧。',
        steps='复核五工况正常收发及既有原生协议；对已有喷水、碎枝及两种传动记录做代表性反馈负载核对。',
        acceptance='Car0保持0x181及0x101/0x102/0x103，Car3保持0x514及0x532/0x533/0x534，Car4沿用独立悬挂/动力协议。五工况应有实际收发及运动/机具反馈；已有代表性喷水、碎枝和动力反馈的收到负载应一致。记录适用协议和匹配数量，不外推零丢包；不以跨机ROS执行指令替代实体CAN。',
        evidence='正常任务收发状态、既有三方机具反馈审计及Car4四类动力反馈审计。',
        excluded='本版不开展逐工况非法CRC、非法字段、错误方向的原生帧注入，不声明完整协议鲁棒性认证。'),
    'VCU-d': dict(title='底盘授权、正常行驶与停机',
        preconditions='五种冻结工况正常就绪，使用已记录的授权专项和正常任务，实际车体状态可观测。',
        steps='核对禁止授权时的非零请求结果，再观察正常任务中的直行、转弯及任务结束停车。',
        acceptance='未授权时实际车辆不得执行非零运动；正常授权任务应有实际位置和朝向变化，结束后按车型原停车阈值停止。不得用控制目标冒充实际运动或直接改车辆位姿。只做功能级直行、转弯和停车检查，不要求逐车轮逐帧转角/轮速标定。',
        evidence='五工况授权专项、独立车体位姿/速度记录、终点停车及撤销授权记录。',
        excluded='取消逐轮心、逐帧转角及指令—轮速的精密一致性试验，不声明底盘动力学标定精度。'),
    'VCU-i': dict(title='受控断令及反馈暂停保护',
        preconditions='仅使用隔离台架及已完成的有限故障专项；正常链路可用，不拔USB、不接错设备。',
        steps='复核既有停止高层输入、短时暂停隔离收发进程的保护与恢复记录，分别记录正常和故障窗口。',
        acceptance='受控断令或反馈暂停后应按程序原有租约进入零执行/制动，实际车辆停止；恢复应依赖新鲜反馈及新的合法请求，不重放旧目标。Car4原0.30秒状态门限不变。专项保护成功与正常任务中意外停机分别记录。',
        evidence='既有Car0/Car3断令与USB所有者暂停专项、两种Car4传动的反馈中断专项。',
        excluded='不再要求现场物理拔线、错误设备身份接入或所有异常组合穷举；不据本用例宣称上述未测情形已验证。'),
    'IMP-a': dict(title='冻结机具配置与正常装载',
        preconditions='使用当前软件已登记的Car0机械臂、Car3喷水/碎枝、Car4圆盘耙五种组合。',
        steps='核对工况目录、资源和控制通道，结合正常任务及代表性运行画面确认机具装载；复核已有Car3错通道拒绝专项。',
        acceptance='五种工况的车型、机具类型和任务应与目录一致，模型资源可加载，实际作业通道与所挂机具相符。代表性Car3喷水/碎枝错通道应被拒绝，新合法请求可恢复。不进行安装孔位尺寸、全部姿态或任意未登记组合的机械适配验收。',
        evidence='工况目录、场景/模型资源、正常作业记录、已有错通道测试和代表性实际运行截图。',
        excluded='取消全车型机械安装尺寸及Car0/Car4全部错误装配组合穷举；不将基本加载视为真实机械安装认证。'),
    'IMP-i': dict(title='圆盘耙基础作业与痕迹显示', profiles=['tractor_cvt'],
        preconditions='以既有Car4悬挂圆盘耙/CVT为代表工况，沿用原场景土壤和现有模型，不增加测试物体。',
        steps='观察下放前进、圆盘/滚筒状态、抬升及已作业区域的历史显示，保存已有遥测和实际画面。',
        acceptance='代表工况下放前进时16片圆盘应有入土遥测，后滚筒实际旋转；作业过程应形成可见痕迹，停止作业后历史记录保留且不继续增加作业段。按基础功能显示验收，不考核全部连杆姿态、细微材质效果、土壤力学或农艺碎土指标。',
        evidence='代表工况16圆盘高度、滚筒速度、实际作业历史及真实Gazebo截图；注明记录对应构建。',
        excluded='由两种传动分别做完整视觉细节验收改为CVT代表性基础功能；动力换挡仅引用共享模型及其既有正常全程作为旁证。'),
    'IMP-j': dict(title='正常任务停机与代表性机具断令',
        preconditions='五种冻结工况有正常任务结果，已有喷水、碎枝及悬挂断令专项可追溯。',
        steps='复核五工况正常任务结束状态及已有代表性单通道断令记录，检查收臂、停转、保持与授权撤销。',
        acceptance='正常任务结束应停止行走和机具输出，机械臂完成收臂放果，不能强行张爪丢果；喷水/碎枝断令应减速停止且不篡改合法行走，悬挂断令按既有策略保持。允许用已完成的代表性专项验证共享控制链，不要求各工况重复全部异常中断组合。',
        evidence='五组正常结束记录、机械臂独立审计、已有喷水/碎枝停令和悬挂保持专项。',
        excluded='不做夹持苹果中途强制终止、每工况每通道独立故障穷举，不将未测分支写成通过。'),
    'NAV-a': dict(title='正常依赖下的网口传感器接入',
        preconditions='沿用已安装ROS依赖和当前DDS网口配置，不卸载或屏蔽相机压缩插件。',
        steps='复核五工况所需点云、图像、IMU、GNSS及clock的正常接收与时间推进，检查当前外参文件及已有PNG像素审计。',
        acceptance='当前所需传感器应经配置网口到达X5，消息时间推进，外参文件与工况对应。Car0/Car3正常PNG压缩/解码记录应保留尺寸、时间戳和像素一致性；Car4按RTK/IMU输入验收。不在本版中人为移除压缩插件验证缺依赖分支。',
        evidence='五工况传感审计、已部署外参/地图目录一致性及DDS网口配置记录。',
        excluded='删除缺压缩插件的现场负向注入要求；不降低正常PNG像素一致性，也不声明真实传感器硬同步。'),
    'NAV-b': dict(title='正常输入下的定位和启动就绪',
        preconditions='使用正常、完整的既有传感输入和配置，运动授权先关闭，不暂停定位节点或TF。',
        steps='核对五工况定位算法和实际输出，检查等待就绪、Nav2激活及任务授权的正常启动顺序。',
        acceptance='Car0/Car3使用既定FAST-LIVO2+RTK，Car4使用NavSat ESKF；正常输入下应有定位输出，先完成就绪等待再提交任务和授权。正常路线应可完成，不以Gazebo真值替代X5定位。只验证正常启动，不开展缺输入、缺TF或定位中断的现场故障注入。',
        evidence='实际启动阶段、等待就绪成功记录、定位配置及全程独立位姿/传感记录。',
        excluded='取消五工况逐项屏蔽输入、TF及未就绪强制注入；现有保护逻辑不删除，但本版不签署其完整负向覆盖。'),
    'NAV-e': dict(title='车型运动学配置与正常路线跟踪',
        preconditions='沿用五种已冻结车辆、原生Smac/MPPI、原始默认路线及当前CAN链路。',
        steps='核对Nav2运动模型及车辆参数，结合完整任务实际位姿确认车辆发生正常直行和转弯。',
        acceptance='Car0/Car4配置Ackermann，Car3配置DiffDrive，阿克曼规划沿用前进搜索。实际车辆应沿默认路线运动并到点，不能用侧向瞬移或直接设车位姿代替控制。按模型选择、基本行驶和任务完成验收，记录实际运动但不做逐轮速度/转角、全程侧滑和加速度的定量标定。',
        evidence='两端Nav2配置、真实轴距相关参数、正常任务及独立车体轨迹/停车记录。',
        excluded='取消全车型逐轮定量运动学、侧滑及每帧加速度校核；不修改控制器限速、车体尺寸和规划轮廓。'),
    'NAV-g': dict(title='既有场景障碍与规划轮廓核查', profiles=['tracked_sprayer'],
        preconditions='以苹果园履带喷水工况为代表，使用原有树木和场景边界，不加入任何测试障碍物。',
        steps='检查现有地图/代价图中的原有障碍及底盘轮廓配置，复核既有默认路线运行和只读观察。',
        acceptance='原场景地图和代价图中应保留已有障碍，底盘规划轮廓与冻结配置对应，不把挂载机具扩大为底盘规划轮廓。代表工况默认路线应正常通行，已有只读记录不得显示车体内部持续致命栅格。不开展突发动态障碍、强制绕行或无路场景注入，不清除原有障碍、缩车或关闭避障。',
        evidence='原场景地图、冻结轮廓、代表工况原始代价图只读记录及完整正常路线。',
        excluded='由五工况新障碍/无安全路径试验改为代表性既有障碍与配置核查；不声明动态避障能力已全面验证。'),
    'NAV-i': dict(title='代表性失败记录与有限恢复核查', profiles=['tracked_sprayer'],
        preconditions='使用已归档的履带喷水实际失败/恢复记录及同版本原生MPPI软件夹具，不改现有场景。',
        steps='复核既有控制失败的终态及后续剩余目标，再复核软件夹具中MPPI失败、零输出和新动作恢复。',
        acceptance='既有代表性实板控制失败应被明确记录，后续规划应保留尚未通过目标的顺序；受控软件夹具应明确区分软件与实板范围，验证MPPI按原patience返回失败、零输出及新动作恢复。仅对实际取得的记录和软件夹具判定，不要求五工况重新强制触发首次Smac失败、重试上限及全部中断分支。',
        evidence='既有实板失败及恢复目标记录、原生controller_server软件夹具结果与输入/构建标识。',
        excluded='取消五工况逐类现场故障和上限退出穷举；软件夹具不得标作本轮实板异常试验。'),
}


def revised(original):
    result = deepcopy(original)
    result.update(version='V2.1', date='2026年11月16日',
        document_id='AGRIBOT-SRS-HIL-20261116-V21',
        revision_basis='用户2026-11-16明确要求：场景不额外加物体，剩余11条降低难度，按现有测试条件调整。第三方审核及签署待完成。')
    result['scope'] = [
        '沿用整机控制器、智能机具控制器、自动驾驶导航控制器三类，各a至j十条，共30条，原用例编号不变。',
        'V2.0保留归档；V2.1只调整上一版19条通过之外的11条测试范围。已经满足的19条判据不变。调整是范围修订，不代表原V2.0全部判据已经通过。',
        '仅使用既有苹果园、宽地头、默认车辆和机具，不再添加测试障碍物或其他场景对象。以正常功能、代表工况、既有实板记录复核和明确标注的软件夹具为主。',
        '不进行物理拔线、错误接线、卸载依赖、屏蔽传感器/TF或夹持中强制终止等额外现场故障注入。原保护逻辑、停车容差、CAN协议和0.30秒反馈门限不修改。',
        *original['scope'][2:]]
    changes = []
    for section in result['sections']:
        group = section['test_prefix'].split('-')[2]
        for case in section['cases']:
            key = group+'-'+case['letter']
            if key not in REVISIONS:
                continue
            before = deepcopy(case)
            revision = REVISIONS[key]
            case.update({k: v for k, v in revision.items() if k != 'excluded'})
            case['excluded_scope'] = revision['excluded']
            changes.append(dict(id=section['test_prefix']+'-'+case['letter'],
                original=before, revised=deepcopy(case), reason=revision['excluded']))
    assert len(changes) == 11
    return result, changes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--scope-approved', action='store_true')
    args = parser.parse_args()
    if not args.scope_approved:
        raise SystemExit('Explicit user approval of the changed scope is required')
    source = ROOT/'docs/deliverables/20261112_hil_srs/requirements.json'
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    original = json.loads(source.read_text())
    data, changes = revised(original)
    args.output.mkdir(parents=True, exist_ok=False)
    (args.output/'requirements.json').write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n')
    (args.output/'11条范围调整对照.json').write_text(json.dumps(dict(
        original_version='V2.0', revised_version='V2.1', source_sha256=digest,
        case_count=30, changed_count=11, unchanged_count=19,
        approval=data['revision_basis'], changes=changes), ensure_ascii=False, indent=2)+'\n')
    with (args.output/'11条范围调整对照.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['用例编号', '原项目', '修订项目', '原判据', '修订判据', '原适用工况', '修订适用工况', '本版不覆盖的范围'])
        for change in changes:
            a, b = change['original'], change['revised']
            writer.writerow([change['id'], a['title'], b['title'], a['acceptance'], b['acceptance'],
                             ','.join(a['profiles']), ','.join(b['profiles']), change['reason']])
    assert hashlib.sha256(source.read_bytes()).hexdigest() == digest
    print(json.dumps(dict(version=data['version'], cases=30, revised=11, output=str(args.output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
