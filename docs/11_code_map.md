# 代码导航

## Unity

| 路径 | 内容 |
| --- | --- |
| unity/Assets/AgribotConfig/Runtime/ | 车辆契约、装配、场景、AI、导出和启动 |
| Runtime/RosSimulationLauncher.cs | 五组算法选项和ROS启动接口 |
| Runtime/AssistantConversation.cs | 连续会话与操作上下文 |
| Editor/ | 资源生成、预览校验与Linux构建 |
| Tests/ | Editor/PlayMode回归 |
| Resources/farm_vehicle_presets.json | 车型、机具和导出预设 |
| unity/Assets/Scenes/ | 车库和单车界面 |
| unity/Packages/ | Unity包依赖 |

除完整unity前缀的行外，Runtime/Editor等均相对unity/Assets/AgribotConfig。

## 配套ROS

| 包 | 职责 |
| --- | --- |
| agribot_vehicle_config_tools | JSON校验、坐标转换、URDF/SDF/YAML和导出 |
| agribot_vehicle_description | 车辆配置、统一仿真launch、Gazebo及控制插件 |
| agribot_farm_sim | 场景、机具适配、人工确认、AI知识和校验 |
| agribot_k2_sim | 扫描场景导入、目录、缓存和传感器检查 |
| agribot_manipulator_sim | 可选机械臂演示 |
| agribot_visual_perception | 额外视觉AI任务和叠加 |
| agribot_ackermann_mppi、agribot_autonomy | 仿真导航集成与公共组件 |
| agribot_hardware_bringup | 仿真复用的融合定位、NavSat及共享库 |
| agribot_time_sync | 共享时间相关消息和工具 |
| scout_*、gazebo_ros_pkgs、velodyne_simulator | Gazebo/RViz/模型与导航依赖 |
| FAST-LIVO2、FAST_LIO_ROS2、kiss-icp、rpg_vikit | 定位算法与库依赖 |

共享包中的原真机实现不表示本产品包含真实设备控制页面。构建入口仅选择统一仿真需要的包；完整范围见manifests/delivery_scope.json。

## 关键入口

```text
ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh
ros2_ws/src/agribot_vehicle_description/launch/configured_ackermann_sim.launch.py
ros2_ws/src/agribot_farm_sim/config/assistant_system_prompt.txt
ros2_ws/src/agribot_farm_sim/config/compatibility.json
ros2_ws/src/agribot_farm_sim/config/scenarios.json
ros2_ws/src/agribot_farm_sim/agribot_farm_sim/natural_recommendation.py
ros2_ws/src/agribot_farm_sim/agribot_farm_sim/assistant_scenes.py
ros2_ws/src/agribot_k2_sim/agribot_k2_sim/import_scene.py
ros2_ws/src/FAST-LIVO2/src/LIVMapper.cpp
```

## 可选HIL入口

| 路径 | 责任 |
| --- | --- |
| `unity/Assets/Scripts/LastPanel.Hil.cs`、`HilSimulationUi.cs` | 原面板入口、模式选择、提交与状态 |
| `unity/Assets/AgribotConfig/Runtime/HilSimulationLauncher.cs` | 方案匹配和短时CLI接口 |
| `unity/Assets/AgribotConfig/Resources/hil_profiles.json` | 5个HIL方案与3组固件唯一目录 |
| `tools/hil/cli.py`、`workflow.py`、`backend.py` | 用户请求、必烧录状态机、物理启动与清理 |
| `tools/hil/deploy.py`、`prepare_runtime.py`、`protect_real_runtime.py` | 独立部署及真车文件保护 |
| `tools/hil/agent.py`、`ros_control.py` | 专用运行单元、就绪检查和授权 |
| `resources/hil/` | 固定ELF与可重定位场景资源 |
| `ros2_ws/src/agribot_hil_apple` | 苹果园、原底盘CAN、机械臂及ISOBUS农具 |
| `ros2_ws/src/agribot_hil_hitch` | 独立悬挂协议、动力换挡/CVT和作业可视化 |

验证和产品边界见[HIL集成说明](18_hil_integration.md)，此模块不包含手机/网页农机控制台。
