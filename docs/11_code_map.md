# 代码导航

以下路径均相对仓库根目录，便于网页模型定位设计依据。

## Unity

| 位置 | 说明 |
| --- | --- |
| `unity/Assets/AgribotConfig/Runtime/` | 配置契约、导出、车库适配、算法/场景/助手及启动器 |
| `unity/Assets/AgribotConfig/Runtime/RosSimulationLauncher.cs` | 五组算法选项、兼容性与ROS进程入口 |
| `unity/Assets/AgribotConfig/Editor/` | 编辑器资源生成、构建、模型/预览校验 |
| `unity/Assets/AgribotConfig/Tests/` | Editor与PlayMode测试，不等于完整Gazebo验收 |
| `unity/Assets/AgribotConfig/Resources/farm_vehicle_presets.json` | 车型/机具导出预设与历史验证标识 |
| `unity/Assets/Scenes/` | 原有场景与单车配置界面 |
| `unity/Packages/manifest.json`、`packages-lock.json` | Unity包依赖 |

## ROS核心

| 包 | 主要职责 |
| --- | --- |
| `agribot_vehicle_config_tools` | JSON校验、FLU转换、URDF/SDF/导航配置和真机包生成 |
| `agribot_vehicle_description` | 车型配置、生成资源、统一仿真launch、Gazebo/控制器插件 |
| `agribot_farm_sim` | 场景、机具适配、人工确认、AI知识与推荐校验 |
| `agribot_k2_sim` | 扫描场景准备/导入、目录/缓存和传感器验证 |
| `agribot_hardware_bringup` | 真机统一启动、车辆profiles、传感器/底盘/融合/初始定位 |
| `agribot_mobile_app` | HTTP网关、React网页、Android封装、任务/配置管理 |
| `agribot_time_sync` | 时间同步诊断和相关消息/工具 |
| `agribot_offline_mapping` | 离线建图、地理配准、轨迹对比和语义地图处理 |
| `agribot_semantic_navigation` | 语义导航/成本约束相关组件 |
| `agribot_visual_perception` | 额外视觉AI任务和叠加图 |
| `agribot_autonomy` | 导航集成和公共行为 |
| `scout_viz` | RViz图像/轨迹/面板等本地适配 |

其他ackermann/differential示例包、manipulator、rl_nav及第三方驱动保留在源码树，不能因为已纳入仓库就宣称当前默认任务全部调用。

## 关键文件

```text
ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh
ros2_ws/src/agribot_vehicle_description/launch/configured_ackermann_sim.launch.py
ros2_ws/src/agribot_vehicle_description/launch/configured_ackermann_physical.launch.py
ros2_ws/src/agribot_farm_sim/config/assistant_system_prompt.txt
ros2_ws/src/agribot_farm_sim/agribot_farm_sim/natural_recommendation.py
ros2_ws/src/agribot_farm_sim/agribot_farm_sim/assistant_scenes.py
ros2_ws/src/agribot_farm_sim/config/compatibility.json
ros2_ws/src/agribot_farm_sim/config/scenarios.json
ros2_ws/src/agribot_k2_sim/agribot_k2_sim/import_scene.py
ros2_ws/src/agribot_mobile_app/agribot_mobile_app/gateway_node.py
ros2_ws/src/agribot_mobile_app/agribot_mobile_app/processes.py
ros2_ws/src/agribot_mobile_app/agribot_mobile_app/profiles.py
ros2_ws/src/agribot_mobile_app/agribot_mobile_app/physical_config.py
ros2_ws/src/agribot_mobile_app/web/src/
ros2_ws/src/agribot_hardware_bringup/launch/vehicle_autonomy.launch.py
ros2_ws/src/agribot_hardware_bringup/launch/include/fastlivo_rtk_localization.launch.py
ros2_ws/src/FAST-LIVO2/src/LIVMapper.cpp
```

## 固件

`firmware/hpm6750evk2/` 是独立上游项目的源码快照。先读其README及协议说明，再看firmware、profiles、scripts和host_app。它的上位机不是当前ROS控制台的替代品。保留协议名称、字节序和作者说明，不能为统一UI而改变实际CAN载荷。
