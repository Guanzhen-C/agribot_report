---
name: "FieldGate VCU"
description: "蓝色曲线桌面、玻璃导航与清晰内容层组成的离线农机工作空间。"
colors:
  paper: "#f3f6fb"
  paper-2: "#ffffff"
  paper-3: "#eaf0f7"
  ink: "#182b43"
  ink-2: "#304760"
  muted: "#52647b"
  rule: "#d4dfea"
  input-rule: "#7c8ca1"
  accent: "#0062cc"
  accent-hover: "#004fa8"
  accent-ink: "#ffffff"
  accent-paper: "#e1efff"
  focus: "#003f91"
  success: "#246341"
  success-paper: "#e7f4ed"
  warning: "#805009"
  warning-paper: "#fff4dc"
  error: "#ac2937"
  error-paper: "#ffeced"
  disabled: "#dce4ee"
  model-background: "#e8f0f3"
  desktop: "#4cc5f1"
  desktop-deep: "#1271e4"
  desktop-line: "rgb(255 255 255 / .58)"
  desktop-text: "#132e50"
  desktop-muted: "#244b72"
  wave-ice: "#a4e8ff"
  wave-cyan: "#41caf3"
  wave-sky: "#43b8fa"
  wave-blue: "#0754e7"
  glass: "rgb(241 249 255 / .52)"
  glass-solid: "#e3f0fb"
  glass-highlight: "rgb(255 255 255 / .9)"
  glass-selected: "rgb(255 255 255 / .68)"
  content-material: "#f3f6fb"
  widget-material: "#ffffff"
  widget-blue: "#ffffff"
typography:
  display:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"SF Pro Text\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "3rem"
    fontWeight: 500
    lineHeight: 1.15
    letterSpacing: "-.04em"
  headline:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"SF Pro Text\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "1.875rem"
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: "-.035em"
  body:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"SF Pro Text\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"SF Pro Text\", \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif"
    fontSize: ".875rem"
    lineHeight: 1.5
rounded:
  control: ".875rem"
  panel: "1.5rem"
  dialog: "1.75rem"
  app: "1.125rem"
  dock: "1.75rem"
spacing:
  xs: ".25rem"
  sm: ".5rem"
  control: ".75rem"
  md: "1rem"
  lg: "1.5rem"
  xl: "2rem"
  2xl: "3rem"
  3xl: "4rem"
  dock: "6.5rem"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.accent-ink}"
    rounded: "{rounded.control}"
    padding: ".5rem 1rem"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.accent-ink}"
  button-secondary:
    backgroundColor: "{colors.paper-2}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: ".5rem 1rem"
  text-input:
    backgroundColor: "{colors.paper-2}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "{spacing.control}"
  device-tag:
    backgroundColor: "{colors.paper-3}"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.control}"
    padding: ".25rem .5rem"
  standard-widget:
    backgroundColor: "{colors.widget-material}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
  navigation-dock:
    backgroundColor: "{colors.glass-solid}"
    textColor: "{colors.desktop-text}"
    rounded: "{rounded.dock}"
    padding: ".5rem .75rem"
  navigation-dock-glass:
    backgroundColor: "{colors.glass}"
---

# FieldGate 界面设计

## Overview

**Creative North Star: "蓝色曲线上的清晰工作空间"**

面向专业农机用户的 VCU 工作界面，作者刘泽禹；FieldGate 不是执行器品牌。按用户批准的整体视觉替换，参考所给 iCloud 网页截图，以蓝色曲线、浅色模块和底部程序坞组织应用，保留真实设备状态与专业工作流。

玻璃仅用于顶栏与程序坞，内容卡片使用不透明白底，圆角与轻阴影区分层级。导航复用 simple-liquid-glass 的网页光学效果，不是 Apple 原生 Liquid Glass 渲染器，也不代表官方认证。

