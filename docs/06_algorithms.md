# 算法库与参数依据

## 分组而非算法数量宣传

Unity 在统一接口中选择感知、额外视觉AI、定位、规划、控制。组合计数是配置笛卡尔积，不是同等数量的独立算法，亦非已全部跑过全程。

| 类别 | 已有实现/选项 | 作用与边界 |
| --- | --- | --- |
| 障碍感知 | STVL、Voxel 等成本地图输入 | 真实障碍标记/清除；高度带、坐标系和自反射过滤影响结果 |
| 额外视觉AI | 检测、实例分割等任务 | 相机叠加显示；不是运动闭环必选输入，权重需另备 |
| 定位 | FAST-LIVO2+RTK、FAST-LIO2、NavSat ESKF、KISS-ICP 等 | 根据环境和传感器可用性选择；不支持全部场景通吃 |
| 全局规划 | Smac Hybrid-A*、Smac2D/State Lattice、NavFn、Theta*、直线等 | 转向约束与可达性不同，直线不能保证避障 |
| 局部控制 | MPPI、RPP、DWB | 按阿克曼或差速运动学适配，不能忽略转向/速度极限 |

具体当前选项以 `RosSimulationLauncher.cs` 和 ROS `algorithm_matrix.py` 为准；此表是算法族说明，不替代机器可读目录。

## 主要真机定位

FAST-LIVO2 使用IMU预测、激光几何匹配和视觉光度信息产生局部里程计。项目适配包括有界图像缓冲、接收与计算分离、视觉内存管理、局部地图/资源约束和机械雷达点时序处理。不能把这些工程修正都说成原创基础算法。

RTK 固定解位置通过GTSAM因子图提供全局位置约束，连续局部运动主要由FAST-LIVO2提供。失去固定解后不继续把低质量位置当强观测，漂移能否保持受局部特征和惯导误差影响，不是无限时无漂移保证。相机/雷达/RTK杆臂和坐标变换必须使用同一车辆配置。

初始地图对齐使用RTK、视觉或手动粗位姿，再经3D地图两级NDT和GICP。不是用二维PGM做3D配准。运动中的融合修正与每次重跑全局初始配准不同。

## 场景约束

当前适配目录中宽地头场景全部车型限定NavSat；葡萄园Car3/4/6、玉米田Car4等也有NavSat专用约束。稀疏空旷场景激光/视觉特征不足，不能因为某一室内默认配置稳定就忽略该限制。以 `compatibility.json` 的当前字段和场景策略为最终依据。

## 规划与控制

Smac Hybrid-A* 在离散姿态/运动学空间搜索，代价考虑占据栅格和代价场，不等于任意车都可原地转向。大型车需相应半径、轴距和轮廓；四轮差速/履带车可选择对应运动模型。

MPPI 对候选控制序列前向预测并用路径、障碍、角度等critic评价，路径跟踪仍受线速度、角速度、加速度和模型约束影响。RPP和DWB同样需匹配车辆，而非把阿克曼参数直接用于所有差速车。

仿真默认保留失败恢复，但关闭定时全局重规划；初始、更新目标和失败重试仍可规划。最终位置使用半轴距容差，不强制最终航向。此规则限定统一配置仿真入口，不覆盖所有历史launch或真机任务。

## 参数文件

```text
agribot_hardware_bringup/config/fastlivo_hikrobot_mv_cu013.yaml
agribot_hardware_bringup/config/fastlivo_rtk_fusion.yaml
agribot_hardware_bringup/config/pcd_initial_localization.yaml
agribot_hardware_bringup/config/rtk_map_initializer.yaml
agribot_hardware_bringup/ackermann/
agribot_hardware_bringup/differential/
agribot_vehicle_description/config/vehicles/
agribot_vehicle_description/generated/<vehicle_id>/config/
```

不要把说明中的某个数字复制成所有车型默认值。当前激活值由launch参数、车辆生成配置、场景overlay共同决定，历史README可能记录过旧值。调参时记录具体配置SHA和实际ROS参数。

## 离线建图与对比

`agribot_offline_mapping` 提供 LIO-SAM+RTK、地图过滤、地理配准以及不同定位轨迹计算/显示流程；不是所有原始传感器bag已经附带。RTK质量、时间、外参、map<-ENU变换应统一，不能把各自不同起点的曲线直接作精度结论。没有独立真值时，互相重合不等于绝对精度已证明。
