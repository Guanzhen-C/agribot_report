# 车型、装备、场景与算法配置附录

本文件由 `tools/generate_function_catalog.py` 从当前配置生成，避免手写目录与软件脱节。

**这是配置目录和候选适配关系，不是所有组合的全程验收报告。** 机具名称不表示已经模拟真实耕作、播种、喷洒、切割或采摘。

## 1. 车型与可挂载机具

| 编号 | 名称 | 运动学 | 配置ID | 机具目录（不含裸车） |
| --- | --- | --- | --- | --- |
| Car0 | 阿克曼小车 | 阿克曼 | `ackermann_current` | 六轴机械臂与夹爪 |
| Car1 | 大型驾驶室拖拉机 | 阿克曼 | `unity_car1` | 悬挂式中耕机、凿式犁、圆盘耙、六行开沟机、悬挂式林地粉碎机、四行铣土机、大蒜播种机、灌水覆膜播种机 |
| Car2 | 宽轮驾驶室拖拉机 | 阿克曼 | `unity_car2` | 悬挂式中耕机、凿式犁、圆盘耙、六行开沟机、悬挂式林地粉碎机、四行铣土机、大蒜播种机、灌水覆膜播种机 |
| Car3 | 履带小车 | 差速/履带 | `unity_car3` | 六轴机械臂与夹爪 |
| Car4 | 通用轮式拖拉机 | 阿克曼 | `unity_car4` | 凿式犁、圆盘耙、六行开沟机、悬挂式中耕机、悬挂式林地粉碎机、四行铣土机、大蒜播种机、灌水覆膜播种机 |
| Car5 | 紧凑型开式拖拉机 | 阿克曼 | `unity_car5` | 凿式犁、圆盘耙、六行开沟机、悬挂式中耕机、悬挂式林地粉碎机、四行铣土机、大蒜播种机、灌水覆膜播种机 |
| Car6 | 紧凑型轮式拖拉机 | 阿克曼 | `compact_tractor` | 凿式犁、圆盘耙、大蒜播种机、灌水覆膜播种机、六行开沟机、悬挂式中耕机 |
| Car7 | 带驾驶室轮式拖拉机 | 阿克曼 | `cab_tractor` | 六行开沟机、悬挂式中耕机、悬挂式林地粉碎机、四行铣土机、双轴农用拖车、凿式犁、圆盘耙、大蒜播种机、灌水覆膜播种机 |
| Car8 | 骑乘式割草机 | 阿克曼 | `ride_on_mower` | 前置割草刀杆 |
| Car9 | 履带式联合收割机主机 | 差速/履带 | `tracked_harvester` | 谷物收割割台 |

## 2. 内置默认场景

用户导入场景为本机动态内容，不编造进本附录的固定清单。

| ID | 名称 | 目录任务标签 |
| --- | --- | --- |
| wide_headland | 宽地头机具运输区 | 大型机具运输、宽地头转弯 |
| orchard | 苹果园 | 巡检、喷洒、机械臂演示 |
| arable_field | 玉米试验田 | 巡检、行间除草、喷洒 |
| greenhouse | 温室行间 | 巡检、行间除草、喷洒 |
| farmyard | 农场运输区 | 物料运输、巡检 |
| tomato_greenhouse | 番茄温室 | 作物巡检、视觉识别 |
| strawberry_field | 草莓种植区 | 行间巡检、果实识别、地头转弯 |
| vineyard | 葡萄园 | 行间巡检、植株观察、地头转弯 |
| vegetable_field | 蔬菜试验田 | 分区巡检、作物观察、地头转弯 |

任务标签只说明场景目录用途，不证明对应物理作业已实现。

## 3. 五组算法组件

| 类别 | 当前组件ID |
| --- | --- |
| 几何感知 | `stvl`、`voxel` |
| 额外视觉感知 | `off`、`object_detection`、`instance_segmentation`、`pose_estimation` |
| 定位 | `fastlivo_rtk`、`fast_lio`、`navsat`、`kiss_icp` |
| 规划 | `smac_hybrid`、`navfn`、`theta_star`、`smac_2d`、`direct` |
| 控制 | `mppi`、`rpp`、`dwb` |

