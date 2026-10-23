"use client";

import { useState } from 'react';
import { ConsoleLink, Icon, PageHeading, PanelHeading, Status, number, useConsole } from './console-shell';

export function MachineDiagram() {
  const { data, preview } = useConsole();
  return <div className="fg-machine-diagram">
    <div className="fg-drive-label left"><span>左驱动</span><strong>{number(data.left.rpm)} <small>rpm</small></strong><span>{number(data.left.temperature)} °C</span></div>
    <svg viewBox="0 0 360 360" className="fg-machine-svg" role="img" aria-label={`农机设备示意：左驱动 ${number(data.left.rpm)} 转每分，右驱动 ${number(data.right.rpm)} 转每分，${data.running}`}>
      <path className="fg-diagram-guide" d="M180 12v28m0 278v28M12 180h27m282 0h27M53 59l10 10m244-10-10 10M53 301l10-10m244 10-10-10"/>
      <circle className="fg-diagram-guide" cx="180" cy="180" r="143"/>
      <path className="fg-diagram-guide" d="M180 40v278M39 180h282" strokeDasharray="3 7"/>
      <path className="fg-diagram-arrow" d="m173 30 7-8 7 8"/>
      <rect className="fg-track" x="79" y="82" width="48" height="206" rx="24"/><rect className="fg-track" x="233" y="82" width="48" height="206" rx="24"/>
      {Array.from({ length: 13 }, (_, i) => <g key={i} className="fg-tread"><path d={`M85 ${102 + i * 13}h36M239 ${102 + i * 13}h36`}/></g>)}
      <rect className="fg-machine-base" x="125" y="91" width="110" height="184" rx="12"/>
      <path className="fg-machine-top" d="M139 96h82l8 36v79h-98v-79Z"/>
      <rect className="fg-machine-service" x="141" y="219" width="78" height="41" rx="5"/>
      <path className="fg-machine-service-line" d="M150 231h60m-60 9h60m-60 9h60"/>
      <path className="fg-diagram-wire" d="M180 145v-38m0 107v29m-23-66h-36m82 0h37"/>
      <rect className={preview ? 'fg-vcu-module active' : 'fg-vcu-module'} x="151" y="149" width="58" height="57" rx="6"/>
      <text x="180" y="174" textAnchor="middle" className="fg-vcu-label">VCU</text><text x="180" y="190" textAnchor="middle" className="fg-vcu-sub">FieldGate</text>
      <rect className={preview && data.functions[3].value ? 'fg-lamp active' : 'fg-lamp'} x="137" y="86" width="20" height="7" rx="2"/><rect className={preview && data.functions[3].value ? 'fg-lamp active' : 'fg-lamp'} x="203" y="86" width="20" height="7" rx="2"/>
      <path className="fg-diagram-label-line" d="M79 184H22m259 0h57"/>
    </svg>
    <div className="fg-drive-label right"><span>右驱动</span><strong>{number(data.right.rpm)} <small>rpm</small></strong><span>{number(data.right.temperature)} °C</span></div>
    <span className="fg-diagram-caption">设备示意 · 非比例模型</span>
  </div>;
}

export function FunctionList() {
  const { data } = useConsole();
  return <div className="fg-function-list">{data.functions.map(item => <div className="fg-function" key={item.key}>
    <Icon name={item.key} size={23}/><div className="fg-function-info"><div><strong>{item.name}</strong><Status tone={item.tone}>{item.state}</Status></div>{item.detail && <p>{item.detail}</p>}</div>
    <span className="fg-function-value">{item.key === 'light' ? item.value === null ? '—' : item.value ? 'ON' : 'OFF' : number(item.value)}{item.unit && <small>{item.unit}</small>}</span>
  </div>)}</div>;
}

