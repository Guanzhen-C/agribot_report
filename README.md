# 农机配置、仿真与控制软件源码交付

本仓库整理当前使用的 Unity 农机配置软件、ROS 2 算法与仿真、农机控制台网页/Android 源码，以及可选的 HPM6750 CAN 桥接固件。整理日期：2026-10-23。它是当前工作树的交付快照，不是三个原仓库 Git 历史的拼接，也不是只包含界面截图的报告仓库。

## 先读这个限制

**代码齐备不等于所有外部资源已经获准公开。** 部分购买的农机 CAD 衍生资源在原项目中即被忽略，重分发授权未确认，本仓库不擅自上传。因此：

- 可以从源码构建 ROS 模块、网页控制台，并使用已包含资源的 Car0 基础仿真。
- **完整十车型 Unity 车库目前需要补齐授权模型资源，不能宣称仅 clone 后即可完整构建。** `python3 tools/doctor.py --unity` 会明确报告缺项，不用空模型假装成功。
- 海康 MVS SDK、Unity Editor/许可证、Android SDK、AI 凭据、真实传感器、现场地图和实验 bag 不随源码自动获得。
- 真机启动前必须完成本机设备、外参、地图和急停检查。不要把本仓库中记录的历史实验尺寸直接套到任意车辆。

详见 [资源与许可证](docs/09_resources_and_licenses.md) 和 [验证记录](docs/10_validation.md)。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| [大模型阅读指南](docs/00_reading_guide.md) | 撰写设计说明和用户手册的阅读顺序、证据和禁写事项 |
| [系统概览](docs/01_overview.md) | 两种应用、部署位置、功能与边界 |
| [架构与数据流](docs/02_architecture.md) | Unity、ROS、控制台、固件及服务器之间的关系 |
| [安装与构建](docs/03_installation.md) | 从新电脑克隆、依赖、编译和环境变量 |
| [Unity 操作手册](docs/04_unity_manual.md) | 选车、装配、算法、场景、仿真、配置导出、AI 助手 |
| [控制台操作手册](docs/05_console_manual.md) | 手机/网页、定位、导航、采集、配置导入 |
| [算法与参数](docs/06_algorithms.md) | 感知、定位、规划、控制及配置的真实来源 |
| [接口与配置契约](docs/07_interfaces.md) | 文件、坐标系、HTTP、ROS 和 CAN 边界 |
| [部署与排障](docs/08_operations.md) | 安全步骤、日志、故障定位和数据管理 |
| [资源与许可证](docs/09_resources_and_licenses.md) | 未随仓库提供的内容、第三方来源和准备方式 |
| [验证记录](docs/10_validation.md) | 本次交付实际执行了什么，尚未覆盖什么 |
| [代码导航](docs/11_code_map.md) | 主要类、脚本和功能的对应位置 |

`docs/legacy/` 和各包原有 README 保留历史上下文；与本交付文档冲突时，以当前代码、配置和明确日期的验证记录为准。

## 最小启动路线

目标平台是 Ubuntu 22.04、ROS 2 Humble、Gazebo Classic 11。先安装 ROS Humble Desktop，然后：

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
export ROS_DOMAIN_ID=37
export ROS_LOCALHOST_ONLY=1
ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard \
  rviz:=true gui:=false run_waypoints:=false
```

此入口只启动仿真，不连接真车 CAN；车辆等待 RViz 目标。若机器无图形桌面，不能把 RViz 不显示误认为 ROS 没有数据。Unity 完整构建另见安装文档，不在缺资源时跳过校验。

## 目录

```text
unity/                         Unity 6000.0.26f1 工程源码
ros2_ws/src/                   ROS 包、第三方算法及其本地适配
firmware/hpm6750evk2/          HPM6750 固件和原配套上位机源码
calibration/                   纳入源码管理的标定记录/配置
scripts/                       本交付仓库的可移植构建与启动入口
tools/                         快照、完整性、审计工具
manifests/                     原始版本、文件哈希、依赖清单
docs/                          中文设计与使用说明
docker/                        ROS 依赖环境构建文件
```

模型采用 Git LFS。不要只下载 GitHub 单个文本文件就认定模型缺失，也不要把 LFS 指针当成可加载的网格。

## 发布约束

本仓库未包含私人密钥、真实 AI API Key、登录凭据、现场原始 bag、专利申请材料和软件著作权申请材料。第三方代码和模型保留各自授权，不对整个混合仓库套用一个宽松许可证。源码公开不自动授予第三方 CAD/商用 SDK 的重分发权。
