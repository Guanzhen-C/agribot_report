# 安装与构建

## 环境

Ubuntu 22.04 amd64、ROS 2 Humble、Gazebo Classic 11、系统 Python 3.10。建议32GB内存和充足SSD。源码构建使用Unity6000.0.26f1、Linux Build Support (Mono)和有效许可证；安装匹配的预编译程序不需要Unity Editor。

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

完整车库执行 `python3 tools/doctor.py --unity --hashes`；当前已包含全部授权运行资源，但仍应校验LFS下载和文件指纹，不要把LFS指针当成模型。

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

GTSAM使用ROS源的 ros-humble-gtsam，包含GTSAM_UNSTABLE。Dockerfile仅提供ROS依赖环境，不含Unity许可证或桌面/GPU转发；模型随Git LFS工作区提供。

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

普通使用者在取得完整Git LFS工作区后执行`bash scripts/bootstrap.sh`，会构建ROS并优先安装仓库中与源码匹配的预编译程序；然后运行`bash scripts/run_unity.sh`。分阶段安装可用`bash scripts/install_app.sh`，但仍需先安装ROS运行依赖和构建配套仿真。

激活使用者自己的Unity许可证后：

```bash
python3 tools/doctor.py --unity --hashes
export UNITY_EDITOR=/path/to/6000.0.26f1/Editor/Unity
bash scripts/build_unity.sh
bash scripts/run_unity.sh
```

发布式构建要求运行源码与资源已提交，构建前后指纹一致。开发中的修改可直接在Unity Editor测试与构建，但不能把没有匹配构建记录的旧程序发布成新版。

输出为dist/unity，不覆盖开发者原软件的桌面入口。Editor开发建议先source scripts/env.sh，再从同一终端启动Editor，使用-projectPath指向本仓库unity目录；没有环境变量时也会从工程位置查找同一交付仓库，不再回退访问开发者的~/agribot_ws。

## AI

运行 `python3 tools/configure_llm.py` 输入自己的Key，默认写入权限600且被Git忽略的.local/llm.json。也可以显式设置DEEPSEEK_API_KEY或AGRIBOT_LLM_CONFIG。当前接口为https://api.deepseek.com、模型deepseek-flash。未配置时仍能手动选车、装配和仿真，不伪造AI结果。

额外视觉AI的相同权重已附在resources/vision_models，运行 `bash scripts/setup_vision.sh` 安装独立环境。默认CPU；有兼容NVIDIA显卡时可用 `AGRIBOT_VISION_TORCH_BACKEND=cu121 bash scripts/setup_vision.sh`。视觉推理不改变定位/规划/控制的输入接口；不启动额外视觉时，相机仍可供FAST-LIVO2使用。

完整安装入口为 `bash scripts/bootstrap.sh`。用户扫描场景、仿真状态和应用数据默认保存在本仓库.local目录，自动化所园区数据不随仓库提供，使用者可在“导入场景”中自行添加。
