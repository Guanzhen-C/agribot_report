三类控制器硬件在环测试需规 V2.0 编制说明

按用户2026年11月12日最终确认，类别和顺序为：
整机控制器、智能机具控制器、自动驾驶导航控制器。
各类a)至j)十条，共30条，不是此前临时提到的每类19条。

requirements.json是本版文字源，包含前置、操作、判定和必需证据。
reference_run_summary.json是既有运行汇总的逐字节摘存，不是新执行结果。
build_documents.py生成同内容的Word和纯文本；check_documents.py核对Word、PDF、纯文本的30条编号、150个用例字段及PDF页面边界。
本目录脚本仅处理文档，不烧录、不启动仿真、不发送控制报文。

文档编制依赖：Python 3.10及以上、python-docx 1.2.0、LibreOffice Writer、Poppler pdftotext。
在仓库中运行：
python3 docs/deliverables/20261112_hil_srs/build_documents.py --output <交付目录>
libreoffice --headless --convert-to pdf --outdir <交付目录> <交付目录/生成的Word文件.docx>
python3 docs/deliverables/20261112_hil_srs/check_documents.py <交付目录>
依赖应安装在文档专用虚拟环境；不得为生成文档升级Ubuntu或改动ROS环境。

本次交付目录：/home/cgz/Desktop/三类控制器硬件在环测试需规_20261112
Word、PDF均为17页。每条用例适用多个工况时逐工况记录，但不增加独立用例数量。
本版是重新定义的测试需求，不是30条已全部通过的测试报告。已知碎枝导航失败和拖拉机通信保护事件保留在单独章节，待整改复测，不降低门限掩盖问题。

操作步骤与真实结果补充
execution_cases.json逐条登记实际操作、结论、覆盖边界及待补测项；不是预填通过表。
build_execution_report.py生成配套的《三类控制器硬件在环测试步骤与实测结果_V2.0_20261112》Word及纯文本，再用LibreOffice转成PDF。
该补充共31页，仍按三类各10条：6条通过、4条不通过、20条未完成完整专项。后者不少已有正常运行证据，但不能代替全部拒绝性/异常试验。
交付目录另含30条实测对照表.csv、逐项实测记录.json、证据索引.json、55份证据副本和实测报告排版核查.json。
65处量值由证据文件的JSON Pointer实际读取，不在Word中手填测量数值。两幅动力曲线来自既有真实HIL记录，原图哈希及失败审计结论保留。
review_implement_payloads.py只读取已保存CAN记录；新增负载多重集复核不等于新运行、不用于宣称同步时延或零丢包。
check_execution_report.py核对30条编号/结论、55份文件哈希、65处原始量值、Word/PDF/CSV/纯文本一致性、曲线原图及页面边界。
有交付证据时可离线重建，不需连接笔记本、X5或6750：
python3 docs/deliverables/20261112_hil_srs/build_execution_report.py --evidence-dir <当前交付目录> --output <新交付目录>
python3 docs/deliverables/20261112_hil_srs/check_execution_report.py <含生成PDF的新交付目录>
文档生成及检查不执行其中归档的历史启动命令；实际补测必须另建运行编号并保留本次失败与未完成状态。
