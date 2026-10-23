# 资源与许可证

本仓库只含Unity软件和配套仿真依赖。第三方代码、场景、模型保持各自LICENSE/NOTICE和版权头，不用一个宽松许可证覆盖整个混合仓库。

## 外部资源

- 部分购买CAD衍生模型：原工程即忽略；提供方已确认有分发权限，但当前暂缓补传，资源尚未齐备。
- Unity6000.0.26f1与有效许可证：使用者自行安装/激活。
- 额外视觉AI权重与专用Python环境：按模块要求和权重许可准备。
- 用户LCC2/K2扫描原始文件：由用户提供，不作为本仓库实验数据。
- AI凭据：本机环境变量或私人配置，不编入应用和Git。

海康真实SDK驱动属于共享源码依赖，但仿真构建跳过它，不要求连接真实相机。没有Android/控制台/固件环境准备步骤。

## 完整车库缺项

```text
unity/Assets/AgribotConfig/Resources/LocalFarmAssets/
unity/Assets/AgribotConfig/Resources/LocalGarageThumbnails/
unity/Assets/AgribotConfig/Resources/LocalGarageVehicles/
unity/Assets/AgribotConfig/Resources/LocalVehicleAppearances/
unity/Assets/StreamingAssets/LocalFarmExports/
unity/Assets/StreamingAssets/LocalVehicleAppearances/
```

完整资源包需包含.meta、预览、导出、指纹和相应ROS模型。仅放入任意FBX不能通过一致性检查。取得有权提供的资源后放到对应路径，执行doctor --unity再构建。

另补齐的16个vehicle_config.json只供配置和AI目录读取，不表示相应网格已公开。模型准备脚本需合法原始CAD和工具环境，不是自动恢复购买资产的方法。

## 来源

Unity来自boyana12cow-team/AgriculturalCar_Interior的stage工作树；ROS仿真及依赖来自Guanzhen-C/agribot_humble当前工作树。版本、当前修改和文件哈希记录在manifests，范围由delivery_scope.json限定。

场景来源见各资产目录的source.json和许可证。不要把所有场景、模型和底层算法称为自行原创。

未收录密钥、私人密码、专利/软著申请材料和原始实验bag。Git当前主分支删除范围外内容不等于重写既有历史。
