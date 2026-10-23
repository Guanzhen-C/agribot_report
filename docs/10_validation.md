# 验证记录

## 本次范围

验证对象仅为Unity配置软件和配套ROS/Gazebo仿真。已移除控制台、Android、HTTP网关、6750固件和不相关的独立工具；以前针对它们的测试不计入本软件验证结果。

## 已有验证依据

- 交付副本此前从空build完成统一仿真依赖构建，未借用原工作区install。
- Car0 orchard无GUI/RViz、无目标启动，收到/clock并列出相机、雷达、IMU、融合、地图和Nav2话题；不是全程精度验收。
- 远端HTTPS克隆及339个唯一LFS对象曾完整校验；大文件通过官方media恢复，未从原开发目录复制模型冒充下载。
- 完整十车型Unity构建仍缺授权CAD衍生资源，doctor --unity应失败，不以空模型跳过。
- 没有启动真实CAN，没有做真实硬件/固件联调。

## 本轮范围调整后复验

| 检查 | 结果 |
| --- | --- |
| 仿真依赖构建 | 移出旧install后，仅构建当前仿真需要的34个包，全部完成；复用了本交付已有编译缓存 |
| 源码回归 | 去除控制台测试后的109项通过 |
| 交付完整性 | 按新范围核对6289条来源记录，无意外缺失或更改 |
| 基础预检 | doctor通过，完整Unity资源检查仍因授权资源缺失而受限 |
| 基础启动 | Car0 orchard启动并收到/clock，传感器/融合/地图/Nav2话题存在；检查后正常退出 |

以上是范围调整后的构建、契约和启动检查，不是重新完成全程自动行驶，也不是完整Unity车库构建。原Unity应用、源工作区和已验证算法参数未改变。

## 可重复命令

```bash
python3 tools/doctor.py
python3 tools/doctor.py --unity
python3 tools/verify_snapshot.py
python3 tools/audit_delivery.py
bash scripts/test_source.sh
bash scripts/build_ros.sh simulation --cmake-args -DBUILD_TESTING=OFF
bash scripts/smoke_simulation.sh
```

smoke脚本的有界时限仅用于交付检查，不是用户仿真的运行时限。源包中的人工确认仍受车型、场景、算法、版本和代表机具范围约束。
