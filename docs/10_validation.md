# 本次交付验证记录

日期：2026-10-23。验证对象为本仓库新建工作区，不是直接引用旧工作区已经编译好的install。

## 已执行

| 检查 | 结果与范围 |
| --- | --- |
| 来源快照 | ROS、Unity stage、HPM6750三套工作树；包含当前源码修改，记录SHA256 |
| 凭据/大文件审计 | 已执行 `tools/audit_delivery.py`，未发现匹配的真实Key/私钥内容和未走LFS的超限文件；不替代专业安全审计 |
| ROS完整仿真profile构建 | 47包完成，跳过需要厂商SDK的海康真实相机驱动；系统ROS/开发库已预装 |
| 关键源码契约回归 | `scripts/test_source.sh`，116项通过 |
| 控制台Python回归 | `agribot_mobile_app/test`，66项通过 |
| GitHub云端源码检查 | [Source Checks](https://github.com/Guanzhen-C/agribot_report/actions/runs/39901911358)通过；只含选定源码测试，不是Gazebo/真机验收 |
| 网页构建 | npm ci + Vite production build通过 |
| 控制台HTTP启动 | 独立loopback端口18888启动，状态JSON与HTML页面均通过；未启动任何车辆任务 |
| Android构建 | Gradle test、lint、assembleDebug通过；使用本机已装JDK/SDK及缓存，不等于全新SDK安装验收 |
| 基础仿真启动 | Car0 orchard，无Gazebo/RViz窗口、无目标；/clock有消息，列出雷达、相机、IMU、融合、地图和Nav2话题 |
| 基础仿真自动行驶全程 | 本次未执行；启动检查不能替代路径/精度验收 |
| 完整Unity车库构建 | 未执行：缺少原项目未公开的授权CAD衍生资源，doctor --unity明确失败 |
| HPM固件重新烧录 | 未执行；无连接板卡和本次目标工具链，源码及原构建说明已归档 |
| 真实车辆运动 | 未执行，本次交付测试没有启用真实CAN |
| GitHub重新克隆 | 独立目录通过HTTPS获取，339个唯一LFS对象全部SHA256验证并检出，Git LFS fsck通过 |
| 重新克隆后的源文件完整性 | 原始9276条快照记录核对，无意外缺失/更改；10个历史APK构建产物按交付规则排除 |
| 重新克隆后的测试与构建 | 116项源码测试、npm ci与网页production build通过；没有借用原工作区install |

重新克隆时普通LFS链路的部分大文件过慢，曾中止并恢复下载；全部对象最终从本公开仓库的GitHub媒体地址获取并核对SHA256，没有用开发机原模型目录补齐来冒充下载成功。克隆验证并非“所有网络一条命令必定成功”的承诺。恢复工具和常规LFS命令均要求可用网络。

首次构建检查曾发现用户目录失效CMake和可选MVS包跳过方式问题，已在交付包装中修正后重跑。基础检查CLI与仿真使用同一CycloneDDS配置，避免CLI默认参与者上限不足造成误报。原工作区代码没有被这些交付修正覆盖。

## 不应写成的结论

本记录不证明全新操作系统已完成所有依赖安装，不证明每种车辆/机具/算法/场景组合都跑过，更不证明现场定位精度、底盘安全或完整Unity车库仅clone即可运行。需补齐授权资源、SDK及实际硬件条件。

源包保留的历史人工确认带车型、场景、算法和版本范围；它们不是本次交付重新执行的记录。任何报告引用应注明代表试验与分组确认的差别。

## 可重复检查

```bash
python3 tools/doctor.py
python3 tools/doctor.py --unity  # 完整资源未准备时应明确失败
python3 tools/audit_delivery.py
bash scripts/test_source.sh
bash scripts/build_mobile.sh web
bash scripts/build_ros.sh simulation --cmake-args -DBUILD_TESTING=OFF
bash scripts/smoke_simulation.sh
```

smoke脚本有界运行仅用于交付检查，不是正常用户任务的运行时限。正式仿真启动无本脚本的检查时限。实际命令输出保存在本机日志目录，Git只保存摘要和源码，不上传用户运行日志和缓存。
