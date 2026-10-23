# 三维装配与可视化配置

作者：刘泽禹。FieldGate VCU 是控制器产品，不是执行器品牌。

## 使用入口

本机启动 `host_app` 后，打开 `http://localhost:3000/launchpad`。桌面包含总览、车辆预览、算力与感知、设备、诊断和设置，底部程序坞在各页保留。`/preview` 默认只关注执行机构；`/robot` 独立管理安装关系并生成 URDF。见 [执行机构与机器人描述](URDF_EXECUTOR_WORKFLOW.md)。

1. 在车辆预览中切换标准装配或扩展装配，拖动旋转，双指缩放/平移。
2. 点“爆炸展开”，滑动展开程度；支持俯视、侧视、复位、单独查看。部件目录提供搜索和键盘选择。
3. 点选执行机构，在车辆配置中可选关联功能、改显示名称；不需要关联每个外观零件。雷达、相机在「算力与感知」管理。“整机参数”可修改农机名称及模板允许的标定参数。
4. 新建农机可选择阿克曼四灯、差速作业或原三合一模板。原模板及作者信息保留；四灯目前跟随原协议的同一个照明开关，不虚构四个独立上游信号。
5. 保存仅写本浏览器草稿。配置文件与管理中可导入 JSON/FGMP、导出描述、生成配置包、删除并撤销删除。导入创建新 UUID，不覆盖原文件。

所有新建、编辑和导入配置保持 `commissioned=false`。配置包不是固件镜像；此页不写板、不发运动指令。现有设置页的选择写入协议不变，仍须真实 ACK。完整板端上传、读回、列表、删除、激活及持久化接口尚未接入。

## 数据范围

`profiles/fgmp-v1.schema.json` 与 `docs/FGMP_MACHINE_DESCRIPTION.md` 定义通用描述。两套 CAD 绑定存在描述的命名空间扩展中，随 JSON/FGMP 保留，不影响执行表：

```json
{
  "extensions": {
    "fieldgate:models": {
      "ackermann-independent-v1": {"part-0001": "light_1"},
      "ackermann-flagship-v1": {"part-0001": "light_2"}
    }
  }
}
```

两个模型的同名 part ID 独立解释；总关联数最多 64。模型重新划分部件必须使用新 model ID 或提供明确迁移，不能把旧 ID 静默指向另一部件。兼容读取旧的 `fieldgate:model` 单模型扩展。

草稿键 `fieldgate.machine-drafts.v1`，最多 12 份、总 JSON 最多 256 KiB；单配置执行包最多 16 KiB。入库前执行结构、语义、大小及绑定校验。FGMP 导入校验 CRC 并重新编译核对执行表。损坏存储不自动覆盖，写入失败不显示保存成功；未保存时导航和关闭页面有提示。本地存储不是跨浏览器/跨电脑备份，请导出重要配置。

## 原模型与转换

原文件在工作区相邻目录 `顶配独立悬挂阿克曼小车`，未改写，也未上传。这里保留准确源路径用于溯源；前端资源只保留中性部件名称与稳定标识。

| 来源文件 | SHA-256 | 网页资源 | 可选装配部件 | 三角面 |
|---|---|---|---:|---:|
| 顶配独立悬挂阿克曼（三维模型）.stp | FC3B0EDF2E02A4A5D9F2D0173D205615D4985D21B6918708D52D3345796D74AE | ackermann.glb，18,184,492 B | 93 | 755488 |
| 顶配阿克曼旗舰独立悬挂（3D模型客户图）.stp | E4927515A16CBD4E6B39525ACB48CA53CD9E9637D1B0DF7C27D1F662196980BB | ackermann-flagship.glb，25,049,816 B | 76 | 968989 |

部件数为可选装配节点，不等于原 CAD 全部零件总数。OCCT 导出报告标准/扩展装配分别有 1/9 个空几何节点跳过；这些是缺少独立网格的标签，不代表同名装配的子网格全部缺失。两套模型的车轮等实物网格已实际渲染检查，未凭空补画缺失实体。

使用隔离环境 `C:/hpm_analysis/cad-venv`，`cadquery-ocp==8.0.1.0.0`、Python 3.14，依赖经本机代理安装。原 STEP 标签为 GB 编码，读取必须设置 `read.step.codepage=GB`。复现时选择新的输出路径：

```powershell
& C:/hpm_analysis/cad-venv/Scripts/python.exe tools/convert-step.py '<原STEP路径>' 'C:/hpm_analysis/new-native.glb'
node tools/prepare-model.mjs C:/hpm_analysis/new-native.glb C:/hpm_analysis/new-public.glb new-model-v1
```

原生转换保留 XCAF 装配实例与材质，线偏差 0.5、角偏差 0.35。打包器去除原部件/材料/模型名称，生成 `.parts.json`；原始标签溯源文件保存在原生 GLB 旁，不进入网页目录。DWG 未转换。

Three.js 0.180.0 加载本地 GLB，只在交互或尺寸变化时绘制，不自动旋转；路由离开释放画布、几何和材质。三维不可用时显示重试，已加载的部件目录和草稿编辑仍可用。首次加载约 18/25 MB，真实低性能平板的 GPU/内存测试仍需补做。

网页只做展示坐标修正（X 轴 -90°）及归一化。**爆炸图是沿装配位置的空间展开，不是维修拆卸工序，也不保证全部零件互不遮挡。CAD 外形不用于推定轮距、轴距或控制标定，配置不会移动实机。**

技术依据：[OCCT GLB 导出](https://occt3d.com/dev/doc/refman/html/class_r_w_gltf___caf_writer.html)、[STEP 读取设置](https://github.com/Open-Cascade-SAS/OCCT/blob/master/src/DataExchange/TKDESTEP/STEPControl/STEPControl_Controller.cxx)、[Three.js GLTFLoader](https://threejs.org/docs/pages/GLTFLoader.html)、[OrbitControls](https://threejs.org/docs/pages/OrbitControls.html)。

## 三维配置基础验证与边界（2026-09-09）

下列为基础三维配置阶段的记录；后续执行机构分层与 URDF 的 29 项测试、七页检查及独立 ROS 解析结果见 [增量验证记录](URDF_EXECUTOR_WORKFLOW.md#验证与剩余边界)。

- 构建及 23 项 JS 自动测试通过；类型检查、静态检查通过。
- 三组主机 C 测试通过；同一 C 可执行程序加载原透传、阿克曼四灯及差速作业配置，覆盖超时、健康反馈、中立重启、损坏包与计时回绕。
- HPM SDK 1.12.1 交叉编译通过；新 FGMP C 核纳入构建，但没有接入主循环，没有烧录。
- Headless Edge 验证实际网格点选、展开、独显、相机、两模型隔离绑定、草稿新增/保存/重载/删除/撤销、配置包导出和再次导入、CRC 错误拒绝、存储失败、未保存导航保护。
- 六页在 320、375、414、768、1280、1440、1920 CSS px 检查根页面及工作区无横向溢出、按钮无多行标签；1280×800、200% 字号、减少动态效果、焦点与计算对比度检查通过。
- 模拟串口回归通过：预览零串口请求/写入、原 ACK 保存、导航锁、拒绝 ACK、错误 CRC、超时、拔出、取消连接。均非实机测试。
- 构建仍提示三维模块分块较大；按路由加载，尚未做低面数 LOD。
- `npm audit --omit=dev` 经代理返回 4 个 high 依赖项：既有 next、其 postcss/sharp，以及 nanoid。没有执行跨版本自动修复；应在对外部署前做独立依赖升级与回归。本次保持本地运行，不发布 CAD。
