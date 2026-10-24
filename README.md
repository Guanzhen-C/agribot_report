# Unity 农机配置与算法仿真软件

本仓库交付 **Unity 农机配置软件及其配套 ROS 2/Gazebo 仿真**，包含完整车库和运行资源，用于独立构建、运行、阅读源码及编写设计说明和用户手册。

不包含农机控制台、网页/Android 控车应用、控制台 HTTP 网关、6750 独立固件工程或上位机，也不包含独立离线建图/真机语义导航服务器。仅保留现有“导出真机配置”按钮所需的小型离线输入文件，不包含在线烧录功能。

## 完整功能与手册

- [详细功能说明](docs/12_detailed_functions.md)：产品边界、19个功能模块、输入输出、状态和限制。
- [完整用户手册](docs/13_user_manual.md)：从打开软件到选车装配、场景/算法、RViz任务、AI、停止导出的逐步操作。
- [车型、机具、场景与算法附录](docs/14_catalog_reference.md)：从当前配置自动生成的完整目录与候选适配关系。

网页端大模型撰写设计说明和用户手册时，应先读这三份，再按[阅读指南](docs/00_reading_guide.md)核对源码及验证边界。

## 软件流程

车库选车 -> 安装传感器和农机具 -> 选择场景与算法 -> 启动 Gazebo/RViz 仿真 -> 人工指定目标或经停点 -> 停止仿真 -> 导出配置。

AI 助手贯穿本次应用会话，可问答并生成可点击装配/算法方案；应用方案之后仍需用户单独启动仿真。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| [大模型阅读指南](docs/00_reading_guide.md) | 设计说明、用户手册的阅读顺序和范围 |
| [详细功能说明](docs/12_detailed_functions.md) | 完整功能规格及各模块状态、限制和代码依据 |
| [完整用户手册](docs/13_user_manual.md) | 逐步操作、示例、文件位置、常见问题 |
| [配置目录附录](docs/14_catalog_reference.md) | 当前车型/机具/场景/算法与专用规则 |
| [软件概览](docs/01_overview.md) | 产品功能、部署环境、边界 |
| [架构与数据流](docs/02_architecture.md) | Unity、配置生成、ROS 仿真、AI 助手 |
| [安装与构建](docs/03_installation.md) | 克隆、依赖、编译和启动 |
| [Unity 操作手册](docs/04_unity_manual.md) | 选车、装配、场景、算法、AI、导出 |
| [仿真操作](docs/05_simulation.md) | RViz 目标/经停点、运行状态和停止 |
| [算法与参数](docs/06_algorithms.md) | 五组组件和车辆/场景适配 |
| [接口与配置](docs/07_interfaces.md) | JSON、坐标系、文件、ROS 和进程接口 |
| [运行与排障](docs/08_operations.md) | 日志、模型、传感器、导航和 AI |
| [资源与许可证](docs/09_resources_and_licenses.md) | 授权模型和外部依赖 |
| [验证记录](docs/10_validation.md) | 本次验证结果与未覆盖范围 |
| [代码导航](docs/11_code_map.md) | 功能与源码路径对应 |

## 克隆和基础仿真

目标环境：Ubuntu 22.04、ROS 2 Humble、Gazebo Classic 11。先安装 ROS Humble Desktop，再运行：

```bash
sudo apt-get install git-lfs
git clone https://github.com/Guanzhen-C/agribot_report.git
cd agribot_report
git lfs install --local
git lfs pull
python3 tools/doctor.py
bash scripts/install_dependencies.sh
bash scripts/build_ros.sh simulation
source scripts/env.sh
export ROS_DOMAIN_ID=37 ROS_LOCALHOST_ONLY=1
ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard \
  gui:=false rviz:=true run_waypoints:=false
```

这是仿真，不连接真实底盘。车辆默认等待 RViz 目标。Unity 构建、网络下载恢复和详细前提见安装文档。

## 目录与范围

```text
unity/                  Unity 工程、界面、车库、装备、AI 和导出
ros2_ws/src/            配套仿真与所需算法、共享库/驱动依赖
scripts/                构建、启动和仿真检查
tools/                  快照、范围、完整性与模型下载工具
manifests/              交付范围、来源版本、文件哈希
docs/                   本软件的设计与使用说明
docker/                 ROS 依赖环境
```

部分定位、融合、NavSat、配置导出代码复用原工程的共享包，因此保留了 `agribot_hardware_bringup` 等依赖。**包名中有 hardware 不表示本仓库包含农机控制台，也不表示仿真会连接真车。** 构建入口只选择统一仿真需要的依赖。机器可读范围见 `manifests/delivery_scope.json`。

## 完整资源与私人输入

模型提供者已确认分发权限，完整10车型、57个装配预设、9个内置场景和3份视觉权重已纳入交付。运行 `python3 tools/doctor.py --unity --hashes` 检查实际文件和SHA-256；只下载LFS指针不算完成。Car6包含当前高精度显示模型，车辆、传感器、农具和算法参数与来源工作区保持一致。

用户手工导入的“自动化所园区”不上传；导入、删除、转换缓存和AI场景查询功能保留，新机器从9个内置场景开始。用户可在软件内手动导入自己的LCC2场景。AI服务需要使用者配置自己的Key；源码构建需要安装并激活Unity Editor。运行资源和许可证说明见[资源目录](resources/README.md)。不包含专利/软著申请材料、私人凭据、原始实验bag。第三方代码和模型保持各自许可证。

独立复现的安装、验收范围和验证依据见[复现说明](docs/15_reproducibility.md)。支持的平台与当前软件一致，为Ubuntu 22.04 x86_64；不宣称可在任意操作系统上原生运行ROS/Gazebo。

本次范围调整采用普通 Git 删除提交，未重写已有仓库历史。
