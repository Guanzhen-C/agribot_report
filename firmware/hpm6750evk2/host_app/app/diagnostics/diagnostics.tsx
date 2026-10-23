"use client";

import { useState } from 'react';
import { Icon, PageHeading, PanelHeading, Status, number, useConsole } from '../console-shell';

export function Diagnostics() {
  const { data, preview, scenario, connected } = useConsole();
  const [filter, setFilter] = useState('all');
  const [exportError, setExportError] = useState('');
  const rows = [
    ['vehicle', '车辆状态', '0x532', data.mode, '500 ms'],
    ['drive', '左驱动', '0x533', `${number(data.left.rpm)} rpm`, '500 ms'],
    ['drive', '右驱动', '0x534', `${number(data.right.rpm)} rpm`, '500 ms'],
    ['work', '植保风机', '0x5B2', data.functions[0].state, '500 ms'],
    ['work', '供液水泵', '0x5B3', data.functions[1].state, '500 ms'],
    ['battery', '电池组', '0x650', `${number(data.soc, 1)} %`, '200 ms'],
  ].filter(row => filter === 'all' || row[0] === filter);

  function exportSnapshot() {
    setExportError('');
    try {
      const record = { product: 'FieldGate VCU', author: '刘泽禹', capturedAt: new Date().toISOString(), source: preview ? '界面预览：示例数据，非实机记录' : '本机页面快照：尚无有效遥测', scenario: preview ? scenario : null, serialPortOpen: connected, telemetryVerified: false, data };
      const url = URL.createObjectURL(new Blob([JSON.stringify(record, null, 2)], { type: 'application/json;charset=utf-8' }));
      const anchor = document.createElement('a');
      anchor.href = url; anchor.download = `FieldGate-${preview ? 'preview' : 'diagnostic'}-${Date.now()}.json`;
      document.body.append(anchor); anchor.click(); anchor.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch { setExportError('记录未能导出，请重试。'); }
  }

  return <>
    <PageHeading title="设备诊断"><button className="fg-button" onClick={exportSnapshot}><Icon name="download" size={18}/>导出记录</button></PageHeading>
    {exportError && <p className="fg-notice tone-danger" role="alert">{exportError}</p>}
    <section className={`fg-health ${data.alerts.length ? 'has-warning' : ''}`} aria-labelledby="health-heading">
      <div className="fg-health-symbol"><Icon name={data.alerts.length ? 'warning' : preview ? 'check' : 'link'} size={28}/></div>
      <div><h2 id="health-heading">{data.alerts.length ? `${data.alerts.length} 项需要关注` : preview ? '当前无告警' : '等待有效设备反馈'}</h2>{data.alerts.length ? <p>请处理告警后继续作业。</p> : !preview && <p>请检查连接与设备电源。</p>}</div>
      <Status tone={data.alerts.length ? 'warning' : preview ? 'good' : 'muted'}>{preview ? '场景示例' : connected ? '串口已打开' : '设备未连接'}</Status>
    </section>
    {data.alerts.map(item => <section className="fg-alert-detail" key={item.title}><Icon name="warning"/><div><h2>{item.title}</h2><p>{item.detail}</p><span>来源：{item.source}</span></div></section>)}
    <div className="fg-diagnostic-layout">
      <section className="fg-panel"><PanelHeading title="通信链路"/><div className="fg-diagnostic-links">{[['算力模块 → VCU', '控制输入'], ['VCU → 执行器', '指令转发'], ['执行器 → VCU', '设备反馈']].map(([title, description]) => <div key={title}><span><strong>{title}</strong><small>{description}</small></span><Status tone={preview ? 'good' : 'muted'}>{preview ? '正常' : '未获取'}</Status></div>)}</div></section>
      <section className="fg-panel"><PanelHeading title="安全检查"/><dl className="fg-facts fg-safety-facts"><div><dt>紧急停止</dt><dd><Status tone={preview ? 'good' : 'muted'}>{data.emergency}</Status></dd></div><div><dt>行走状态</dt><dd>{data.running}</dd></div><div><dt>作业模式</dt><dd>{data.mode}</dd></div><div><dt>电池电量</dt><dd>{number(data.soc, data.soc !== null && data.soc % 1 ? 1 : 0)} %</dd></div></dl></section>
    </div>
    <section className="fg-panel fg-feedback-panel"><PanelHeading title="反馈一览"><label className="fg-inline-field">筛选<select aria-label="反馈分类" value={filter} onChange={e => setFilter(e.target.value)}><option value="all">全部设备</option><option value="vehicle">车辆状态</option><option value="drive">行走系统</option><option value="work">作业设备</option><option value="battery">电池组</option></select></label></PanelHeading>
      <div className="fg-table-wrap"><table className="fg-table"><caption className="fg-sr-only">主要设备反馈及协议周期</caption><thead><tr><th scope="col">反馈来源</th><th scope="col">当前状态</th><th scope="col">数据来源</th></tr></thead><tbody>{rows.map(row => <tr key={row[2]}><th scope="row">{row[1]}</th><td>{row[3]}</td><td><Status tone="muted">{preview ? '示例数据' : '未接收'}</Status></td></tr>)}</tbody></table></div>
      <details className="fg-technical"><summary>协议明细</summary><p>三合一 CAN 协议 · 以下为协议定义周期，不是实测延迟。</p><dl className="fg-protocol-list">{rows.map(row => <div key={row[2]}><dt>{row[1]}</dt><dd>{row[2]} · {row[4]}</dd></div>)}</dl><p>8 字节标准帧，低字节在前；包含循环计数与异或校验。</p></details>
    </section>
  </>;
}
