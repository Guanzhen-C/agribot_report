FROM osrf/ros:humble-desktop-full
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y \
    git git-lfs build-essential cmake ninja-build pkg-config curl rsync zenity \
    python3-colcon-common-extensions python3-rosdep python3-pytest python3-pip \
    python3-venv python3-yaml python3-numpy python3-scipy python3-pil python3-vtk9 \
    python3-opencv python3-serial python3-tk libeigen3-dev libpcl-dev libopencv-dev \
    libboost-all-dev libyaml-cpp-dev libgeographiclib-dev ros-humble-gtsam \
    libfmt-dev libapr1-dev libtbb-dev libusb-1.0-0-dev \
    libglew-dev nlohmann-json3-dev libunwind-dev ros-humble-navigation2 \
    ros-humble-nav2-bringup ros-humble-spatio-temporal-voxel-layer ros-humble-sophus \
    ros-humble-geographic-msgs ros-humble-ackermann-msgs ros-humble-pcl-ros \
    ros-humble-pcl-conversions ros-humble-cv-bridge ros-humble-camera-info-manager \
    ros-humble-image-transport ros-humble-robot-localization \
    ros-humble-rmw-cyclonedds-cpp && rm -rf /var/lib/apt/lists/*
WORKDIR /workspace
CMD ["bash"]
