"use client";

import { ConsoleLink, Icon, PageHeading, PanelHeading, Status, number, useConsole } from '../console-shell';
import { FunctionList, MachineDiagram } from '../overview';

export function Devices() {
  const { data, preview } = useConsole();
  return <>
    <PageHeading title="设备与功能"><ConsoleLink href="/settings" className="fg-text-link">设备设置<Icon name="arrow" size={18}/></ConsoleLink></PageHeading>
    <div className="fg-device-layout">
      <section className="fg-panel fg-equipment-profile">
        <PanelHeading title="当前农机"><Status tone={preview ? 'good' : 'muted'}>{preview ? '示例设备' : '待识别'}</Status></PanelHeading>
        <div className="fg-equipment-name"><h2>{preview ? '履带差速农机' : '尚未读取设备'}</h2>{preview && <p>三合一 · 左右独立驱动</p>}</div>
        <MachineDiagram/>
        <dl className="fg-facts"><div><dt>车辆控制器</dt><dd>FieldGate VCU</dd></div><div><dt>工作模式</dt><dd>{data.mode}</dd></div><div><dt>执行器协议</dt><dd>{preview ? '三合一 CAN 协议' : '未读取'}</dd></div></dl>
      </section>
      <div className="fg-device-details">
        <section className="fg-panel"><PanelHeading title="行走系统"><Status tone={data.tone}>{data.running}</Status></PanelHeading>
          <div className="fg-table-wrap"><table className="fg-table"><caption className="fg-sr-only">左右驱动状态</caption><thead><tr><th scope="col">设备</th><th scope="col">转速 / rpm</th><th scope="col">电流 / A</th><th scope="col">温度 / °C</th></tr></thead><tbody>{[['左驱动', data.left], ['右驱动', data.right]].map(([name, drive]) => typeof drive !== 'string' && <tr key={String(name)}><th scope="row">{String(name)}<Status tone={drive.faults === 0 ? 'good' : 'muted'}>{drive.faults === 0 ? '无故障' : '未获取'}</Status></th><td>{number(drive.rpm)}</td><td>{number(drive.current, 1)}</td><td>{number(drive.temperature)}</td></tr>)}</tbody></table></div>
        </section>
        <section className="fg-panel"><PanelHeading title="作业设备"/><FunctionList/></section>
      </div>
    </div>
    <div className="fg-bottom-note"><Icon name="help"/><p>更换农机或执行器后，请核对设备设置。</p><ConsoleLink href="/diagnostics" className="fg-text-link">查看诊断<Icon name="arrow" size={18}/></ConsoleLink></div>
  </>;
}
