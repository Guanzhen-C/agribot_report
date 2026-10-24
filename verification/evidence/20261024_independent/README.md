# 独立交付验收附件

## 对象与来源

本目录记录从GitHub新克隆`agribot_report`后的验证，不是原Unity工程桌面快捷方式的截图。测试目录为`agribot_report_independent_20261024`，程序由仓库预编译包安装；ROS的34个包在该目录从空build/install构建。首次构建中途会话中断后继续完成，没有借用原工作区install。

测试电脑为Ubuntu 22.04 x86_64、RTX 3070 Ti、32GB内存。独立目录和环境不等于全新操作系统；不同电脑的性能和显示驱动仍需满足安装说明。

原生启动链路发现并修复了界面增量构建没有排除真机海康SDK驱动的问题。最终版本继续使用相同定位、规划、控制和车辆参数，不借助真值里程计替代定位。

## 附件

| 文件 | 证据范围 |
| --- | --- |
| [source_tests.xml](source_tests.xml) | 120项Python回归，无失败 |
| [unity_editmode.xml](unity_editmode.xml) | 完整169项EditMode，无跳过 |
| [unity_playmode.xml](unity_playmode.xml) | 完整57项PlayMode，无跳过 |
| [default_full_route.json](default_full_route.json) | Car0苹果园、默认FAST-LIVO2+RTK/Smac/MPPI，原23点全部完成 |
| [vision_inference.json](vision_inference.json) | 相同本地权重在真实测试照片上有检测、分割掩膜和姿态输出 |
| [vision_ros_outputs.json](vision_ros_outputs.json) | 实际仿真相机三模式依次输出8/7/7组同时间戳图像和结构化结果，不发布速度指令 |
| [native_car6.png](native_car6.png) | 独立程序中的Car6高精度几何和中文界面 |
| [native_main.png](native_main.png) | 独立程序车库 |
| [native_garage_bottom.png](native_garage_bottom.png) | 车库后续车型，包括Car6至Car9 |
| [native_ai_recipe.png](native_ai_recipe.png) | 使用外部私有Key实际请求得到的可点击装配方案；Key未随仓库发布 |
| [native_ai_applied.png](native_ai_applied.png) | 点击方案后安装传感器/机具、选择场景和算法，未自动发车 |
| [native_gazebo.png](native_gazebo.png) | 真实Unity按钮启动的苹果园、车辆和机具 |
| [native_rviz_goal.png](native_rviz_goal.png) | 同一启动链路的Nav2实际目标成功 |
| [native_started.png](native_started.png) | 原生软件启动后的工具栏 |
| [native_stopped.png](native_stopped.png) | 点击停止后导出按钮恢复 |
| [native_exported.png](native_exported.png) | 原生离线配置导出操作 |
| [export_manifest.json](export_manifest.json) | 实际导出文件清单与哈希；5项文件逐项复核通过，board_verified为false |

完整路线最终误差0.1763m，原生动作成功后观测停稳，原半轴距终点容差0.2633m。约43.9分钟墙钟运行的实时率0.614；不作为所有机器1倍速实时保证。短目标与完整路线是两项不同运行，不能混用各自终点误差。

Car6的完整主机显示几何保留1,867,718个三角面、没有Decimate，含传感器预览共2,108,164面；来源和几何哈希在`unity/Assets/StreamingAssets/LocalFarmExports/compact_tractor/asset_manifest.json`。CAD高精度预览与Gazebo仿真网格分开，未改变碰撞、外参或动力学。

没有上传原始bag、用户园区扫描场景、API凭据、专利软著材料。导入功能保留，独立程序只显示9个内置场景，用户自行导入园区数据。没有连接真实CAN、烧录固件或逐一重跑全部车辆/机具/场景/算法组合；这些不应写成此次通过的项目。