## 4. 挂载预设及候选场景组

候选组不是已通过白名单。AI还会检查排除规则、配置版本和方案条件。JSON存在不代表完整模型已经随仓库提供。

| 车型 | 挂载 | 导出预设 | 候选场景（非验收） | 仓库SDF |
| --- | --- | --- | --- | --- |
| Car0 | 裸车 | ackermann_current | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car0 | 六轴机械臂与夹爪 | ackermann_current | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 裸车 | unity_car1 | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 悬挂式中耕机 | car1_cultivator | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 裸车 | unity_car2 | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 悬挂式中耕机 | car2_cultivator | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car3 | 裸车 | unity_car3 | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car3 | 六轴机械臂与夹爪 | unity_car3 | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 裸车 | unity_car4 | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 凿式犁 | car4_chisel | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 圆盘耙 | car4_harrow | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 裸车 | unity_car5 | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 凿式犁 | car5_chisel | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 圆盘耙 | car5_harrow | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car6 | 裸车 | compact_tractor | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car6 | 凿式犁 | tractor_chisel | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car6 | 圆盘耙 | tractor_harrow | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car6 | 大蒜播种机 | tractor_garlic | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car6 | 灌水覆膜播种机 | tractor_mulch | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 裸车 | cab_tractor | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 六行开沟机 | cab_groove | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 悬挂式中耕机 | cab_cultivator | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 悬挂式林地粉碎机 | cab_crusher | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 四行铣土机 | cab_interrow | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 双轴农用拖车 | cab_trailer | 玉米试验田、宽地头机具运输区 | 需补资源 |
| Car8 | 裸车 | ride_on_mower | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car8 | 前置割草刀杆 | mower_front_cutter | 苹果园、玉米试验田、温室行间、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car9 | 裸车 | tracked_harvester | 玉米试验田、宽地头机具运输区 | 需补资源 |
| Car9 | 谷物收割割台 | harvester_header | 玉米试验田、宽地头机具运输区 | 需补资源 |
| Car1 | 凿式犁 | car1_chisel | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 圆盘耙 | car1_harrow | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 六行开沟机 | car1_groove | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 悬挂式林地粉碎机 | car1_crusher | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 四行铣土机 | car1_interrow | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 大蒜播种机 | car1_garlic | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car1 | 灌水覆膜播种机 | car1_mulch | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 凿式犁 | car2_chisel | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 圆盘耙 | car2_harrow | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 六行开沟机 | car2_groove | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 悬挂式林地粉碎机 | car2_crusher | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 四行铣土机 | car2_interrow | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 大蒜播种机 | car2_garlic | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car2 | 灌水覆膜播种机 | car2_mulch | 玉米试验田、农场运输区、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 六行开沟机 | car4_groove | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 悬挂式中耕机 | car4_cultivator | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 悬挂式林地粉碎机 | car4_crusher | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 四行铣土机 | car4_interrow | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 大蒜播种机 | car4_garlic | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car4 | 灌水覆膜播种机 | car4_mulch | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 六行开沟机 | car5_groove | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 悬挂式中耕机 | car5_cultivator | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 悬挂式林地粉碎机 | car5_crusher | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 四行铣土机 | car5_interrow | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 大蒜播种机 | car5_garlic | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car5 | 灌水覆膜播种机 | car5_mulch | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car6 | 六行开沟机 | tractor_groove | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car6 | 悬挂式中耕机 | tractor_cultivator | 苹果园、玉米试验田、农场运输区、番茄温室、草莓种植区、葡萄园、蔬菜试验田、宽地头机具运输区 | 需补资源 |
| Car7 | 凿式犁 | cab_chisel | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car7 | 圆盘耙 | cab_harrow | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car7 | 大蒜播种机 | cab_garlic | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |
| Car7 | 灌水覆膜播种机 | cab_mulch | 玉米试验田、番茄温室、蔬菜试验田、宽地头机具运输区 | 已包含 |

