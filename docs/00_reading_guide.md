# 文档撰写阅读指南

## 本软件的唯一范围

产品是 Unity 农机配置与算法仿真软件，包括三维车库、车辆/传感器/机具配置、场景与算法选择、AI 助手、Gazebo/RViz 启动停止、配置导出。

农机控制台、手机控车、Android、HTTP 控车网关、6750 固件、真机语义导航服务不属于本软件，不应出现在功能列表、界面章节和部署结构中。

## 阅读顺序

1. 根 README 与资源说明，确认资源和可运行边界。
2. 软件概览、架构与数据流、代码导航。
3. Unity 操作手册和仿真操作，编写实际用户流程。
4. 算法、接口与配置，说明模块边界和数据传递。
5. 验证记录，区分接口可选、静态检查、代表试验和全程运行。

## 建议目录

设计说明：目标与范围、运行环境、总体架构、车库与装备、场景管理、算法库、AI 助手、配置导出、仿真进程管理、接口、异常处理、验证与限制。

用户手册：安装、启动、选车、安装装备、选择场景和算法、启动/停止仿真、RViz 目标和经停点、AI 对话与应用方案、导出配置、排障。

## 主要事实来源

```text
manifests/delivery_scope.json
unity/Assets/AgribotConfig/Resources/farm_vehicle_presets.json
ros2_ws/src/agribot_farm_sim/config/compatibility.json
ros2_ws/src/agribot_farm_sim/config/scenarios.json
ros2_ws/src/agribot_farm_sim/config/manual_validation_policy.json
ros2_ws/src/agribot_farm_sim/config/manual_confirmations*.json
```

当前源码和配置优先于历史 README。包内可能保留共享算法的原说明，不代表本产品提供其中全部真机功能。来源版本、快照和可移植修正见 manifests。

## 不应夸大

- 算法组件组合数量不是独立算法数量，不等于所有组合均已跑完全程。
- 一个代表机具通过后按用户规则确认其他适配机具，不等于逐一实测。
- 场景导入不自动识别场景语义、道路净空或真实 GNSS 质量。
- AI 只接收文字状态和目录，不读取实时相机、点云或 ROS 定位质量。
- 当前仿真机具不参与导航轮廓和碰撞，不能声称已验证真实机具避障或土壤作业。
- 配置导出不等于真机标定、硬件联调或自动烧录。
- 缺少授权模型时不能把不完整车库称为完整独立运行。
- 不编造性能数字、第三方认证、定位精度保证或不存在的页面。

## 可交给网页大模型的任务

> 阅读 README 和 docs，以当前源码为依据编写“Unity 农机配置与算法仿真软件”的设计说明与用户手册。仅描述 Unity 软件和配套 Gazebo/RViz 仿真，不加入农机控制台、Android、HTTP 控车网关和固件系统。引用源码路径，写出配置、场景、算法、AI、启动停止和导出的输入输出与失败处理。保留资源授权和验证范围限制，不把配置可选、人工分组确认或仿真结果扩大为所有组合实测。
