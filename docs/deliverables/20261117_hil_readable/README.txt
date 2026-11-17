控制器硬件在环测试材料：外行可执行操作说明版
整理日期：2026-11-17

一、交付内容

files/中提供以下成品，同时放在主机桌面：
  控制器硬件在环测试材料_外行可执行版_20261117

1. 2026042-01 农机控制器测试用例与实测报告_操作说明版V1.6.docx及PDF。
   沿用原机构用例表，整机、智能机具、自动驾驶导航各10条。
   前面C0至C8给出人员可照做的公共流程，每条再写自动动作、观察重点和查结果方式。
2. 三类控制器硬件在环测试需求规格说明_V2.1_配套说明版.docx及PDF。
   第3.1、3.2、3.3章分别对应三类，a至j与30张用例表逐一对应。
   原V2.1全部验收判据逐字保留，不新增指标、不降低门限。
3. 两份纯文字稿、新测试人员操作步骤.txt、条款与用例对照.csv。
   终端命令应从操作TXT复制，避免PDF折行造成命令错误。
4. 历史30条结果_20261116.json、冻结判据_V2.1.json、证据索引.json及校验结果。
   原始记录仍指向原归档，没有重复复制整个代码库或大型模型。

二、事实边界

历史结论是2026-11-16在已批准V2.1范围下30条通过。
本次仅整理文档并执行离线校验，没有连接/启动台架，没有烧录或重跑实板。
NAV-i为新软件测试与指定历史实板失败/恢复记录核查，不是本轮新实板故障注入。
第三方本次日期、人员、OUT和结论留给实际复测填写，不预填第三方签字。
旧表单和冻结使用说明的“STVL均关闭”在新说明中纠正；不改原冻结文件。
当前配置使用STVL，算法ID的off仅表示关闭额外视觉AI。

三、给测试人员的入口

先读新测试人员操作步骤.txt；不需要先从Unity启动。
运行脚本会实际控制隔离台架，必须由项目人员先核实接线、版本、权限和软件。
REF、OUT、SCRIPT等变量、真实启动及停止命令均在该操作说明中。
全套复测只执行一次run，再审阅7张新图、登记真实人员、finalize并逐条读结果。
不要在已有HIL会话中启动另一轮，不自行发CAN、不拔线、不新增障碍。

新增辅助脚本（均位于本目录，不修改冻结运行脚本）：
- inspect_results.py：只读中文查看进度、整轮或某条结果；区分历史与本次，不控制硬件。
- review_images.py：登记实际看图人员、观察、运行编号及图像哈希，不冒充AI审阅者。
  它只创建新的visual-review.json，不覆盖旧登记，不启动仿真/烧录。
  尚未看图、图片或执行证据不完整时不可使用批准选项。
  输出结构已用原冻结证据审计函数做离线兼容性检查。

四、维护和再生成

build_materials.py、export_materials.py、check_materials.py只处理文档和归档数据。
再生成需要主机现有V1.5原始Word、两阶段结果及完整原始证据，不能从缺少证据的
新下载目录伪造新的实测报告。阅读成品Word/PDF/TXT不需要这些生成依赖。

在仓库根目录，使用已准备的python-docx环境：
  <文档Python> docs/deliverables/20261117_hil_readable/build_materials.py \
    --work <临时路径>/generated --output <交付路径>
  /usr/bin/python3 docs/deliverables/20261117_hil_readable/export_materials.py \
    --work <临时路径>/generated --output <交付路径>
  <文档Python> docs/deliverables/20261117_hil_readable/check_materials.py \
    --output <交付路径>

文档Python需要python-docx；导出使用系统UNO、私有LibreOffice，校验需要Poppler。
不升级Ubuntu，不修改真实X5工作区或已签署归档。
离线工具测试：
  python3 -m pytest -q docs/deliverables/20261117_hil_readable/test_reader_tools.py

阅读/文档校验与实板复测是两回事，不能把离线测试数量写成30条又重新通过。
