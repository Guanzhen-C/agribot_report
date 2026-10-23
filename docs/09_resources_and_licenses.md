# 资源、依赖与许可证

## 代码与资产的区别

本仓库汇总当前软件源代码，不自动拥有全部第三方资源版权。保留源目录中的LICENSE、NOTICE、版权头和资源来源文件；没有一个覆盖全仓库所有内容的统一MIT授权。使用者应逐组件检查授权，特别是模型和商用SDK。

## 未自动公开的资源

| 内容 | 原因/准备方式 |
| --- | --- |
| 部分购买的农机CAD及衍生网格 | 原项目明确忽略且重分发授权未确认，需权利人提供授权资源包 |
| Unity Editor/许可证 | 安装6000.0.26f1并自行激活，不能打包他人许可证 |
| 海康MVS SDK | 按厂商许可和CPU架构安装，仿真可跳过此驱动 |
| 视觉AI模型权重 | 根据模块准备并遵守权重许可证；不默认下载未知来源文件 |
| 现场地图/原始bag/扫描高斯文件 | 实验数据与代码分离；使用者提供自己的数据 |
| AI Key、签名密钥、账号 | 私人配置/环境变量提供，不进Git |
| 语义服务器知识库/Neo4j数据 | 按部署脚本建立并导入自有地图，不依赖开发机在线服务 |
| HPM SDK/工具链 | 固件README所指定版本及构建脚本准备，不是烧录器或板卡本身 |

## 完整 Unity 车库资源布局

以下路径在原Unity工程中本来就被忽略，本交付未擅自解除：

```text
unity/Assets/AgribotConfig/Resources/LocalFarmAssets/
unity/Assets/AgribotConfig/Resources/LocalGarageThumbnails/
unity/Assets/AgribotConfig/Resources/LocalGarageVehicles/
unity/Assets/AgribotConfig/Resources/LocalVehicleAppearances/
unity/Assets/StreamingAssets/LocalFarmExports/
unity/Assets/StreamingAssets/LocalVehicleAppearances/
```

完整资源包还需包含目录.meta、预览、导出文件、指纹与对应ROS生成配置。仅随便放一份FBX进去不能通过资源一致性校验。获得有权提供的资源包后在根目录解压到对应相对路径，执行 `tools/doctor.py --unity`，再构建；仍缺项时停止，不将缺模型当作成功。

仓库另补齐16个不含网格的 `vehicle_config.json` 契约供AI目录和配置逻辑读取，来源哈希见 `manifests/additional_configuration_metadata.json`。存在JSON不等于该车型的仿真模型资源已齐备，也不解除完整Unity资源检查。

资产准备脚本在 `agribot_farm_sim/scripts/`，需要原始CAD和对应FreeCAD/网格工具环境。它们不是绕过许可或从Git自动恢复购买文件的工具。基础Car0 ROS仿真不要求完整十车型车库资源。

## 第三方来源

- Unity原工程：`boyana12cow-team/AgriculturalCar_Interior`，本快照来自stage工作树。
- ROS原工程：`Guanzhen-C/agribot_humble` 当前工作树；包含FAST-LIVO2、FAST-LIO2、LIO-SAM、KF-GINS、KISS-ICP、ROS/Gazebo相关包及适配。
- HPM6750：`LiuZeyu1119/hpm6750evk2`，保留FieldGate VCU及原作者标注。
- Gazebo农场场景：各资产目录的source.json、LICENSE和上游说明；不能把所有场景宣称为自行建模。

准确版本和文件内容哈希见 `manifests/source_snapshot.json`。源码历史未整体导入，本仓库不能替代上游完整贡献记录。

## 交付与隐私

不发布专利/软件著作权申请材料和聊天历史，不公开私人连接密码。示例中保留的历史设备IP或用户名是待覆盖配置，不是当前服务器可用性承诺。私人密钥应使用本机文件权限或环境变量保存，不能为方便网页大模型阅读而上传。
