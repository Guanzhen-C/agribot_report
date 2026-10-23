#!/usr/bin/env bash
set -euo pipefail
if [[ "$(. /etc/os-release; echo "$VERSION_ID")" != 22.04 ]]; then
  echo '本安装清单仅面向 Ubuntu 22.04。' >&2; exit 2
fi
if [[ ! -f /opt/ros/humble/setup.bash ]]; then
  echo '请先按 ROS 官方文档安装 ROS 2 Humble Desktop，再运行本脚本。' >&2; exit 2
fi
sudo apt-get update
sudo apt-get install -y \
  git git-lfs build-essential cmake ninja-build pkg-config curl rsync zenity \
  python3-colcon-common-extensions python3-rosdep python3-pytest \
  python3-pip python3-venv python3-yaml python3-numpy python3-scipy \
  python3-pil python3-vtk9 python3-opencv python3-serial python3-tk \
  libeigen3-dev libpcl-dev libopencv-dev libboost-all-dev libyaml-cpp-dev \
  libgeographiclib-dev ros-humble-gtsam libfmt-dev \
  libapr1-dev libtbb-dev libusb-1.0-0-dev libglew-dev nlohmann-json3-dev \
  libunwind-dev gazebo libgazebo-dev \
  ros-humble-navigation2 ros-humble-nav2-bringup ros-humble-gazebo-ros-pkgs \
  ros-humble-spatio-temporal-voxel-layer ros-humble-sophus \
  ros-humble-geographic-msgs ros-humble-ackermann-msgs ros-humble-pcl-ros \
  ros-humble-pcl-conversions ros-humble-cv-bridge ros-humble-image-transport \
  ros-humble-camera-info-manager ros-humble-robot-localization \
  ros-humble-xacro ros-humble-joint-state-publisher ros-humble-rmw-cyclonedds-cpp
echo '基础依赖完成。海康 MVS SDK、Unity 许可证、Android SDK 和可选模型另行准备。'
