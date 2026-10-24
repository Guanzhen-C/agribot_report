# 匹配源码的 Linux 程序包

本目录提供从本仓库源码构建的 Ubuntu 22.04 x86_64 程序，不是原开发目录中的旧可执行文件。

- 资源：完整10车型、57个装配预设及当前界面。
- 程序包大小、SHA-256、源码/资源指纹：见[manifest.json](manifest.json)。
- 构建输入版本见清单中的`runtime_inputs.source_commit`；仅文档或本目录的发布提交不会改变运行输入指纹。
- 不包含用户园区扫描数据、私人AI配置、Unity Editor或Unity许可证。

先下载完整仓库与Git LFS资源并安装ROS Humble。普通安装使用：

```bash
bash scripts/bootstrap.sh
python3 tools/configure_llm.py
bash scripts/run_unity.sh
```

也可以分步骤构建ROS、安装视觉环境后执行`bash scripts/install_app.sh`。安装器会核对程序包SHA-256及运行输入指纹，再解压到`dist/unity`，不覆盖已有应用。重复安装前应退出应用并将旧目录移到备份位置。

程序必须连同同一仓库中的ROS仿真代码运行；单独复制这个压缩包不构成完整部署。启动脚本按相对位置寻找仓库，用户场景和应用状态写入本仓库被忽略的`.local/`，不会访问开发者的原工作区。

修改源码或资源后，应从干净提交重新执行`bash scripts/build_unity.sh`和`python3 tools/package_app.py`。打包器不会给旧程序事后补写匹配版本。
