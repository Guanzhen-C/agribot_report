# 执行机构与机器人描述

作者：刘泽禹。2026-09-09。

## 不必逐个关联零件

`/preview` 默认只列执行机构候选部件。标准装配从 93 个外观节点收敛到 7 个，扩展装配从 76 个收敛到 5 个；这是候选模型节点数，不表示它们全部是独立可控的执行器。

- 行走、转向、灯光和作业机构可选择对应的 FGMP 功能；不指定模型对应也不影响原协议配置。
- 雷达、相机、算力等转入 `/robot`「算力与感知」，不进入 VCU 功能下拉框。
- 支架、紧固件和外壳默认只作结构外观。供电与安全独立分类，原有电池反馈和安全配置不删除。
- 初始分类来自装配名称，不是实机识别；可在「算力与感知」切换查看对象、选择部件并修改用途。
- 「全部外观」仍可查看所有部件。旧绑定保留，不因分类而自动删除。

## 生成与保存

在「算力与感知」可直接生成 URDF 包。需要修改的部件才打开「安装与运动关系」；不必逐个填写。生成与保存描述仅在本机运行，不写入 VCU，不发送执行器命令。

ZIP 包含 ROS 2 描述包目录、`urdf/robot.urdf`、实际 CAD 导出的二进制 STL、`robot-description.json`、`part-links.json`、`package.xml` 和 CMake 安装文件。描述包名可改，不等同于产品品牌。刘泽禹署名保留。STL 保留尺寸与零位装配，但不保留原材质颜色。

两套初始装配已生成示例包：

- `C:/hpm_analysis/fieldgate-standard-robot-v1.zip`：27 links、26 joints。
- `C:/hpm_analysis/fieldgate-extended-robot-v1.zip`：29 links、28 joints。

初始包的关节全部是固定安装，适合检查整机装配，不是可直接控制或仿真的运动模型。本地脚本可复现，输出必须选择尚不存在的路径：

```powershell
cd host_app
node scripts/robot-export.mjs ackermann C:/hpm_analysis/standard-description-v2.zip
node scripts/robot-export.mjs ackermann-flagship C:/hpm_analysis/extended-description-v2.zip
```

### 数据独立，部件标识互通

| 数据 | 用途 | 对应关系 |
|---|---|---|
| FGMP | 上游三合一协议、执行器映射、安全与标定 | `extensions['fieldgate:models'][modelId][partId] → deviceId` |
| FGROBOT/1 | 部件用途、安装父子关系、关节和传感器坐标 | `modelId + partId → joint/frame` |
| part-links.json | 导出模型与控制配置之间的查找表 | `parts[partId].link → URDF link` |

算力侧可以按同一 modelId/partId 查到 URDF link 与原 FGMP deviceId。分类和 URDF 编辑不重写 FGMP，不改变原协议 ID，也不自动生成 ROS 控制驱动。

FGROBOT 草稿分别保存在 `fieldgate.robot-descriptions.v1.<modelId>`，每个模型一份、最大 128 KiB，独立于 FGMP 草稿库。不同标定版本可通过 JSON 备份保存。保存失败不会显示成功；损坏存储不自动覆盖，可导入有效备份恢复。未保存导航有确认；生成包时锁定模型切换和应用导航。当前支持导入 **FGROBOT JSON**，尚未实现任意现有 URDF/Xacro 的反向导入。

## 坐标与运动边界

原 GLB 已是米制、Z 向上。导出重新加载原始 GLB，忽略网页为了观感设置的归一化、相机和爆炸位移。base_link 暂取模型包围盒底面中心；默认 CAD 前向 -Y，需要按实物核验。网页坐标字段用 mm/°，文件用 m/rad。

坐标约定为右手系、X 前/Y 左/Z 上；相机 optical 的转换为 X 右/Y 下/Z 前。这遵循 [ROS REP-103](https://raw.githubusercontent.com/ros-infrastructure/rep/master/rep-0103.rst)。关节采用 URDF 的 fixed、continuous、revolute 和 prismatic 类型，参见 [ROS 官方关节示例](https://raw.githubusercontent.com/ros/urdf_tutorial/ros2/urdf/06-flexible.urdf)。

编辑的 xyz/rpy 表示相对整机的连杆坐标；导出计算 `inverse(parentWorld) × childWorld` 得到父子关节变换，网格使用 `inverse(linkWorld) × CADtoRobot × meshWorld`。更改原点或父级不会错误移动零位外观。安装坐标已有人工覆盖后锁定 CAD 前向，避免静默改变坐标基准。

- 未配置的执行机构和感知部件保留独立 fixed link；无独立坐标设置的结构/供电部件合入 base_link。明确编辑过的结构部件可以保留独立节点。
- 初始关节原点来自外观包围盒中心，不是已知轮轴、转向轴或相机光心。
- 非固定关节必须填写有效运动轴、正的力/力矩及速度限值；有限位关节还要求 lower < upper，并人工确认。默认轴为零，不能直接导出运动关节。
- 相机 optical 需核对安装坐标后显式开启；修改参数会取消确认。不会替代外参标定。
- 源 CAD 少量节点只有线框，保留坐标节点而不伪造实体；清单 `linksWithoutSolidMesh` 明确列出。所有输出 mesh 引用必须实际存在。
- 不虚构质量、惯量、碰撞体、控制器、闭环连杆约束或阿克曼运动学。要用于动力学仿真、导航或实机控制，还需标定、建模与相应系统接入。

## 验证与剩余边界

本轮 `npm test` 的构建与 29 项测试通过，类型检查、静态检查通过。两套真实模型的全部实体三角面、尺寸、非零姿态/原点的零位还原和 ZIP 网格引用已自动校验。

实际浏览器完成执行机构筛选、关联保存、感知页面独立编辑、相机坐标确认、运动关节缺参拒绝、生成 ZIP 下载、刷新保留、分类修改、未保存导航保护和原 FGMP 存储不变检查。默认两包及浏览器导出的相机/运动测试包均通过独立 `urdf_parser_py` 的树结构与网格引用检查；后两者是合成参数测试，不是实机标定文件。

七页在 320/375/414/768/1280/1440/1920 CSS px 检查布局，覆盖键盘焦点、减少动态效果与 200% 字号。模拟串口回归不涉及真实硬件。未在 ROS 2/RViz 或动力学仿真中运行，未烧录或验证实机。仍有既有依赖安全告警、大模型加载和低性能平板性能风险，见 [三维配置文档](VISUAL_CONFIGURATION.md)。CAD 始终本地处理，分发前需确认授权；自动生成的包联系邮箱是显式占位，应在分发前替换。
