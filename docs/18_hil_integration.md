# 硬件在环集成

状态：2026-11-11正在进行Unity入口集成和实板复验，不能把下表的既有独立HIL验证等同于新入口的全部验收已通过。桌面正式程序应在新入口全程验证后再替换。

## 工况和入口

沿用现有车库、装备和场景/算法配置面板，在“启动仿真”旁增加“启动HIL仿真”，共用停止入口。只对Car0、Car3、Car4开放HIL入口；组合不匹配时不允许提交。新增风送植保机和碎枝机在原装备面板内选择，只挂载到Car3，不另建装备页面。

| 方案ID | 车辆和机具 | 场景与默认任务 | 算法与通信 |
| --- | --- | --- | --- |
| ackermann_apple | Car0 + Lite6机械臂 | 苹果园1→2→3，途中采摘，到3结束 | FAST-LIVO2+RTK、Smac Hybrid-A*、MPPI；底盘原CAN，机械臂仍网口/ROS |
| tracked_sprayer | Car3 + 风送植保机 | 苹果园1→2→3，行走时喷水 | FAST-LIVO2+RTK、Smac Hybrid-A*、MPPI；原差速CAN与ISOBUS农具同总线 |
| tracked_shredder | Car3 + 碎枝机 | 苹果园1→2→3，行走时刀轴工作 | 同上；碎枝机应用报文经6750转换 |
| tractor_powershift | Car4 + 圆盘耙 | 宽地头，接近、第一作业行、抬升转弯、第二作业行、抬升停靠 | NavSat ESKF、Smac Hybrid-A*、MPPI；专用悬挂及动力换挡HIL协议 |
| tractor_cvt | Car4 + 圆盘耙 | 同上，CVT自动模式 | 同上，连续传动比闭环 |

方案唯一目录为`unity/Assets/AgribotConfig/Resources/hil_profiles.json`。拖拉机的CVT和动力换挡在现有配置面板中切换，默认CVT。HIL使用版本化默认模型、参数、传感器、场景和任务输入，不将任意修改后的配置宣称为已验证工况；选择了不匹配算法时提示确认，不悄悄替换用户选择。普通仿真的算法组件仍可独立人工调整，伏羲农场104点绑定规则不变。

## 硬件与隔离

仿真PC运行Unity、Gazebo物理与传感器、CAN反馈端；X5运行导航、作业任务和控制端；6750执行该工况的中继或控制固件。网口承载ROS消息，实际CAN承载底盘及农具命令和反馈，不能用PC内部ROS直连替代CAN闭环。

当前台架固定PC网口192.168.1.1、X5网口192.168.1.102；ROS域73用于Car0/Car3，74用于拖拉机。CycloneDDS单播、分片和缓冲沿用已验证配置。HIL CAN盒、烧录器按序列号绑定，不能把连接真车执行机构的设备当成台架使用。

- X5真车目录`/home/sunrise/agribot_ws/ros2_ws`只读使用已有算法依赖，禁止向其中复制、编译、部署或修改服务。
- X5新增代码、编译、安装、场景和日志全部位于`/home/sunrise/agribot_app_hil_ws`。
- PC生成运行目录为仓库`.local/hil`；它是运行所需的编译/场景缓存，不是源码备份。
- 每次启动前后核对真车源码、安装项和用户服务配置的SHA-256清单。源码、安装项或服务变化会阻止HIL启动，需要人工核对，不能自动重建基线掩盖变化。两份上游运行日志`FAST-LIVO2/Log/mat_pre.txt`、`mat_out.txt`的变化单独记录，不当作源码修改。
- 检测到非本应用HIL进程组中的真实底盘驱动时拒绝启动，不停止真车服务、不结束其他用户进程。
- 不升级Ubuntu；继续使用22.04和ROS 2 Humble。

修改前基线已保存在GitHub标签`before-hil-integration-20261111`，不保留本地整仓复制备份。

## 自动烧录顺序

每次点击启动，即使连续两次选择同一工况，也完整执行一次烧录：

1. 校验工况、场景包、ELF哈希及烧录器/CAN身份，检查X5独立运行环境。
2. 停止本应用上一轮任务，撤销行走和作业授权，释放HIL通信。普通受管仿真也先停止。
3. 比较X5真车工作区指纹。
4. 用绑定的CMSIS-DAP烧录器下载对应ELF，必须收到Programming Finished和Verified OK，再复位运行。不对整片Flash擦除，不覆盖末尾配置区。
5. Car0使用1Mbps，Car3和Car4使用250kbps；只配置绑定的PC台架CAN盒，X5仅准备专用vcan73/vcan74，不修改其真实物理CAN配置。
6. 启动PC场景和X5算法，等待新鲜硬件反馈、推进的仿真时钟、定位与Nav2生命周期就绪。
7. 再核对真车指纹，然后授权并执行默认任务。尚未完成就绪检查时不放行行走。