材质分工参考 [Apple HIG · Materials](https://developer.apple.com/design/human-interface-guidelines/materials)：导航与控制使用 Liquid Glass，内容层使用标准材质，并避免滥用玻璃效果。

**Key Characteristics:**
- 明亮蓝色桌面与克制的玻璃导航。
- 内容优先，状态用文字和颜色共同表达。
- 本机字体与资源，动效服务于页面进入和操作识别。

## Colors

主色是清晰的操作蓝；浅冷白承载内容，深蓝灰承载文字。前置 YAML 从 `tokens.css` 抽取并保留实际 CSS 颜色格式；它是文档令牌层，运行值仍以 CSS 为准。

- **Primary：** `accent` 用于主操作、选中与链接；`accent-hover` 为悬停状态，`focus` 为键盘焦点。
- **Neutral：** `paper` 系列用于工作区、字段与标签；`ink`、`muted` 区分主次信息；`rule` 与 `input-rule` 分隔容器与输入边界。
- **桌面与材质：** `desktop`、`wave-*` 组成蓝色装饰几何；`glass` 是支持模糊时的导航背景，`glass-solid` 是不支持时的实色回退。`content-material`、`widget-material` 用于高不透明内容。
- **语义：** 绿色表示正常，琥珀表示预警，红色表示故障；均伴随文字。各应用图标沿用 `color-icon-*` 专属渐变，仅识别应用，不代表运行状态。

## Typography

显示与正文共享系统字体栈：优先 Apple 系统字体，其后为 Segoe UI、苹方和微软雅黑；不下载字体，实际显示取决于本机可用字体。标题保持紧凑字距；仪表数字用等宽数字特性，不另造装饰字体。

正文、标签、页面标题、仪表主数值分别对应前置 `body`、`label`、`headline`、`display`。桌面“工作空间”主标题仅供辅助技术识别，不占据可见布局；电池数值使用局部大号显示（3.75rem），不扩展为通用标题规范。

## Layout

顶栏在上、程序坞固定在底部并避开安全区；桌面内容最大宽度（1056px），大屏三列，中屏（不超过 64rem）两列，窄屏（不超过 42rem）一列。小于 23rem 进一步压缩导航间距；90rem 起微调宽屏空间。业务工作区桌面端内部滚动，窄屏恢复页面滚动。通用间距沿四点节奏，底部预留使用 `spacing.dock`。

七个路由保持原职责：
- `/launchpad`：工作空间；工作区身份、真实装配模型入口、应用网格、电池与诊断摘要。
- `/`：状态指标、带标注的设备功能示意、作业功能与能量趋势。
- `/preview`：默认仅执行机构的本机 CAD 装配模型、爆炸展开与配置草稿；关联可选，全部外观另选；机械外观与运行配置分开。
- `/robot`：独立“算力与感知”工作区，保存安装描述并生成 URDF 包；关节、轴向、限位等按需展开，不写入 VCU。
- `/devices`：当前农机、左右驱动与作业设备反馈；链路关系仍在总览与诊断。
- `/diagnostics`：当前告警、处理建议与通信状态；缺少反馈不宣称无故障。
- `/settings`：机型、原协议、算力接口与真实配置写入；保留原 ID 和键值。

`?preview=1` 始终显示预览标识，提供待机、作业和告警示例场景，禁止串口连接和写入。程序坞含桌面、总览、预览、设备、诊断、设置；“算力与感知”从应用网格进入。

## Elevation & Depth

**The 导航分层 Rule.** 玻璃只用于顶栏与程序坞，绝不把玻璃层扩散到内容面板。

顶栏提供“玻璃特效”开关，默认开启。选择保存在本浏览器的 `fieldgate.glass-effects.v1`，同源标签页同步；关闭时卸载两处光学组件并使用实色导航，不留下背景模糊。存储受限时仍可切换，并提示仅在本次使用中生效。系统减少透明度、高对比度和强制颜色设置优先于开启选择。此设置不影响设备配置或控制状态。

导航光学层直接使用 `simple-liquid-glass@5.3.0`（lucaperullo，MIT）；`GlassOptics` 仅适配客户端挂载、无障碍偏好和导航尺寸，不再维护法线贴图、折射滤镜或自写反光算法。采用库的 `clear` 材质、`lens` 折射、`high` 质量、6px 模糊和 115% 饱和度；浅色透明底通过库公开参数设置。光学层位于原有文字、图标和点击区域下方，业务状态不交给第三方库管理。

Edge / Chrome 使用库的原生 SVG 背景折射路径，高光与圆角透镜由库生成。不运行持续液体动画，以保留界面响应；原有图标悬停和页面切换动效仍保留。库在浏览器 hydration 完成后挂载，服务端保留可读背景；减少透明度、高对比度或强制颜色模式卸载光学组件。其他浏览器回退为普通导航材质，不宣称同等折射效果。内容保留 `shadow-widget`，图标保留 `shadow-icon`。

此前尝试的 `liquid-glass-react` 虽通过挂载和业务检查，但在本页图像对照中未显示有效背景折射，已移除依赖，不作为有效版本交付。测试通过与视觉通过分别记录。

对照 [Apple · Meet Liquid Glass](https://developer.apple.com/videos/play/wwdc2025/219/) 的透镜、折射与反光特征，验收须检查背景确实产生位移，不能仅凭存在 `backdrop-filter` 就判定完成。

页面进入使用 `dur-short` 渐显；桌面模块按 `dur-arrival` 轻微缩放落定，后续模块延迟依次为 25、50、75、100ms。精细指针悬停时应用图标轻抬（3px）、程序坞图标轻抬（5px），模型缩略图轻微放大；按下图标缩至 .94，按钮沿用 1px 按压。没有持续循环动画；减少动态效果时关闭入场与图标、模型变换，并保留原按钮位移禁用规则。具体时长与缓动见 sidecar。

## Shapes

桌面滚动区用静态透明度遮罩衔接上下边缘（20px / 28px），移动端在程序坞后方使用不拦截点击的底部渐变。不新增滚动监听或卡片滤镜；高对比度和强制颜色模式移除这些渐隐。

控件、卡片、工作区、应用图标与程序坞分别使用对应圆角令牌；顶栏按钮与分段控件为胶囊形。图标颜色与形状用于辨认应用，内容容器避免多层边框。桌面 SVG 蓝色曲线是静态装饰几何（隐藏于辅助技术，不接收指针），不是设备模型或实物图像。`public/models/ackermann-poster.png` 继续使用原真实装配模型图片，不修改源图。

## Components

- **按钮：** 主操作蓝底白字，次操作白底深字，顶栏轻按钮采用浅底胶囊；常规主操作至少 48px 高，顶栏按钮为 44px。悬停只在支持悬停的设备启用；键盘焦点清晰，禁用状态保留语义与交互阻止。
- **输入与标签：** 本机草稿字段使用白底、清晰边线与控件圆角；输入至少 48px 高。设备标签为浅冷灰底，不伪装成按钮；错误说明放在对应操作附近。
- **卡片与导航：** 卡片不透明白底，轻阴影区分模块；导航玻璃可关闭并使用实色回退。当前程序坞应用有底色、圆点及 `aria-current`；诊断徽标只来自现有告警数据。
- **模型与数据：** 部件列表保留不依赖画布的键盘入口；原有单指旋转、双指缩放和平移能力不变。串口连接与 ACK 校验沿用现有 API，串口打开不等于收到状态。新增配置仅存浏览器草稿，可导出原始 JSON 和 FGMP；原串口设置仍须真实 ACK。模型只在本机离线转换，原文件不改，不新增远程服务。
- **文案：** 用标题表达用途，不追加无效副标题；预览标识集中展示，草稿与设备写入分开，未收到确认不报成功。标定、停机确认与导出限制放在相应操作附近，协议明细按需展开。

## Do's and Don'ts

### Do:
- Do 保留七个路由、真实串口与 ACK 边界；未收到反馈显示“—”，不用零或成功状态替代。
- Do 让玻璃只承担导航层；内容卡片、表单和工作区使用标准材质。
- Do 保留预览标识、键盘入口、草稿离开确认和减少动态效果规则。
- Do 保留真实模型图片和本机模型来源，文案只说明用途、状态、单位与操作后果。

### Don't:
- Don't 把玻璃叠到内容卡片上，或增加持续漂浮、闪烁、自动循环的装饰动效。
- Don't 把图标专属颜色当作设备运行状态，把示例数据混入真实数据，或把浏览器草稿称作实机写入。
- Don't 把 CAD 尺寸当作标定、把设备功能示意当作实物比例模型，或把资源发布到外网。
- Don't 在界面上加入开发说明、需求回复、口号、假系统时间、电量、浏览器框或营销代码展示。

**验证边界：** 业务、类型、构建与浏览器检查证据保存在 `../../outputs/FieldGate_VCU_Liquid_Glass/VERIFICATION.txt`；开关、持久化、白色卡片与滚动边缘记录为 `effects-browser.json`。此前 `optics-*` 和 `card-performance-*` 分别属于自写效果及已撤下的卡片效果，不用于证明当前方案的性能。视觉效果仍须用户确认；不宣称 Apple 原生一致性或官方认证。
