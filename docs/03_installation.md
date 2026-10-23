# 安装与构建

## 环境

Ubuntu 22.04 amd64、ROS 2 Humble、Gazebo Classic 11、系统 Python 3.10。建议至少16GB内存和充足SSD。Unity使用6000.0.26f1、Linux Build Support (Mono)和有效许可证。

本仓库不需要 Node、Android SDK、手机控制台或6750固件工具链。

## 克隆

```bash
sudo apt-get install git git-lfs
git clone https://github.com/Guanzhen-C/agribot_report.git
cd agribot_report
git lfs install --local
git lfs pull
python3 tools/doctor.py
```

完整车库额外执行 python3 tools/doctor.py --unity；授权资源未补齐时应明确失败。不要把LFS指针当成模型。

普通Git下载正常但LFS大文件长期缓慢时，可先用 GIT_LFS_SKIP_SMUDGE=1 完成新克隆，再执行 python3 tools/fetch_lfs_media.py。恢复工具从GitHub官方media地址下载，逐个核对当前提交的LFS SHA256，再执行checkout；不提供未授权资源，也不保证任意网络均可下载。

## ROS

先按[官方安装说明](https://docs.ros.org/en/humble/Installation/Ubuntu-Install-Debs.html)安装ROS Humble Desktop：

```bash
bash scripts/install_dependencies.sh
bash scripts/build_ros.sh simulation
source scripts/env.sh
```

构建入口只选统一仿真、农场场景、扫描场景和视觉模块及其依赖，跳过真实海康SDK驱动，不启动真实传感器或底盘。共享包保留不代表需要连接实车。

脚本排除Conda污染，使用系统Python/CMake并限制并发。可用 AGRIBOT_BUILD_PACKAGE_WORKERS 和 AGRIBOT_BUILD_JOBS_PER_PACKAGE 调整编译资源。

GTSAM使用ROS源的 ros-humble-gtsam，包含GTSAM_UNSTABLE。Dockerfile仅提供ROS依赖环境，不含Unity许可证、桌面/GPU转发或授权模型。

## 基础仿真

```bash
source scripts/env.sh
export ROS_DOMAIN_ID=37 ROS_LOCALHOST_ONLY=1
ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard \
  gui:=false rviz:=true run_waypoints:=false
```

gui=false只关闭Gazebo窗口，服务端和虚拟传感器仍运行。相机需要可用GL/显示环境；软件渲染可能变慢。车辆等待RViz目标，不默认发送预设路线。

## Unity

先补齐授权模型和激活Unity许可证：

```bash
python3 tools/doctor.py --unity
export UNITY_EDITOR=/path/to/6000.0.26f1/Editor/Unity
bash scripts/build_unity.sh
bash scripts/run_unity.sh
```

输出为dist/unity，不覆盖旧软件桌面入口。Editor开发也应先source scripts/env.sh，再从同一终端启动Editor，使用-projectPath指向本仓库unity目录；直接从未加载环境的Hub打开，可能找不到交付副本的ROS目录。

## AI

可选设置DEEPSEEK_API_KEY，或权限600的 ~/.config/agribot/llm.json，字段为api_key、base_url、model。当前代码默认接口https://api.deepseek.com、模型deepseek-flash。未配置时仍能手动选车、装配和仿真，不伪造AI结果。

额外视觉AI的独立Python环境和权重参见 agribot_visual_perception/scripts/setup_ai_runtime.sh；不启动额外视觉时不必准备这些权重，相机仍可供FAST-LIVO2使用。
