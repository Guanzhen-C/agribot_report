# 验证记录

## 本次范围

验证对象仅为Unity配置软件和配套ROS/Gazebo仿真。已移除控制台、Android、HTTP网关、6750独立产品和不相关工具；`resources/deployment/`中的小型离线输入仅服务现有配置导出按钮。以前针对外部产品的测试不计入本软件验证结果。

## 已有验证依据

- 交付副本此前从空build完成统一仿真依赖构建，未借用原工作区install。
- Car0 orchard无GUI/RViz、无目标启动，收到/clock并列出相机、雷达、IMU、融合、地图和Nav2话题；不是全程精度验收。
- 远端HTTPS克隆及339个唯一LFS对象曾完整校验；大文件通过官方media恢复，未从原开发目录复制模型冒充下载。
- 此前交付确实缺少部分CAD衍生资源；用户确认授权后，现已补齐完整资源，见下面的最新检查。旧的缺资源状态不代表当前文件树。
- 没有启动真实CAN，没有做真实硬件/固件联调。

## 完整资源补齐后的复验（进行中）

| 检查 | 结果 |
| --- | --- |
| 完整资产 | `doctor --unity --hashes`通过；10车型、57装配、9内置场景、3个视觉权重 |
| 仿真依赖构建 | 当前仿真需要的34个包构建完成；本轮复用了本交付已有编译缓存，尚不等于新克隆干净构建 |
| 源码回归 | Python 119项通过，包含高精度显示格式与真实Git版本指纹检查 |
| Unity源码构建 | 从本交付首次导入Library并构建Linux程序成功；不是仅复制原开发程序 |
| Unity EditMode | 完整167项通过，结果位于本机`.validation/unity_editmode.xml` |
| Unity PlayMode | 首次完整运行57项，42项通过、15项失败；正在修复旧UI/导出接口测试并重跑，尚未验收通过 |
| 视觉真实推理 | 本仓库独立环境对相同权重完成检测、分割、姿态三模式推理；不是ROS整车图像输出验收 |
| 扫描场景渲染器 | 只用仓库自带源代码编译通过，不使用开发者的3dgs工程；园区数据未发布 |
| 远端与完整运行 | 当前版本LFS推送、新克隆、预编译安装及完整导航运行仍待最终验证 |

这些结果不能替代远端新克隆与全程自动行驶验收。原Unity源工作区和已验证算法参数未因交付修改而改变。交付测试使用当前共用车库的关闭按钮、正式导出入口及米制模型配置，不再查找早期场景独占的轮心节点或隐藏的修理面板关闭按钮。

## 可重复命令

```bash
python3 tools/doctor.py
python3 tools/doctor.py --unity
python3 tools/verify_snapshot.py
python3 tools/audit_delivery.py
bash scripts/test_source.sh
bash scripts/test_unity.sh EditMode
bash scripts/test_unity.sh PlayMode
bash scripts/build_ros.sh simulation --cmake-args -DBUILD_TESTING=OFF
bash scripts/smoke_simulation.sh
```

smoke脚本的有界时限仅用于交付检查，不是用户仿真的运行时限。源包中的人工确认仍受车型、场景、算法、版本和代表机具范围约束。
