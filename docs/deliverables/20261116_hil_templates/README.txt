2026-11-16 第三方模板填写工具

输入：用户提供的2026042-01测试计划.doc及测试用例.docx，转换工作副本位于
/home/cgz/.local/state/agribot-hil-templates-20261116/converted/。
内容：../20261112_hil_srs/requirements.json和桌面已归档的逐项实测记录.json。
输出：/home/cgz/Desktop/农机控制器测试计划与用例_按模板填写_20261116/。

fill_templates.py使用python-docx，保留原模板结构，填入三类各10条及6/4/20覆盖结论。
export_documents.py使用系统Python UNO及私有LibreOffice实例更新目录/页码，导出docx/doc/PDF。
两者都只做文档与归档I/O，禁止加入硬件、ROS、CAN或烧录操作。

生成：
/home/cgz/.local/state/agribot-hil-docs-20261109/venv/bin/python docs/deliverables/20261116_hil_templates/fill_templates.py
env LD_LIBRARY_PATH=/usr/lib/libreoffice/program:/usr/lib/x86_64-linux-gnu /usr/bin/python3 docs/deliverables/20261116_hil_templates/export_documents.py

先将输出测试计划.doc再转换到工作目录doc-roundtrip/作兼容性复核，然后运行：
/home/cgz/.local/state/agribot-hil-docs-20261109/venv/bin/python docs/deliverables/20261116_hil_templates/check_documents.py
校验30条编号、三个类别、6/4/20结论、所有历史步骤/判据/量值/未覆盖分支、证据摘要、PDF页码及页边界。

机构原文档标识JH/YL-2026042-02及原计划人员安排保留，正式编号、第三方复测、签署由机构确认。
不将原模板人员、日期、发动机标定值或性能阈值虚构为本轮实测。
