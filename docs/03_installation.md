# 安装与构建

## 支持基线

ROS/Gazebo 工作站：Ubuntu 22.04 amd64，ROS 2 Humble，Gazebo Classic 11，系统 Python 3.10，GCC 11 系列。建议至少 16 GB 内存和充足 SSD，编译 PCL/GTSAM 时控制并发。RDK X5 为 arm64，硬件驱动和 SDK 必须匹配架构，不能复制 PC 的 build/install 目录。

Unity：6000.0.26f1，Linux Build Support (Mono) 和有效许可证。Editor、许可证、Unity Hub 不属于本仓库。网页：Node 20+；Android：JDK17、Android SDK35，Gradle Wrapper 随源码。可选 HPM 上位机另需 Node22.13+，不要与移动网页最低版本混写。

## 克隆和资源

```bash
sudo apt-get update
sudo apt-get install git git-lfs
git clone https://github.com/Guanzhen-C/agribot_report.git
cd agribot_report
git lfs install --local
git lfs pull
/usr/bin/python3 tools/doctor.py
```

不要使用 `GIT_LFS_SKIP_SMUDGE=1` 后直接运行模型。若 LFS 下载报配额/网络错误，修复下载后再运行。完整 Unity 请额外执行 `python3 tools/doctor.py --unity`；授权资源缺失会返回非零。

## ROS 依赖和首次构建

先按 [ROS Humble Ubuntu 安装说明](https://docs.ros.org/en/humble/Installation/Ubuntu-Install-Debs.html) 配置 ROS 软件源并安装 Desktop。仓库脚本不静默替换系统软件源或升级 Ubuntu。

```bash
bash scripts/install_dependencies.sh
bash scripts/build_ros.sh simulation
source scripts/env.sh
```

仿真 profile 跳过海康 SDK 驱动；海康真实相机需先按 `ros2_ws/src/hikrobot_mvs_ros2/scripts/install_mvs_usb_sdk.sh` 的要求准备厂商 SDK，然后使用 hardware profile。不需要真实相机时不要为通过编译安装伪 SDK。

构建脚本清理 Conda 环境污染，显式使用系统 Python/CMake，只加载 `/opt/ros/humble`，不依赖开发机原工程的 install。默认限制包级和编译级并发；资源足够时可用：

```bash
AGRIBOT_BUILD_PACKAGE_WORKERS=2 AGRIBOT_BUILD_JOBS_PER_PACKAGE=2 \
  bash scripts/build_ros.sh simulation
```

首次完整构建可能较久。`--packages-up-to ...` 可用于开发子集，但不代表完整闭环依赖都已准备。Dockerfile 只描述 ROS 依赖环境，不含 Unity Editor、模型许可、GPU/桌面转发和硬件设备权限。

GTSAM使用ROS源提供的 `ros-humble-gtsam`，其中包括GTSAM_UNSTABLE；本次CMake缓存实际选择 `/opt/ros/humble/lib/cmake/GTSAM` 的4.2版本。不要求安装开发机遗留、当前软件源已无候选下载的Ubuntu GTSAM4.0包。实测工具和库版本见 `manifests/tested_environment.json`。

## 基础仿真

```bash
source scripts/env.sh
export ROS_DOMAIN_ID=37 ROS_LOCALHOST_ONLY=1
ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard \
  gui:=false rviz:=true run_waypoints:=false
```

Gazebo 服务仍生成传感器，`gui:=false` 仅不显示 Gazebo 窗口。相机渲染需要可用 GL/显示环境；无物理显示时使用 Xvfb/软件渲染，速度可能明显下降。此处不向真实 CAN 发指令。RViz 经停点操作见 Unity 手册。

## Unity 构建

```bash
python3 tools/doctor.py --unity
export UNITY_EDITOR=/path/to/6000.0.26f1/Editor/Unity
bash scripts/build_unity.sh
bash scripts/run_unity.sh
```

缺授权模型时先补齐资源，不能跳过预检保证黑屏界面也“构建成功”。也可在 Unity Hub 添加本仓库 `unity/`，打开 `Assets/Scenes/scene1.unity`，先等导入/编译完成再进入 Play。运行脚本设置 `AGRIBOT_ROS_SYNC_SCRIPT` 和 `AGRIBOT_WORKSPACE_SRC`，无需将仓库改名为开发者原目录。

输出默认 `dist/unity/`；不会覆盖旧软件桌面快捷方式。AI 凭据只在本机通过 `DEEPSEEK_API_KEY` 或权限600的 `~/.config/agribot/llm.json` 设置，字段为 `api_key`、`base_url`、`model`。无 Key 时仍可手动配置和运行，不伪造在线 AI 结果。

## 网页与 Android

```bash
bash scripts/build_mobile.sh web
bash scripts/run_mobile.sh
```

本机打开 `http://127.0.0.1:8088`。在车端启动后，手机访问车端实际 IP 的8088端口，不固定为历史的125。默认网关配置含 RDK 数据目录，移植电脑需复制 YAML 到私人目录修改 `map_root`、`bag_root`、日志路径和语义服务地址，通过 `params_file:=...` 传入，不修改示例当成所有机器的真实目录。

Android：设置 `ANDROID_HOME` 后 `bash scripts/build_mobile.sh android`；APK 在 Android 模块 `app/build/outputs/apk/debug/`。Debug APK 用于测试，不等于商店发布签名包。项目不包含个人签名密钥。

## 其他外部依赖

视觉 AI 权重、算法专用 Python 环境、扫描场景原始文件、现场地图、Neo4j 数据与语义服务 Key 都单独准备，参见资源说明。禁止把历史绝对路径存在误认为这些数据随 Git 提供。
