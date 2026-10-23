# 接口与配置

## 车辆契约

Unity导出vehicle_config.json和OBJ/MTL。JSON结构由 agribot_vehicle_config_tools/schema/vehicle_config.schema.json 描述，包含ID、运动学、尺寸、轮/转向、传感器、机具等字段。

生成器校验单位、有限值、结构和资源，输出URDF/SDF/YAML及manifest。不要只手改一个生成文件。运行时临时模型策略与源配置分开。

## 坐标

ROS使用米、秒、弧度；车体FLU，X前Y左Z上。Unity原生坐标由统一转换器处理，导出后不能再人工交换一次坐标轴。

Car0参考点为后轴中点；差速车型由其配置定义，不能把Car0原点强加给所有车。map、odom、base_link及传感器外参应组成一致的TF链。

## ROS

| 接口 | 用途 |
| --- | --- |
| /clock | Gazebo仿真时间 |
| /camera/rgb/image_raw | 虚拟相机 |
| /lidar/points、/imu/data | 激光和IMU输入 |
| /fastlivo_rtk/odometry、/fastlivo_rtk/path | 所选融合定位输出 |
| /map、/plan | 二维地图和全局路径 |
| NavigateToPose、NavigateThroughPoses | 单目标与连续经停任务 |
| 速度命令/关节状态 | 控制Gazebo模型并显示轮/转向状态 |

名称、QoS及具体输出按当前launch与所选算法确认。sim时间与系统时间不可混用，否则会出现超时或时间停滞。

## Unity进程接口

RosSimulationLauncher调用unity_sync_and_simulate.sh，传入导出目录、场景和统一算法选项。环境变量AGRIBOT_ROS_WORKSPACE、AGRIBOT_ROS_SYNC_SCRIPT、AGRIBOT_WORKSPACE_SRC用于可移植定位，不依赖开发者固定用户名。

启动脚本校验/导入/编译后运行统一launch，停止仅针对自己管理的任务。默认不发送预设路线。

## AI接口

Unity通过stdin向Python传JSON请求，stdout返回结构化结果，不把用户文字拼成shell命令。后端校验方案中的车型、机具、场景、算法和版本。

只读Function Call为list_scenes({})，不接受任意路径，没有运动/文件执行工具。应用前重新核对目录和配置，旧方案不能绕过删除或版本变化。

## 配置导出边界

导出JSON/ROS配置是本软件功能。外部应用接收、真实CAN通信和固件烧录不属于本软件。现有含固件部署包接口保留兼容，但所需固件是外部输入，不随本仓库交付。
