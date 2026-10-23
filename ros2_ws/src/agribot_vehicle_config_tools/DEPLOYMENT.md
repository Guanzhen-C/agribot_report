# Unity 配置导出边界

本交付只包含Unity配置软件与配套仿真。配置导出功能保留；外部接收应用、真实底盘联调和固件工程不在本仓库范围。

## 当前配置

vehicle_config.json是统一车辆契约。ROS导入生成URDF/SDF/YAML和manifest；仿真临时模型策略不回写物理模板。导出草案不表示真实标定通过。

## 既有兼容接口

原Car0部署包导出接口仍可接收外部已构建固件目录，使用AGRIBOT_HPM_FIRMWARE_ROOT或CLI的--firmware-root指定。它会按现有代码校验该输入，并不自动获取或烧录固件。

没有准备外部固件时，不能宣称已经生成含固件的完整部署包。普通ROS配置、车型草案和Gazebo仿真不依赖固件烧录。

## 新车型草案

离线草案包含当前车辆几何、运动学、传感器位姿、外参和文件清单。它不等于外部系统已支持该底盘，也不执行真实车辆操作。

软件操作说明见仓库docs/04_unity_manual.md，输入校验见本包schema及deployment_bundle.py、physical_config.py。