export function Overview() {
  const { preview, data, scenario } = useConsole();
  const [metric, setMetric] = useState<'voltage' | 'current'>('voltage');
  const values = metric === 'voltage' ? data.trend : data.trend.map((_, index) => scenario === 'standby' ? 0.8 : 24.8 + [0, 0.4, -0.5, 0.2, -0.2][index % 5]);
  const min = metric === 'voltage' ? (scenario === 'warning' ? 44 : 48) : 0;
  const max = metric === 'voltage' ? (scenario === 'warning' ? 52 : 56) : 32;
  const points = values.map((v, i) => `${40 + i / Math.max(values.length - 1, 1) * 590},${140 - (v - min) / (max - min) * 110}`).join(' ');
  return <>
    <PageHeading title="运行总览" description={preview ? '履带差速农机（三合一）' : undefined}><ConsoleLink href="/devices" className="fg-text-link">查看设备<Icon name="arrow" size={18}/></ConsoleLink></PageHeading>
    <section className="fg-metric-strip" aria-label="关键运行指标">
      <div className="fg-metric fg-energy-metric"><span><Icon name="battery" size={18}/>剩余电量</span><strong>{number(data.soc, data.soc !== null && data.soc % 1 ? 1 : 0)}<small>%</small></strong><div className="fg-battery-bars" aria-hidden="true">{Array.from({length:20}, (_, i) => <i key={i} className={data.soc !== null && i < data.soc / 5 ? 'is-filled' : ''}/>)}</div></div>
      <div className="fg-metric"><span>电池电压</span><strong>{number(data.voltage, 1)}<small>V</small></strong><span className="fg-metric-note">{preview ? '电池组反馈' : '等待反馈'}</span></div>
      <div className="fg-metric"><span>作业模式</span><strong className="fg-metric-word">{data.mode}</strong><Status tone={data.tone}>{data.running}</Status></div>
      <div className="fg-metric"><span>安全状态</span><strong className="fg-metric-word">{preview ? `急停${data.emergency}` : '未获取'}</strong><Status tone={preview ? data.alerts.length ? 'warning' : 'good' : 'muted'}>{preview ? data.alerts.length ? `${data.alerts.length} 项待处理` : '当前无告警' : '等待反馈'}</Status></div>
    </section>

    <div className="fg-overview-grid">
      <section className="fg-panel fg-machine-panel"><PanelHeading title="农机状态"><Status tone={data.tone}>{data.running}</Status></PanelHeading><MachineDiagram/>
        <div className="fg-machine-bottom"><div><span>左驱动电流</span><strong>{number(data.left.current, 1)} <small>A</small></strong></div><div><span>右驱动电流</span><strong>{number(data.right.current, 1)} <small>A</small></strong></div><div><span>控制来源</span><strong className="fg-source-label">{preview ? scenario === 'standby' ? '遥控器' : '算力模块' : '未获取'}</strong></div></div>
      </section>
      <section className="fg-panel fg-functions-panel"><PanelHeading title="作业功能"><ConsoleLink href="/devices" className="fg-text-link">详情<Icon name="chevron" size={16}/></ConsoleLink></PanelHeading><FunctionList/>
        <div className={`fg-operation-note ${data.alerts.length ? 'has-warning' : ''}`}><Icon name={data.alerts.length ? 'warning' : preview ? 'check' : 'link'}/><div><strong>{data.alerts[0]?.title ?? (preview ? scenario === 'standby' ? '设备处于待机状态' : '植保作业进行中' : '等待设备连接')}</strong>{data.alerts.length > 0 && <p>停车后检查液位并补充药液。</p>}</div>{data.alerts.length > 0 && <ConsoleLink href="/diagnostics" className="fg-text-link">查看</ConsoleLink>}</div>
      </section>
      <section className="fg-panel fg-trend-panel"><PanelHeading title="能量趋势"><div className="fg-segment" role="group" aria-label="趋势指标"><button aria-pressed={metric === 'voltage'} onClick={() => setMetric('voltage')}>电压</button><button aria-pressed={metric === 'current'} onClick={() => setMetric('current')}>电流</button></div></PanelHeading>
        <div className="fg-trend-summary"><strong>{number(metric === 'voltage' ? data.voltage : data.current, 1)}<small>{metric === 'voltage' ? 'V' : 'A'}</small></strong><span>{preview ? '示例曲线 · 60 秒' : '尚无运行数据'}</span></div>
        <div className="fg-chart-area"><div className="fg-chart-y" aria-hidden="true">{[max, (max+min)/2, min].map(value => <span key={value}>{value}</span>)}</div><svg viewBox="36 25 605 120" preserveAspectRatio="none" className="fg-chart" role="img" aria-label={preview ? `${metric === 'voltage' ? '电压' : '电流'}预览趋势，非实机历史记录` : '等待设备数据，无趋势记录'}>
          {[0,1,2].map(i => <path key={i} className="fg-chart-grid" d={`M40 ${30+i*55}H635`}/>)}
          {values.length > 0 && <><polygon className="fg-chart-fill" points={`40,140 ${points} 630,140`}/><polyline className="fg-chart-line" points={points}/><circle className="fg-chart-dot" cx="630" cy={140-(values[values.length-1]-min)/(max-min)*110} r="4"/></>}
        </svg><div className="fg-chart-x" aria-hidden="true"><span>60 秒前</span><span>30 秒前</span><span>当前</span></div></div>
      </section>
      <section className="fg-panel fg-link-panel"><PanelHeading title="连接链路"><ConsoleLink href="/diagnostics" className="fg-text-link">诊断<Icon name="chevron" size={16}/></ConsoleLink></PanelHeading>
        <div className="fg-link-sequence">{[['算力模块','控制输入'],['FieldGate VCU','信号转换'],['执行器','状态反馈']].map(([name, detail], i) => <div key={name}><span className={preview ? 'fg-link-node is-active' : 'fg-link-node'}><Icon name={i === 1 ? 'cpu' : 'devices'}/></span><div><strong>{name}</strong><span>{detail}</span></div><Status tone={preview ? 'good' : 'muted'}>{preview ? '正常' : '未获取'}</Status></div>)}</div>
      </section>
    </div>
  </>;
}
