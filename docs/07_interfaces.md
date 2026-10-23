# 接口与配置契约

## 配置文件

Unity导出核心文件为 `vehicle_config.json`、网格OBJ/MTL；结构由 `agribot_vehicle_config_tools/schema/vehicle_config.schema.json` 描述。配置包含车辆ID、运动学、尺寸、轮/转向、传感器、机具及算法相关字段，生成器校验单位、有限值和结构后产出URDF/SDF/YAML。

生成目录 `agribot_vehicle_description/generated/<vehicle_id>/` 的 manifest用于追踪源配置与生成文件。不要手改URDF一处而保持JSON及其他参数不变。仿真临时过滤策略有独立运行时副本，不回写物理部署模板。

导出真机包与控制台导入契约见 `agribot_vehicle_config_tools/deployment_bundle.py` 和 `physical_config.py`，控制台入口为 `agribot_mobile_app/physical_config.py`；导入不是执行任意压缩包中的命令。

## 坐标与单位

ROS使用米、秒、弧度；车体FLU，X前Y左Z上。Unity原生坐标经统一转换器变成ROS，不能导出后再人工交换一次轴。Car0阿克曼原点在后轴中心；差速车型原点和轮心由各自配置定义，不能所有车都套用Car0原点。

`map` 为全局地图参考，`odom` 为连续局部里程计参考，`base_link` 为车体参考。ENU是卫星定位局部东-北-天坐标，不等于map；地理配准提供map<-ENU，不能仅凭经纬度数值直接当地图XY。杆臂由传感器测量参考点到车体参考点的刚体变换处理。

## ROS 边界

| 接口 | 典型类型/内容 |
| --- | --- |
| `/initialpose` | PoseWithCovarianceStamped，手动粗初值 |
| `/map` | OccupancyGrid，静态/当前二维地图 |
| `/plan` | Path，全局规划 |
| `/fastlivo_rtk/odometry`、`/fastlivo_rtk/path` | 融合定位与轨迹 |
| `/localization/ready`等 | 就绪状态，名称以激活profile为准 |
| Nav2 NavigateToPose/NavigateThroughPoses | 单目标/连续途经任务 |
| 速度指令/底盘状态 | 由车端门控和协议驱动处理，不从前端直接写CAN帧 |

传感器topic名称、QoS和点时间字段见驱动和launch；不是所有点云都有相同字段。sim使用 `/clock`，真机使用系统/设备映射时间，不能混用use_sim_time导致时间停滞。

## HTTP 网关

基础前缀 `/api/v1`，JSON请求。主要只读GET：`state`、`events`、`maps`、`bags`、`profiles`、`grid`、`semantic/config`。主要POST：

```text
/localization/initial-pose
/navigation/goal
/navigation/route
/navigation/cancel
/navigation/clear-costmaps
/runtime/start
/runtime/stop
/runtime/config/import
/runtime/config/reset
/collection/start
/collection/stop
/semantic/route
/semantic/execute
/semantic/clear
```

真实字段校验以 `gateway_node.py`、`physical_config.py` 和前端调用为准。不要依赖一个成功HTTP响应判断车辆已完成任务，应继续观察ROS Action结果与状态。

## Unity AI 进程接口

Unity通过标准输入给Python后端JSON请求，后端通过标准输出返回JSON，用户文本不拼接为shell命令。`natural_recommendation.py` 生成知识、调用模型并校验结果；只读Function Call为 `list_scenes({})`，无路径参数，无任意文件访问工具。

本地校验方案引用的车辆/机具/场景/算法与版本，应用前再次检查。模型生成JSON格式错误与网络失败不同，有界纠正重试不意味着所有错误都会重试成功。

## CAN / 固件

阿克曼、差速使用不同实际协议；传输层（SocketCAN、USB CDC适配器）与CAN载荷协议分开。不要因为USB适配器名称含CANFD就把所有底盘帧写成CAN FD。HPM6750桥接profile需与两侧速率、ID、字节序、周期和车辆模式一致。

协议、固件版本和导出manifest应配套；本仓库保留原固件项目的协议与作者信息。没有接6750时，原直连控制链路并不因此失效；有6750也不自动证明底盘接管授权成功。