## 5. 默认配方与定位限制

以下按各车裸车绑定调用当前 `select_algorithm` 解析。绑定级机具策略仍以完整配置为准；该API给出测试/推荐配方，不自行切换运行中的算法。

| 车型 | 场景 | 默认定位 | 规划 | 控制 | 定位专用白名单 |
| --- | --- | --- | --- | --- | --- |
| Car0 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car0 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 玉米试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 温室行间 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 葡萄园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car0 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car1 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car1 | 玉米试验田 | navsat | smac_hybrid | mppi | 未追加专用白名单 |
| Car1 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car1 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car1 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car2 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car2 | 玉米试验田 | navsat | smac_hybrid | mppi | 未追加专用白名单 |
| Car2 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car2 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car2 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car3 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 玉米试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 温室行间 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car3 | 葡萄园 | navsat | smac_hybrid | mppi | navsat |
| Car3 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car4 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car4 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car4 | 玉米试验田 | navsat | smac_hybrid | mppi | navsat |
| Car4 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car4 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car4 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car4 | 葡萄园 | navsat | smac_hybrid | mppi | navsat |
| Car4 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car5 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 玉米试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 葡萄园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car5 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car6 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 玉米试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car6 | 葡萄园 | navsat | smac_hybrid | mppi | navsat |
| Car6 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car7 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car7 | 玉米试验田 | navsat | smac_hybrid | mppi | 未追加专用白名单 |
| Car7 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car7 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car8 | 苹果园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 玉米试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 温室行间 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 农场运输区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 番茄温室 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 草莓种植区 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 葡萄园 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car8 | 蔬菜试验田 | fastlivo_rtk | smac_hybrid | mppi | 未追加专用白名单 |
| Car9 | 宽地头机具运输区 | navsat | smac_hybrid | mppi | navsat |
| Car9 | 玉米试验田 | navsat | smac_hybrid | mppi | 未追加专用白名单 |

## 6. 明确排除的推荐组合

| 车型 | 场景 |
| --- | --- |
| Car2 | 草莓种植区 |
| Car7 | 农场运输区 |
| Car9 | 农场运输区 |

上述目录不等于手工菜单对所有组合都提供强制禁用；人工选项仍应遵循场景规则，不能把“可选”当“可用”。

## 7. 当前功能限制

- 场景分组是待验证候选范围，不是已经完成路线验收的白名单。
- 没有实现土壤耕作、作物切割、播种、喷洒、采摘或载荷物理过程；不能推荐为已验证真实作业。
- NavFn、Theta*、Smac2D不保证阿克曼曲率与终点朝向；直连不保证避障或曲率，仅为基线。
- 模拟RTK可在室内场景发布；真机室内NavSat不可据此宣称可用。
- 拖车只有保守扫掠轮廓，没有铰接状态空间规划；仅验证低速前进开阔运输。
- 机械臂为无接触力学的运动学展示；带臂行驶关闭演示，不代表机械臂全扫掠碰撞避障通过。
- 收割机与割台为名义全尺寸仿真，不是厂家真实参数；割台宽约12.73米，不可推荐窄行间。
- 视觉检测、实例分割、姿态估计只作独立感知展示，不参与导航控制，也不代表农作物识别精度。
- 当前装备面板没有喷洒组件，旧代码支持不等于当前可安装选项。

## 8. 依据与更新

- 配置：`ros2_ws/src/agribot_farm_sim/config/compatibility.json`
- 场景：`ros2_ws/src/agribot_farm_sim/config/scenarios.json`
- 解析：`compatibility.py::select_algorithm`，排除：`natural_recommendation.py::EXCLUSIONS`
- 更新：`python3 tools/generate_function_catalog.py`；只读校验：`python3 tools/generate_function_catalog.py --check`

配置SHA256：`071296e1921a903fc60741d5905d9bc242eb7a7bbb7194c1ec5536b474cb0a65`

场景SHA256：`0c2c3acb6b8442a018ac172d016736af4d776adcc7c03b2a3a9fad107a667c92`
