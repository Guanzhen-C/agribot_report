# FieldGate VCU 农机控制台

作者：刘泽禹。面向专业农机用户的本机控制台，VCU 上接算力模块、下接不同执行器，不以执行器品牌命名产品。

Launchpad 桌面提供总览、车辆预览、算力与感知、设备、诊断与设置入口。车辆预览载入两套真实装配模型，默认只点选执行机构，功能关联是可选项。雷达、相机与算力安装关系在独立页面管理，可生成 URDF 与实际网格包。总览显示电量、作业状态和能量趋势；诊断页提供告警、反馈筛选与记录导出。协议细节集中在诊断和设置中。

## 启动

需要 Node.js 22.13 或更高版本。在本目录执行：

```powershell
npm install
npm run dev
```

用电脑上的 Edge 或 Chrome 打开 **http://localhost:3000/launchpad**，车辆预览在 **http://localhost:3000/preview**。仪表演示入口为 http://localhost:3000/launchpad?preview=1，提供植保作业、停车待机、液位预警三种场景，不请求串口、不写设备配置、不发送运动指令。模型配置可以保存到本浏览器草稿，界面明确区分草稿与设备确认。

开发服务监听 `0.0.0.0`。其他设备在同一局域网内可打开 `http://电脑的局域网IP:3000/launchpad`；如端口被占用，执行 `npm run dev -- --port 3001` 并使用 3001 端口。`0.0.0.0` 是监听地址，不是浏览器访问地址。

本地开发使用 Node 服务端渲染，`npm run build` 仍保留 Cloudflare 构建适配。局域网 HTTP 不提供 `crypto.subtle`，页面链接在此环境使用整页导航；localhost 或 HTTPS 使用客户端导航。串口连接仍需设备所在电脑上的 localhost 或受支持的 HTTPS 安全上下文，其他设备访问网页不会共享这台电脑的串口。

退出预览后，可点击“连接设备”并选择实际串口。安装依赖如需代理，只在当前命令环境中配置，不修改全局 Git 配置。

页面通过本机串口连接设备，不上传设备数据。未收到有效设备回复不会显示保存成功；打开端口本身并不能证明设备身份。不要同时用其他软件占用同一个端口。

## 三维资源与代码同步

仓库已包含两套内置 GLB 和桌面预览图，资源提供者已确认有权分发：`public/models/ackermann.glb`、`public/models/ackermann-flagship.glb` 和 `public/models/ackermann-poster.png`。克隆后可直接使用内置装配预览、URDF 导出及完整模型测试，无需另行补齐这些资源。也可使用“导入模型 / 调尺寸”载入自己有权使用的模型。本机检查截图不随代码同步。

## 型号与兼容性

保留原版履带差速农机及三合一 CAN 协议，并提供差速、四驱、转向、横移、全向农机配置。已有注册键、ID、位速率保持不变，以兼容保存记录与固件；兼容性不等于已完成所有执行器的实机验证。旧固件会拒绝不认识的型号，不应绕过拒绝检查。

`lib/three-in-one-protocol.mjs` 按用户提供的原始 DBC 解析仪表所需反馈字段，包括电池 SOC、电压、电流、驱动转速与温度、作业状态等。原始 DBC/XLSX 文件不改写。预览通过标注为示例的字节帧生成状态；当前仪表尚未接入实机连续遥测，退出预览后未知数值显示 `—`，不会以示例数值冒充设备反馈。

`lib/hechuan-protocol.mjs` 保留运动设备的 11 字节串口指令、标准 CAN 指令与 24 字节反馈格式。转向型号的串口角速度和 CAN 转角使用不同接口。当前页面用于配置，**不发送运动指令**。

## 开发与验证

```powershell
npm run typecheck
npm run lint
npm test
```

- `docs/PROGRAM_API.md`：程序接口；保留 `window.fieldGateConfigurator` 以兼容已有调用。
- `docs/SERIAL_CONFIG_PROTOCOL.md`：配置帧、注册 ID 和持久化约定。
- `docs/RUNTIME_COMMAND_PROTOCOL.md`：既有运行链路设计，不代表已经完成实机运行接入。
- `../docs/HECHUAN_PROTOCOL_ANALYSIS.md`：协议依据、源码分析、实现边界及安全注意事项。
- `.hallmark/`：界面设计约定与检查记录。
- `../docs/FGMP_MACHINE_DESCRIPTION.md`：固定三合一上游的通用农机描述、配置包与离线工具；示例位于 `../profiles/`。
- `../docs/VISUAL_CONFIGURATION.md`：三维配置使用方法、两套模型溯源、转换工具、配置扩展、验证记录及当前边界。
- `../docs/URDF_EXECUTOR_WORKFLOW.md`：执行机构分层、机器人描述、URDF 导出、部件 ID 互通、运动参数核验及使用边界。页面入口 **http://localhost:3000/robot**。

