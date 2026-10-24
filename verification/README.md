# 实际运行验收

这里的脚本只用于观察验收，不属于程序包的运行输入，不改变算法或车辆参数。

## 仿真相机的三种视觉输出

先按安装文档构建ROS并运行`bash scripts/setup_vision.sh`，在一个终端启动默认仿真。另一个终端使用同一个ROS域：

```bash
source scripts/env.sh
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI="file://$AGRIBOT_WORKSPACE_SRC/agribot_vehicle_description/config/cyclonedds_simulation.xml"
python3 verification/ros_visual_outputs.py --domain 37 --output .validation/vision_camera_01
```

观察器按顺序启动实际的检测、实例分割和姿态节点，订阅仿真相机，要求每种模式至少产生3组时间戳匹配的图像与结构化结果，并检查非空画面、处理状态和不发布速度指令。节点逐个退出，日志和结果写入指定的新目录。

没有识别到物体是合法结果，不能把空旷场景的零目标数判为算法未运行；也不能把有图像输出当成语义识别精度认证。权重的非空检测、分割掩膜和关键点另由`bash scripts/validate_vision.sh`对真实测试照片验证。

该脚本不发送导航目标或底盘指令。完整行驶验收使用原有`verify_algorithm_runtime.py`，不得用这个视觉检查代替完整路线。
