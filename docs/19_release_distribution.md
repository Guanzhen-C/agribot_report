# Release资源与运行包

## 当前分发方式

源码、Unity元数据、场景配置、许可证、资源锁和程序包清单保留在`main`。大体积三维模型文件、预览数据、视觉权重通过本仓库GitHub Releases附件分发；完整Linux程序也作为单独附件。不是把大文件切碎塞进普通Git，也不重写已有提交历史。

`resources/release_assets.json`锁定资源附件URL、大小、SHA-256，以及每个恢复路径的大小、SHA-256和模式。多个相同模型只在附件中保存一次，恢复后仍保留原来的所有目录与文件名，不简化模型、不改变算法或碰撞体。

`prebuilt/manifest.json`锁定Linux程序附件和运行源码/资源指纹。安装器同时校验下载SHA与构建时指纹，不允许把旧程序标记为新程序。

## 新电脑安装

目标环境仍是Ubuntu 22.04、ROS 2 Humble、Gazebo Classic 11。先准备ROS Humble Desktop，然后：

```bash
sudo apt-get install git zstd
git clone --depth 1 https://github.com/Guanzhen-C/agribot_report.git
cd agribot_report
bash scripts/bootstrap.sh
python3 tools/configure_llm.py
bash scripts/run_unity.sh
```

不需要GitHub账号、Git LFS、Unity Editor或开发者缓存来下载公开附件。源码构建仍需要自己的Unity Editor许可证。个人AI Key、用户扫描场景和X5接线配置不会发布。

只恢复模型用`python3 tools/fetch_assets.py`；只安装程序用`bash scripts/install_app.sh`。查看全量资源指纹用`python3 tools/fetch_assets.py --check --hashes`与`python3 tools/doctor.py --unity --hashes`。

## 中断、离线与本地修改

附件下载中断后重跑原命令，会续传并在完成后校验完整SHA。损坏附件不会替换正式文件；解压只接受清单中的普通文件，不接受越界路径和符号链接。

下载到其他机器的资源附件可以集中放在一个目录，然后运行：

```bash
python3 tools/fetch_assets.py --bundle-dir /path/to/downloaded-bundles
```

程序附件放在`prebuilt/`中的清单文件名位置，可离线执行`python3 tools/install_prebuilt.py`。仍需相同源码版本及已安装的ROS运行依赖。

工具不会静默覆盖用户修改的模型。遇到修改时先停止并报告路径；只有明确要丢弃这些受管理资源的修改，才使用`--repair`。用户导入场景和个人配置不属于该资源清单。

## 开发与发布

资源数据在Git中改为忽略，只跟踪资源锁；因此普通`git status`不能代替资源校验。发布构建的运行指纹会逐文件检查锁中的SHA，任何未登记模型修改都会拒绝构建。

发布者使用`tools/package_release_assets.py`从当前锁/迁移前LFS指针生成去重附件，先提交源码与新资源锁，再正常构建和`tools/package_app.py`打包。每个附件小于2GiB；附件上传到与清单一致的固定版本Tag，发布后不覆盖相同Tag的内容。[GitHub附件限制说明](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)

发布须先实际验证所有附件上传和新克隆安装，再将`main`推进到完整发行提交。GitHub授权只用于仓库发布，不开启付费预算。旧历史提交的LFS下载仍可能受原预算限制；本次只保证迁移后的版本使用新的安装流程，不宣称恢复了GitHub账号的历史LFS服务。
