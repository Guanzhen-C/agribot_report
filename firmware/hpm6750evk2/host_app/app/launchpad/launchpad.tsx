"use client";

import { AppIcon, consoleApps, ConsoleLink, Icon, number, Status, useConsole } from '../console-shell';
import { connectionLabel, useConnectionSupport } from '../connection-support';

export function Launchpad() {
  const { data, preview, connected } = useConsole();
  const support = useConnectionSupport();
  const alert = data.alerts[0];
  return <div className="fg-launchpad">
    <h1 className="fg-sr-only">工作空间</h1>
    <div className="fg-launch-grid">
      <section className="fg-workspace-widget fg-widget" aria-labelledby="fg-workspace-title">
        <div className="fg-workspace-emblem" aria-hidden="true"><span>F</span></div>
        <h2 id="fg-workspace-title">FieldGate <span>VCU</span></h2>
        <p>车辆控制与配置</p>
        <Status tone={preview ? 'warning' : 'muted'}>{connectionLabel(preview, connected, support)}</Status>
        <ConsoleLink className="fg-widget-link" href="/settings">设备设置 <Icon name="arrow" size={18}/></ConsoleLink>
      </section>

      <ConsoleLink href="/preview" className="fg-model-widget fg-widget">
        <div className="fg-widget-title"><div className="fg-widget-heading"><AppIcon icon="preview" color="preview" small/><div><h2>车辆预览</h2><p>阿克曼农机 · 装配模型</p></div></div><Icon name="arrow"/></div>
        <div className="fg-model-poster" role="img" aria-label="阿克曼农机装配模型"/>
        <div className="fg-widget-caption"><span>旋转、展开与部件选择</span><span className="fg-model-open"><Icon name="expand" size={16}/>打开三维视图</span></div>
      </ConsoleLink>

      <section className="fg-apps-widget fg-widget" aria-labelledby="fg-apps-title">
        <div className="fg-widget-title"><h2 id="fg-apps-title">应用</h2><Icon name="launchpad" size={18}/></div>
        <div className="fg-launch-apps">{consoleApps.map(([href,icon,name,color]) => <ConsoleLink key={href} href={href} className="fg-launch-app"><AppIcon icon={icon} color={color}/><span>{name}</span></ConsoleLink>)}</div>
      </section>

      <section className="fg-energy-widget fg-widget" aria-labelledby="fg-energy-title">
        <div className="fg-widget-title"><h2 id="fg-energy-title">动力电池</h2><Icon name="battery"/></div>
        <strong>{number(data.soc)}<small>%</small></strong>
        <div className="fg-desktop-battery" aria-hidden="true">{Array.from({length:12},(_,i)=><i key={i} className={data.soc !== null && i < data.soc * .12 ? 'filled' : ''}/>)}</div>
        <div className="fg-energy-detail"><span>{preview ? '示例数据' : '尚未获取电池状态'}</span><span>{number(data.voltage, 1)} V</span></div>
        <ConsoleLink className="fg-widget-link" href="/">打开总览 <Icon name="arrow" size={18}/></ConsoleLink>
      </section>

      <ConsoleLink className="fg-diagnostic-widget fg-widget" href="/diagnostics">
        <AppIcon icon="diagnostics" color="diagnostics" small/>
        <div><h2>设备诊断</h2><p>{preview ? alert?.title ?? '示例场景暂无告警' : '等待有效设备反馈'}</p></div>
        <span className="fg-diagnostic-action">查看诊断<Icon name="arrow" size={18}/></span>
      </ConsoleLink>
    </div>
  </div>;
}
