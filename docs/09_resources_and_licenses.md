# 资源与许可证

本仓库只含Unity软件和配套仿真依赖。第三方代码、场景、模型保持各自LICENSE/NOTICE和版权头，不用一个宽松许可证覆盖整个混合仓库。

## 外部资源

- CAD衍生模型：提供方已确认分发权限；本交付版本已包含完整运行资源，使用Git LFS，不再依赖开发者私有模型目录。
- Unity6000.0.26f1与有效许可证：使用者自行安装/激活。
- 额外视觉AI权重已提供，许可证见resources/vision_models；Python运行环境由scripts/setup_vision.sh在本仓库内创建。
- 用户LCC2/K2扫描原始文件：由用户提供，不作为本仓库实验数据。
- AI凭据：本机环境变量或私人配置，不编入应用和Git。

海康真实SDK驱动属于共享源码依赖，但仿真构建跳过它，不要求连接真实相机。没有Android/控制台/固件环境准备步骤。

## 完整车库目录（已包含）

```text
unity/Assets/AgribotConfig/Resources/LocalFarmAssets/
unity/Assets/AgribotConfig/Resources/LocalGarageThumbnails/
unity/Assets/AgribotConfig/Resources/LocalGarageVehicles/
unity/Assets/AgribotConfig/Resources/LocalVehicleAppearances/
unity/Assets/StreamingAssets/LocalFarmExports/
unity/Assets/StreamingAssets/LocalVehicleAppearances/
```

目录包含.meta、预览、导出、指纹和相应ROS模型。执行git lfs pull及doctor --unity --hashes核验实际资源；仅有LFS指针或任意替代FBX都不能算完整交付。

当前57个预设均包含完整显示与导出资源和对应仿真SDF。原始购买压缩包和STEP源工程不作为运行前提；代码仓库附带的已生成资源足以重建当前软件。开发者若要重新加工其他CAD，仍需自行提供合法来源。

Car0部署包导出所需的既有离线文件位于resources/deployment，仅供人工导出，不包含6750上位机、农机控制台或在线烧录功能。清单中的board_verified状态不改变；提供文件不等于实板验证。

## 来源

Unity来自boyana12cow-team/AgriculturalCar_Interior的stage工作树；ROS仿真及依赖来自Guanzhen-C/agribot_humble当前工作树。版本、当前修改和文件哈希记录在manifests，范围由delivery_scope.json限定。

场景来源见各资产目录的source.json和许可证。不要把所有场景、模型和底层算法称为自行原创。

未收录密钥、私人密码、专利/软著申请材料和原始实验bag。Git当前主分支删除范围外内容不等于重写既有历史。
