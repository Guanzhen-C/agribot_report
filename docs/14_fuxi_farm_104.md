# 伏羲农场固定104点仿真

## 运行方案

“伏羲农场”是原来导入的 `1029_render2` 扫描场景的新显示名。仍然使用完整的
6,660,923个高斯及原扫描射线网格，不重新转换或降低外观精度。

本场景提供以下已验证推荐配置，但不锁定用户的算法选择：

- 车辆：Car3小型差速履带车，使用已验证的四个导轮轮心、连续履带接触体、原CAD及传感器安装位置。
- 感知：STVL和原C16射线输入；相机继续渲染完整高斯，未启用额外AI视觉节点。
- 定位：KF-GINS，输入模拟GNSS位置、双天线航向和Gazebo IMU。
- 跟踪：项目已有的位置终点判定RPP控制器；保留Smac局部恢复能力。
- 规划：使用已有Smac选项，差速车实际使用Smac State Lattice，不增加新的规划算法。
- 路线：自动载入原104个经停点的坐标与朝向，由Smac重新规划。真机已验收轨迹为11,285个位姿、711.985米；新规划必须与它逐点完全一致，才允许RPP开始跟踪。不是直接把原密集轨迹当作本轮规划结果。
- 终点位置容差：Car3轴距的一半，约0.4185米。不要求在终点额外调整航向。

固定方案不将未验证的车辆、农机具或其他算法自动冒充为已经通过的配置。
其他场景仍按原方式选择车辆、算法和用户经停点。

## 在软件中启动

1. 从桌面打开“农机配置平台”。
2. 在车库中选择Car3，保留默认传感器安装配置，不挂载农机具。
3. 点击“启动仿真”，在场景页选择“伏羲农场”。
4. 算法页自动选中该场景的已验证方案，规划仍显示Smac，路线显示“预设104个经停点”。
5. 点击“启动仿真”。软件完成输入检查、增量编译后，自动启动仿真及路线跟踪。
6. 完成后车辆停车，保留结果和完整轨迹显示；点击软件中的“停止仿真”结束本轮进程。

此过程不连接CAN，不向真车发送命令。运行在独立ROS域93和本机Gazebo端口11393，
不可同时启动第二个占用相同端口的该场景实例。软件会先停止自己管理的上一轮仿真。

输入检查发现传感器坐标、车体参数或固定模型发生变化时，会拒绝把该配置当作原验证方案运行。
Unity浮点导出允许1e-5的数值舍入差异，但不允许厘米级安装变更。

## 数据和结果

场景目录位于仓库 `.local/scanned_scenes/`，其 `catalog.json` 保存显示名和固定方案引用。
原高斯、地图和转换缓存不写入Unity场景源码，也不复制进每一轮结果目录。

`fixed_104_smac_v2/profile.json` 记录固定输入、模型文件和原路线指纹，`template/` 保存独立运行输入。
它不依赖旧的测试结果目录进行导航。`baseline_result.json` 仅作为原验收证据，
不会复制成新一轮的 `result.json`。

每次启动新建：

```text
.local/state/agribot-unity-sim/fuxi_farm_runs/日期_时间/
```

- `software_launch.json`：本次Unity导出路径、固定方案、车型和路线指纹。
- `smac_planning.json`：本轮真实Smac动作结果、规划耗时，以及每个位姿与原轨迹的相等性检查。
- `recomputed_route.json`：本轮Smac新返回的完整轨迹，不是缓存轨迹的替身。
- `live_status.json`：当前路线进度及经过的锚点数。
- `runtime_vehicle.sdf`：本次实际使用的物理模型和驱动参数。
- `actual_trajectory.csv`：独立物理轨迹、KF-GINS估计位置和控制指令。
- `result.json`：本轮完整验收结果；没有该文件或 `passed` 不为 `true`，均不能称本轮通过。

本次最新结果目录还写入 `.local/state/agribot-unity-sim/latest_fixed_route_bundle`。
原始104点文件和真机导航地图不被本流程覆盖，也不向X5部署。

## 验收范围

完整通过要求：104点按顺序在0.75米内实际经过、最终实际位置误差不超过0.5米、
真实物理速度满足停稳条件、无倾覆或跌落、受保护源文件指纹不变。
控制器返回成功不能单独替代这些检查。

地面采用保留道路高程的连续硬地近似。人工清洗地图确认可通行的低矮扫描面交给
地形支撑，实际地图障碍及高处结构保留硬接触，原网格仍供雷达射线使用。
这不是土壤变形、作物力学、涉水或真实任意沟坎越障的验证。
本方案是KF-GINS + RPP，不将其标为MPPI或FAST-LIVO2融合精度验收。

## Gazebo录屏画面

Gazebo只显示三维场景和车辆，不再加载额外的相机预览面板。
相机传感器和图像数据继续保留，可在RViz查看 `/camera/rgb/image_raw`。
Gazebo主视角由用户手动控制，不自动跟随或固定观察位置。

扫描模型未包含完整天空，运行时补充Gazebo自带的天空背景，作为可视化环境，
不是扫描重建或定位特征。该背景同时出现在Gazebo主视角和真实仿真相机图像中，
不通过替换黑色像素伪造天空，也不更改原高斯、碰撞体或原始世界文件。
运行时派生文件为 `sky_world.sdf`、`sky_scene.json` 和 `sky_view.json`。
旧运行记录不作为下一次启动的场景输入。

完整Unity资源打包应与本场景仿真分开执行。这台16 GB内存主机同时进行两者时，
曾因内存压力被系统终止仿真；这样的中断记录不能作为全程通过结论。

## 从已有完整验收结果安装固定方案

维护人员使用下列工具，将已通过的完整验证输入冻结为新的本机场景方案目录：

```bash
python3 ros2_ws/src/agribot_differential_fastlivo_mppi/scripts/fuxi_farm_profile.py install \
  /path/to/accepted_validation_bundle \
  /path/to/imported_scene/fixed_104 \
  --workspace "$PWD/ros2_ws" \
  --planning-parameters /path/to/original_x5_runtime_params.yaml
```

安装器要求原完整报告通过、104点及密集路径完整、原始源文件指纹一致；
不会覆盖已有方案目录。场景目录的条目需包含：

```json
{
  "name": "伏羲农场",
  "simulationProfile": "/path/to/imported_scene/fixed_104/profile.json",
  "fixedVehicleId": "unity_car3",
  "fixedAlgorithmId": "stvl_off_navsat_smac_hybrid_rpp"
}
```

以上是原导入场景条目的附加字段，不是用这四个字段替代整个场景目录。
场景仍保留原 `id`、`sceneManifest`、`imported` 等导入信息。

安装时还应传入 `--planning-parameters /path/to/original_x5_runtime_params.yaml`，
保存原Smac和全局代价地图参数。软件初始规划使用真机相同二维PGM与YAML的逐字节副本，
不使用3DGS投影生成的二维图，也不改变原地图分辨率、原点或占据判定。
初始全局规划复现原离线验收时的静态地图状态；车辆运行中的局部STVL避障仍保留，
不会把新传感器观测混入这次要求完全一致的初始全局路线。

AI助手的场景工具会返回该已验证Car3组合和预设104点规则；普通导入场景不因同名
而继承这个结论。其他场景继续在RViz手工选点，不自动加载这104点。