配置协议与运动协议是两条不同链路，不可把 11 字节运动帧发往配置串口。新 C 协议模块已纳入 HPM 工程，运行总线初始化、持续转发和实机安全联锁仍待实现与验证。

本轮验证：构建及 29 项自动测试通过；七页在 320、375、414、768、1280、1440、1920 宽度下检查布局。两套实际模型的 URDF/STL 导出、尺寸还原、独立 ROS 解析及浏览器配置/下载流程通过。原串口 ACK/拒绝/校验失败/超时/断连模拟回归通过。基础三维配置阶段 HPM 交叉编译通过，但新 FGMP 核尚未接入主循环和板端配置管理；本轮未改固件、未烧录或发送执行器指令。依赖审计告警与性能限制见三维配置文档，不将本机验证等同于 RViz、仿真或实机验收。
# 模型导入与尺寸调整

在「车辆预览」或「算力与感知」点击 **导入模型 / 调尺寸**。

1. 导入一个 GLB、glTF、URDF 或 Xacro（`.xacro`、`.urdf.xacro`，也兼容误写的 `.xcaro`）；带引用文件、外部网格、贴图的模型请保留目录结构打包 ZIP，也可同时选取配套文件。单次输入及解压后均限 160 MB、最多 1024 个文件。资源只从所选文件读取，不下载远程地址。
2. 在「坐标与单位」确认源文件的向上方向、车头方向和 m/cm/mm 单位。URDF 默认 Z 向上、X 向前；glTF 默认 Y 向上、Z 向前，可手动更正。应用新坐标会清除尺寸调整。
3. 点选模型或部件目录中的车轮，将其添加到前左、前右、后左、后右位置。轮胎与轮毂可以归入同一车轮；同一部件不能归入两个车轮。轮距和轴距需先指定四轮，轮轴须沿左右方向。
4. 输入轮距、轴距、轮宽、轮胎半径，单位 mm；留空保留原值，有效范围 1–20000 mm。轮距、轴距以轮组包围盒中心计算；半径根据径向包围盒最大直径缩放，保留网格原有细分和轮廓。变化直接显示在模型中，车架、悬架不会自动重建。
5. 导出调整后的 GLB 和可选尺寸 JSON 记录。工作区保存在本页内存，离开前须导出；刷新或页内导航有离开确认，但不是自动保存。尺寸 JSON 是记录，不是可直接写入 VCU 的配置。

**边界：** 输出 GLB 是零位静态装配，不保留原 URDF 关节、惯量、碰撞与控制定义。URDF 可读取几何基本体及 STL/DAE/GLB/glTF 网格；不接受骨骼、实例或变形动画。整体不可分网格应先在建模软件中拆成独立部件。实机标定值与控制配置不随外观调整改变，原始文件和两套内置模型不会改写。

实现复用 [urdf-loader 0.13.1](https://github.com/gkjohnson/urdf-loaders/tree/master/javascript) 和 [Three.js GLTFExporter](https://threejs.org/docs/pages/GLTFExporter.html)。尺寸逻辑测试位于 `tests/model-tuning.test.mjs`。

## Xacro 导入

- 复用 [xacro-parser 0.3.11](https://github.com/gkjohnson/xacro-parser)，在浏览器中展开为 URDF，再使用同一模型预览和尺寸调节流程。支持 property、macro、默认参数、基本算术/数学表达式、if/unless、arg 和本地 include。
- ZIP 中有多个 Xacro/URDF 文件时，在「Xacro 入口与参数」选择整机主文件，不会随意选择第一个文件。可用 `name=value`（每行一项）覆盖 `xacro:arg`；留空采用文件默认值，点击「展开并载入 Xacro」应用。再次展开会清除当前外观尺寸调整，需确认后继续。
- `$(find package)` 仅查找导入 ZIP 中唯一的同名包目录；支持相对路径、嵌套 include 及 `package://` 引用。请保留包名和子目录结构。缺失资源、错误参数、展开失败均不替换当前模型。
- 浏览器解析不是完整 ROS/Python 执行环境：不运行命令、访问环境变量或系统文件；复杂 Python、YAML 读取、动态宏名、include 命名空间、参数透传、递归宏和重复宏定义应先用 ROS 展开为 URDF。所有兼容范围以报错和测试为准，不宣称任意 Xacro 均可解析。
- 单个 Xacro/XML 限 2 MB，include 最多 128 次，宏展开预算 25000 节点、深度 32，展开后 URDF 限 4 MB。导出仍为静态 GLB，不反写源 Xacro，不保留宏或运动关节。