任何失败都会停止HIL并撤销授权，不能假装烧录成功或回退到纯软件控制。烧录中点击停止只提交停止标志，等写入和校验完成后处理，不能中断Flash写入。该功能不负责给真车烧录或改动真车CAN协议。

## 首次准备

普通仿真安装流程不受影响。HIL还需在PC配置免密SSH密钥和可信X5主机指纹、HPM OpenOCD及匹配的HPM SDK OpenOCD配置文件。不要在Git中保存密码或私钥。

按`tools/hil/connection.example.json`的字段创建`.local/hil/connection.json`，实际路径必须对应本机安装。烧录器序列号应填经检查的FireDAP序列号。运行前先通过只读工具记录真实运行环境指纹，例如：

```bash
python3 tools/hil/protect_real_runtime.py \
  --host sunrise@192.168.1.102 --identity ~/.ssh/agribot_hil_app_ed25519 \
  --known-hosts ~/.ssh/known_hosts \
  --output .local/hil/protected-real-runtime.json
python3 tools/hil/deploy.py
sudo python3 tools/hil/install_permissions.py --user "$USER" --probe-serial <已核实的序列号>
```

部署工具仅将列明的HIL源码、场景和固定外部依赖传入专用目录；不上传Unity资产库、不复制真车工作区。PC编译Gazebo插件及附着服务，X5编译Python叠加包、独立FAST-LIVO2和两个ISOBUS端点。AgIsoStack++与IFRA LinkAttacher固定提交并校验，X5无需自行访问GitHub。FAST-LIVO2必须独立编译，因为上游二进制固化的ROOT_DIR会将调试日志写回原源码目录；其他已有ROS依赖仅作只读下层。不宣称在没有ROS依赖的新系统上开箱即用。

权限准备只给绑定烧录器的USB/HID接口授予plugdev访问，并只允许操作已绑定PC CAN盒的250kbps和1Mbps两个命令，不授予任意root命令或系统级网络能力。

## 操作与日志

在车库选车，打开装备面板安装适配机具，进入仿真设置选相应场景，拖拉机再选择CVT或动力换挡，点击“启动HIL仿真”。观察烧录、启动、定位就绪和任务状态；结束或中断使用原“停止仿真”入口。任务完成后可保留Gazebo查看效果，但行走及作业已撤销授权；切换到普通仿真前先停止HIL。

命令行诊断接口对应同一启动链路：

```bash
python3 tools/hil/cli.py start --profile tractor_cvt
python3 tools/hil/cli.py status
python3 tools/hil/cli.py stop
```

PC每次运行保存于`.local/hil/runs/<run_id>`，含步骤、烧录、操作、真车指纹核对和任务结果。X5日志为专用目录`runs/<run_id>`，保留CAN和ISOBUS原始记录。失败记录不覆盖，不能只保留成功截图。

## 当前验证记录

- 31项Python测试通过，覆盖白名单、每次必烧录、失败清理、取消、ELF范围、路径隔离、场景包重定位和授权顺序。
- Unity HIL目录3项Editor测试通过；懒加载车库、两种Car3机具安装切换及相邻按钮不重叠的PlayMode测试通过。
- PC和X5独立环境编译通过，X5新增悬挂/动力/地表4项C/C++测试通过。
- 部署前后X5真车工作区7064项指纹一致。
- 新启动链路`tractor_cvt`已完成实板烧录、校验及5阶段全程任务，记录ID为`20261111T113200_559603ed`；完成后撤销授权，真车指纹一致。此项目前来自与Unity共用的CLI入口，不替代所有组合的界面端验收。
- Unity实际点击已成功提交`tracked_sprayer`并自动烧录；初次场景启动发现XML编码声明兼容问题和CAN瞬时状态未就绪，未放行车辆。随后保护检查捕获旧FAST-LIVO2二进制清空真车目录两份调试日志的行为。源码、安装项和服务无变化，原清单未被覆盖；该隐式写入通过独立编译隔离，不将失败记录改成通过。
- 原独立HIL版本已有完整工况记录；新Unity集成链路的跨组全程复验尚未完成，不能据此签发整体验收通过。

ISOBUS使用固定AgIsoStack++的网络管理及私有应用PGN完成台架控制，不等于通过AEF认证，也不声称实现全部UT/TC功能。喷雾遮挡、圆盘碎土和压碎痕迹为可视化效果，不是农药沉积或土壤力学精度验证。
