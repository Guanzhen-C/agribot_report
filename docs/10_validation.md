# 验证记录

## 本次范围

验证对象仅为Unity配置软件和配套ROS/Gazebo仿真。已移除控制台、Android、HTTP网关、6750独立产品和不相关工具；`resources/deployment/`中的小型离线输入仅服务现有配置导出按钮。以前针对外部产品的测试不计入本软件验证结果。

## 已有验证依据

- 交付副本此前从空build完成统一仿真依赖构建，未借用原工作区install。
- Car0 orchard无GUI/RViz、无目标启动，收到/clock并列出相机、雷达、IMU、融合、地图和Nav2话题；不是全程精度验收。
- 远端HTTPS克隆及339个唯一LFS对象曾完整校验；大文件通过官方media恢复，未从原开发目录复制模型冒充下载。
- 此前交付确实缺少部分CAD衍生资源；用户确认授权后，现已补齐完整资源，见下面的最新检查。旧的缺资源状态不代表当前文件树。
- 没有启动真实CAN，没有做真实硬件/固件联调。

## 完整资源补齐后的复验

| 检查 | 结果 |
| --- | --- |
| 完整资产 | `doctor --unity --hashes`通过；10车型、57装配、9内置场景、3个视觉权重 |
| 仿真依赖构建 | 从GitHub独立克隆到新目录，从空build/install构建34个仿真依赖包；没有借用原工作区install |
| 源码回归 | Python 120项通过，包含高精度显示格式、真实Git版本指纹和界面增量构建范围检查 |
| Unity源码构建 | 从本交付首次导入Library并构建Linux程序成功；不是仅复制原开发程序 |
| Unity EditMode | 最新完整169项通过、无跳过，包含固定Prefab复用及中文字体显式载入 |
| Unity PlayMode | 最新完整57项全部通过、无跳过；覆盖当前共用车库、传感器/机具安装卸载、导出、场景/算法界面、AI会话及导入/删除交互 |
| 视觉真实推理 | 本仓库独立环境对相同权重完成检测、分割、姿态三模式非空推理 |
| ROS相机视觉输出 | 新克隆中，实际640×512仿真相机分别产生8/7/7组检测、分割、姿态的时间戳匹配输出，画面非空，三节点均无速度指令输出；不代表语义识别准确率 |
| 扫描场景渲染器 | 只用仓库自带源代码编译通过，不使用开发者的3dgs工程；园区数据未发布 |
| GitHub克隆 | 完整源码与LFS在新目录从GitHub实际取得，`git lfs fsck`通过；不是复制原开发目录或模型缓存冒充克隆 |
| 独立构建与运行 | 新克隆完成34包构建；完整23点默认路线通过，最终误差0.1763m、原生成功并停稳，无异常退出。程序包远端下载、安装及原生车库/Car6/连续AI验证通过 |
| Unity实际启动链路 | 修复增量构建误依赖真机海康SDK驱动后，原生AI方案卡片→应用装配→启动Gazebo/RViz→定位与Nav2激活→停止→导出全部通过；停止后无遗留仿真进程 |
| 离线导出 | 原生按钮实际生成JSON、部署清单、说明和原有BIN/ELF；导出文件哈希通过，不依赖原工程目录。没有烧录或实板测试 |

测试平台为Ubuntu 22.04 x86_64、RTX 3070 Ti、32GB内存。独立克隆在同一物理电脑的新目录验收，ROS构建、应用数据、视觉环境和导入场景目录均独立；不是全新操作系统或所有硬件平台的测试。原Unity源工作区和已验证算法参数未因交付修改而改变。交付测试使用当前共用车库的关闭按钮、正式导出入口及米制模型配置，不再查找早期场景独占的轮心节点或隐藏的修理面板关闭按钮。

完整23点验收实际使用FAST-LIVO2+RTK、Smac Hybrid-A*和MPPI，全部23点按顺序经过，最后原生返回成功并停稳。最终水平位置误差为0.1763m，低于原半轴距容差0.2633m。约43.9分钟墙钟运行中观测到实时率0.614；这是功能验收，不是1倍速或所有电脑实时性能保证。真值里程计只用于观察误差，没有替代融合定位。

这一轮没有重跑所有车型、机具、场景和算法的全部排列组合；保留来源项目的适配记录和边界，不将默认方案全程通过扩大解释为所有组合全程通过。证据与逐项验收范围见[独立验收附件](../verification/evidence/20261024_independent/README.md)。

## 可重复命令

```bash
python3 tools/doctor.py
python3 tools/doctor.py --unity
python3 tools/verify_snapshot.py
python3 tools/audit_delivery.py
bash scripts/test_source.sh
bash scripts/test_unity.sh EditMode
bash scripts/test_unity.sh PlayMode
bash scripts/validate_vision.sh
bash scripts/build_ros.sh simulation --cmake-args -DBUILD_TESTING=OFF
bash scripts/smoke_simulation.sh
```

smoke脚本的有界时限仅用于交付检查，不是用户仿真的运行时限。源包中的人工确认仍受车型、场景、算法、版本和代表机具范围约束。

相机的实际ROS输出检查见[观察器说明](../verification/README.md)。完整导航验收使用安装后的`verify_algorithm_runtime.py --case stvl_off_fastlivo_rtk_smac_hybrid_mppi --scope full-route --motion-timeout 3600`，保留苹果园原始23点，不用真值位姿替代融合定位。
