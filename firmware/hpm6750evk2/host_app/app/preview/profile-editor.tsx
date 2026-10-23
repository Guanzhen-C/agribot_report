"use client";

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { bindModelPart, createProfileDraft, decodeDrafts, displayName, DRAFT_STORAGE_KEY, modelBindings, profileTemplates, saveDraft, validateWorkspaceProfile, type MachineProfile } from '../../lib/profile-workspace.mjs';
import { compileMachineProfile, readMachineProfilePackage } from '../../lib/machine-profile.mjs';
import { ConsoleLink, Icon, useConsole } from '../console-shell';
import {isActuator,type PartRole} from '../../lib/robot-description.mjs';
import type { ModelPart } from './vehicle-model';

const initialProfile = () => structuredClone(profileTemplates[0].profile);
export function ProfileEditor({modelId,part,role,parts,onBindings}:{modelId:string;part:ModelPart|null;role?:PartRole;parts:ModelPart[];onBindings:(bindings:Record<string,string>)=>void}) {
  const {setDraftDirty} = useConsole();
  const [records,setRecords]=useState<MachineProfile[]>([]);
  const [profile,setProfile]=useState<MachineProfile>(initialProfile);
  const [dirty,setDirty]=useState(false);
  const [ready,setReady]=useState(false);
  const [tab,setTab]=useState('part');
  const [message,setMessage]=useState('');
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const [removed,setRemoved]=useState<MachineProfile|null>(null);
  const dialog=useRef<HTMLDialogElement>(null);
  const input=useRef<HTMLInputElement>(null);
  useEffect(()=>{
    // eslint-disable-next-line react-hooks/set-state-in-effect -- Hydrate browser-only storage after SSR; never write during hydration.
    try { const list=decodeDrafts(localStorage.getItem(DRAFT_STORAGE_KEY));setRecords(list);if(list[0])setProfile(list[0]);setReady(true); }
    catch { setError('本机草稿无法读取。为保护已有内容，暂不覆盖保存；可先导出当前配置。'); }
  },[]);
  useEffect(()=>{
    const bindings=modelBindings(profile,modelId);
    onBindings(Object.fromEntries(Object.entries(bindings).map(([id,deviceId])=>[id,profile.devices.find(d=>d.id===deviceId)?.name??'未关联'])));
  },[profile,modelId,onBindings]);
  useEffect(()=>{
    setDraftDirty(dirty);
    const guard=(event:BeforeUnloadEvent)=>{if(dirty){event.preventDefault();event.returnValue='';}};
    window.addEventListener('beforeunload',guard);
    return ()=>{window.removeEventListener('beforeunload',guard);setDraftDirty(false);};
  },[dirty,setDraftDirty]);
  const boundId=part ? modelBindings(profile,modelId)[part.id]??'' : '';
  const device=profile.devices.find(item=>item.id===boundId);
  function change(next:MachineProfile) { next.identity.commissioned=false;setProfile(next);setDirty(true);setMessage('');setError(''); }
  function edit(update:(next:MachineProfile)=>void) { const next=structuredClone(profile);update(next);change(next); }
  function switching() { return !dirty || window.confirm('当前草稿有未保存的修改。要放弃这些修改吗？'); }
  function persist(list:MachineProfile[]) {
    try {localStorage.setItem(DRAFT_STORAGE_KEY,JSON.stringify(list));}
    catch {throw new Error('未能保存到本机，请检查存储空间或权限。可先导出配置备份。');}
    setRecords(list);
  }
  function save(event:FormEvent) {
    event.preventDefault();setError('');
    try {
      const next=structuredClone(profile);next.identity.name=displayName(next.identity.name);
      const list=saveDraft(records,next);persist(list);setProfile(list[0]);setDirty(false);setMessage('草稿已保存在本机');
    } catch(cause) {setError(cause instanceof Error?cause.message:'未能保存草稿，请导出备份后重试。');}
  }
  function create(event:FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form=new FormData(event.currentTarget);
    try {
      const next=createProfileDraft(String(form.get('template')),String(form.get('name')),crypto.randomUUID());
      if(!switching())return;
      const list=saveDraft(records,next);persist(list);setProfile(list[0]);setDirty(false);setMessage('新农机草稿已创建');setError('');dialog.current?.close();setTab('machine');
    } catch(cause) {setError(cause instanceof Error?cause.message:'未能新建草稿，请检查本机存储空间。');}
  }
  async function importProfile(file:File|undefined) {
    if(!file)return;
    setBusy(true);setError('');
    try {
      if(file.size>65536)throw new Error('文件过大，请选择不超过 64 KB 的车辆配置。');
      const value:MachineProfile=file.name.toLowerCase().endsWith('.fgmp')
        ? readMachineProfilePackage(new Uint8Array(await file.arrayBuffer()))
        : JSON.parse(await file.text());
      validateWorkspaceProfile(value);
      const bindings=modelBindings(value,modelId);
      if(parts.length && Object.keys(bindings).some(id=>!parts.some(p=>p.id===id)))throw new Error('配置关联了此模型中不存在的部件。');
      if(!switching())return;
      value.identity.id=crypto.randomUUID();value.identity.revision=1;value.identity.commissioned=false;
      const list=saveDraft(records,value);persist(list);setProfile(list[0]);setDirty(false);setMessage('已导入为新草稿，原文件未修改');
    } catch(cause){setError(cause instanceof Error?cause.message:'文件无法读取，请检查配置文件。');}
    finally {setBusy(false);if(input.current)input.current.value='';}
  }
  function exportProfile(binary:boolean) {
    setError('');
    try {
      const next=structuredClone(profile);next.identity.commissioned=false;validateWorkspaceProfile(next);
      const source=binary?compileMachineProfile(next):new TextEncoder().encode(JSON.stringify(next,null,2));
      const bytes=new Uint8Array(source).buffer;
      const url=URL.createObjectURL(new Blob([bytes],{type:binary?'application/octet-stream':'application/json'}));
      const anchor=document.createElement('a');anchor.href=url;anchor.download=`${displayName(next.identity.name).replace(/[<>:"/\\|?*]/g,'_')}.${binary?'fgmp':'json'}`;
      document.body.appendChild(anchor);anchor.click();anchor.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);setMessage('已导出配置草稿，尚未写入设备');
    } catch(cause){setError(cause instanceof Error?cause.message:'配置未通过检查，无法导出。');}
  }
  function remove() {
    const previous=records.find(item=>item.identity.id===profile.identity.id);if(!previous || !switching())return;
    try {const list=records.filter(p=>p.identity.id!==previous.identity.id);persist(list);setRemoved(previous);setProfile(list[0]??initialProfile());setDirty(false);setMessage('已删除本机草稿，可以撤销');setError('');}
    catch {setError('删除未完成，请检查本机存储权限。');}
  }
  function undo() {
    if(!removed)return;
    try {const list=saveDraft(records,removed);persist(list);setRemoved(null);setMessage('已恢复草稿');}
    catch {setError('恢复未完成，请先导出已有草稿。');}
  }
  return <aside className="fg-profile-editor" aria-label="车辆配置">
    <div className="fg-config-heading"><div><h2>车辆配置</h2><span>本机草稿 · 未写入设备</span></div><Icon name="settings"/></div>
    <div className="fg-config-library"><label>当前农机<select aria-label="当前农机草稿" value={records.some(r=>r.identity.id===profile.identity.id)?profile.identity.id:''} disabled={busy} onChange={e=>{const next=records.find(r=>r.identity.id===e.target.value);if(next && switching()){setProfile(structuredClone(next));setDirty(false);setError('');setMessage('');}}}>{!records.some(r=>r.identity.id===profile.identity.id)&&<option value="">阿克曼四灯农机 · 示例</option>}{records.map(item=><option value={item.identity.id} key={item.identity.id}>{item.identity.name}</option>)}</select></label><div className="fg-config-library-actions"><button className="fg-button" disabled={!ready||busy} onClick={()=>{setError('');dialog.current?.showModal();}}>新建农机</button><button className="fg-button fg-quiet" disabled={!ready||busy} onClick={()=>input.current?.click()}>{busy?'正在导入…':'导入配置'}</button><input ref={input} type="file" accept=".json,.fgmp" hidden aria-label="导入车辆配置文件" onChange={e=>void importProfile(e.target.files?.[0])}/></div></div>
    <div className="fg-config-tabs" role="group" aria-label="配置内容"><button aria-pressed={tab==='part'} onClick={()=>setTab('part')}>部件配置</button><button aria-pressed={tab==='machine'} onClick={()=>setTab('machine')}>整机参数</button></div>
    <form onSubmit={save}>
      {tab==='part'?<div className="fg-part-config">
        {part&&role!=='actuator'?<div className="fg-select-prompt"><Icon name={role==='perception'?'cpu':'preview'} size={36}/><h3>{part.name}</h3><p>{role==='perception'?'算力与感知':'结构件'}</p>{boundId&&<p>已关联：{device?.name??boundId}</p>}<ConsoleLink href="/robot" className="fg-button">安装与用途管理</ConsoleLink></div>:part?<><div className="fg-selected-part"><Icon name="preview"/><div><h3>{part.name}</h3></div></div>
          <label>关联功能<select aria-label="关联功能" value={boundId} onChange={e=>{try{change(bindModelPart(profile,part.id,e.target.value,modelId));}catch(cause){setError(cause instanceof Error?cause.message:'关联失败，请重试。');}}}><option value="">不关联</option>{device&&!isActuator(device)&&<option value={device.id}>{device.name} · 原有关联</option>}{profile.devices.filter(isActuator).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          {device&&<><label>部件显示名称<input aria-label="部件显示名称" value={device.name} maxLength={64} required onChange={e=>edit(next=>{next.devices.find(d=>d.id===device.id)!.name=e.target.value;})}/></label><div className="fg-device-summary"><span>控制方式</span><strong>{device.category==='light'?'跟随作业照明':device.category==='drive'?'行走控制':device.category==='steering'?'转向控制':device.control==='none'?'状态反馈':'作业控制'}</strong><span>状态来源</span><strong>{device.feedback==='none'?'尚未配置反馈':'原三合一反馈'}</strong></div>{device.category==='light'&&<p className="fg-muted-copy">四路灯光由同一开关控制。</p>}</>}</>:null}
        {!part&&<div className="fg-profile-devices"><h3>当前执行功能</h3>{profile.devices.filter(isActuator).map(item=><span key={item.id}>{item.name}</span>)}</div>}
      </div>:<div className="fg-machine-config">
        <label>农机名称<input aria-label="农机名称" required maxLength={64} value={profile.identity.name} onChange={e=>edit(next=>{next.identity.name=e.target.value;})}/></label>
        <div className="fg-device-summary"><span>车辆结构</span><strong>{profile.machine.type==='ackermann'?'阿克曼':profile.machine.type==='differential'?'轮式差速':'履带差速'}</strong><span>算力模块</span><strong>三合一 CAN 协议</strong><span>配置作者</span><strong>{profile.identity.author}</strong></div>
        {profile.machine.geometry&&<><p className="fg-muted-copy">请填写实机标定值。</p><div className="fg-dimension-fields">
          {([['wheelbaseMm','轴距'],['trackMm','轮距']] as const).map(([key,label])=><label key={key}>{label} / mm<input aria-label={`${label}（毫米）`} type="number" min="100" max="10000" step="1" required value={profile.machine.geometry![key]} onChange={e=>edit(next=>{next.machine.geometry![key]=e.target.valueAsNumber;})}/></label>)}
        </div></>}
        {profile.machine.calibration&&<label>满量程轮速 / mm/s<input aria-label="满量程轮速" type="number" min="1" max="10000" step="1" required value={profile.machine.calibration.wheelSpeedAt100PercentMmS} onChange={e=>edit(next=>{next.machine.calibration!.wheelSpeedAt100PercentMmS=e.target.valueAsNumber;})}/></label>}
        <div className="fg-profile-devices"><h3>已配置执行功能</h3>{profile.devices.filter(isActuator).map(item=><span key={item.id}>{item.name}</span>)}</div>
      </div>}
      <div className="fg-config-save"><button type="submit" className="fg-button fg-primary" disabled={!ready||busy}><Icon name="check"/>保存草稿</button><span>{dirty?'未保存':records.some(r=>r.identity.id===profile.identity.id)?`已保存 · 第 ${profile.identity.revision} 版`:'示例配置'}</span></div>
    </form>
    {error&&<p className="fg-config-error" role="alert"><Icon name="warning" size={18}/>{error}</p>}
    {message&&<p className="fg-config-message" role="status">{message}{removed&&<button className="fg-text-link" onClick={undo}>撤销删除</button>}</p>}
    <details className="fg-config-files"><summary>配置文件与管理</summary><p>配置包默认禁止输出，需完成实机验证后启用。</p><div><button className="fg-button" onClick={()=>exportProfile(false)}>导出描述</button><button className="fg-button" onClick={()=>exportProfile(true)}>生成配置包</button><button className="fg-button fg-quiet" disabled={!ready||!records.some(r=>r.identity.id===profile.identity.id)} onClick={remove}>删除草稿</button></div></details>
    <dialog ref={dialog} className="fg-new-profile-dialog" onClick={e=>{if(e.target===dialog.current)dialog.current.close();}}><form onSubmit={create}><div className="fg-dialog-heading"><h2>新建农机配置</h2><button type="button" className="fg-button fg-quiet" aria-label="关闭新建配置" onClick={()=>dialog.current?.close()}><Icon name="close"/></button></div><label>农机名称<input name="name" required maxLength={64} defaultValue="新建农机" autoFocus/></label><label>结构与功能模板<select name="template">{profileTemplates.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>{error&&<p className="fg-config-error" role="alert">{error}</p>}<button className="fg-button fg-primary" type="submit">创建配置</button></form></dialog>
  </aside>;
}
