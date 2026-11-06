# 运行与排障

| 现象 | 首先检查 |
| --- | --- |
| Unity构建缺资源 | doctor --unity、Git LFS、授权资源包、Editor版本 |
| 启动后没有窗口 | 本次日志、增量构建结果、DISPLAY与GL |
| 相机No Image | 图像话题、QoS、Gazebo渲染和视觉AI节点 |
| 模型红色或材质缺失 | OBJ/MTL/纹理相对路径和完整性 |
| 车原地不动 | 是否发送目标、定位/TF、Nav2状态、路径和约束 |
| 终点绕圈 | 当前goal checker、半轴距容差、目标是否更新、旧进程 |
| 定位漂移 | 场景特征、所选定位是否允许、传感器时间与外参 |
| AI无回复 | 私人Key、模型配置、网络、超时和本地校验错误 |

## 助手流式回复与首字耗时

AI助手使用真实服务端流式响应，不用打字动画模拟。收到正文后逐段显示；每条回复上方的“首字耗时”从点击发送到Unity第一次显示非空正文计时，包含本机准备、网络及必要的场景查询，不是整条回答耗时，也不是固定性能承诺。未得到正文前不会把HTTP响应头、保活消息或工具参数当成首字。

正文生成中只提供文字预览；推荐卡片必须等完整JSON通过车型、场景、算法和版本校验后才可点击。格式不合格仍按原策略最多纠正一次，重新生成前清除旧预览。断流、超时或校验最终失败显示“回复未完成”，不把草稿写入上下文或当成可执行方案。正常回复的首字耗时随该条会话记录保留，不发送回模型作为用户偏好。

传输遵循[DeepSeek Chat Completions流式接口](https://api-docs.deepseek.com/api/create-chat-completion/)，保留现有关闭思考模式、只读场景工具和连续会话机制。命令行兼容单JSON输出；`natural_recommendation --stream`输出逐行JSON事件（reset/text/result），最后的result才是校验结果。密钥仍仅保存在本机私有配置。

## 日志

ROS构建日志在ros2_ws/log，运行日志在ROS日志目录和受管任务状态目录。Unity会话日志位于persistentDataPath/assistant_sessions，不上传Git。交付检查记录写入.validation。

```bash
source scripts/env.sh
ros2 node list
ros2 topic list
ros2 action list
ros2 lifecycle nodes
```

仿真检查工具与被测节点应使用相同ROS_DOMAIN_ID、ROS_LOCALHOST_ONLY和DDS配置。共享电脑不要全局killall；使用软件停止按钮或正常Ctrl+C结束自己的任务。

## 资源与性能

相机渲染即使不显示Gazebo窗口仍需要图形环境；软件渲染和高精模型可能降低实时率。以/clock相对墙钟判断仿真速度，不能将画面更新率直接当成传感器真实采样率。

高带宽话题、队列滞后和CPU/内存都需检查。静态话题列表存在不等于消息持续正常，定位准确与否也需实际轨迹验证。

## 升级和数据

切换车辆前先停止受管仿真。修改尺寸、传感器或轮半径后重新导出，旧配置SHA和验证结论不能直接复用。保留用户导出与原始扫描源文件。

删除导入场景仅清目录项和转换缓存，不删除原始扫描文件。不要通过改绝对路径、跳过校验或空模型来把失败隐藏成成功。
