# 离线运行资源

完整车库资源位于 `unity/Assets/` 及 `ros2_ws/src/agribot_vehicle_description/generated/`，使用 Git LFS。模型提供者已明确确认分发权限；不包含购买压缩包、原始STEP工程或用户扫描的自动化所园区。

本目录补齐原应用运行时所需的另外两类输入：

- `vision_models/`：当前已验证视觉模块使用的三份相同权重，分别用于目标检测、实例分割和姿态估计。上游为 [Ultralytics](https://github.com/ultralytics/ultralytics)，保留该版本的 AGPL-3.0 许可证；不把第三方模型声明为本项目原创。权重已随仓库提供，不要求第一次推理临时下载。
- `deployment/ackermann/`：Car0“导出真机配置”使用的原有离线 BIN、ELF 与SHA-256清单。仅用于复制到人工部署包，不执行烧录、不运行真实CAN、不附带6750独立上位机或农机控制台。板端源基线和文件指纹保留在原清单中；对应源项目为 [hpm6750evk2](https://github.com/LiuZeyu1119/hpm6750evk2)，HPM SDK二进制所需许可见 `deployment/HPM_SDK_LICENSE`。

部署清单中的 `board_verified: false` 保持原值。包含这些文件不等于实板验证或允许直接启动车辆；真实硬件仍需要独立的标定、安全和台架验收。

Git不包含AI服务密钥。通过 `python3 tools/configure_llm.py` 设置自己的密钥；私人配置写入被忽略的 `.local/llm.json`。
