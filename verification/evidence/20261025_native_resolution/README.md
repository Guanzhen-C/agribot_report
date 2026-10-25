# 全屏字体清晰度修复

## 根因

旧版本保存了1600×900渲染分辨率及“Use Native=0”。无边框窗口虽然铺满2560×1440屏幕，仍把1600×900画面拉伸输出，导致字体和模型图像一起模糊。并非缺少字体或需要将文字改为图片。

## 修改

全屏无边框模式启动后，读取软件所在显示器的实际分辨率，显式设置内部渲染分辨率。窗口模式、独占全屏和无法获取显示器尺寸时不强制改变用户设置。没有修改字体大小、字库、布局、模型或ROS算法。

## 验证

- 使用同一份旧1600×900偏好配置，在同一个2560×1440隔离桌面运行修复前后Linux程序。
- 修复后日志实测：`UI_NATIVE_RESOLUTION previous=1600x900 target=2560x1440`，随后`UI_FRAMEBUFFER render=2560x1440 monitor=2560x1440 mode=FullScreenWindow`。
- `resolution_before.png`与`resolution_after.png`为实际窗口截图；`text_before.png`与`text_after.png`是从同一窗口坐标直接截取的650×200区域，没有放大、锐化或图片后处理。
- 6项分辨率策略测试全部通过：2K、4K、已是原生分辨率、缺少显示器尺寸、窗口模式与独占全屏。
- 5项界面回归全部通过，覆盖退出按钮、工具栏、装备面板和中文提示。Python源码测试120项通过。
- 原生分辨率切换后实际点击右上角退出按钮，程序正常退出，systemd结果为success、MainPID为0。

运行源码提交为`e585601`。

## 正式安装

桌面快捷方式对应独立目录已拉取GitHub源码，通过同SHA本机LFS缓存复用并安装匹配程序包，未重新下载同一个1.9GB文件。安装器校验包SHA-256及运行输入指纹通过，旧程序备份为`dist/unity_before_native_resolution_20261025`。

正式安装版实际启动日志同样显示`previous=1600x900 target=2560x1440`，最终`render=2560x1440 monitor=2560x1440`。`installed_native_resolution.png`记录正式安装版实际窗口，没有图片缩放处理。程序包SHA-256为`b21550e17aecdd1d0084d4269dffe0334344012ee5d243389179b93a603717e8`。GitHub Source Checks运行40194365278通过。
