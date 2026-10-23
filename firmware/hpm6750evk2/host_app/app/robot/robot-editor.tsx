"use client";
import {useEffect,useRef,useState} from 'react';
import {ConsoleLink,Icon,useConsole} from '../console-shell';
import {jointSettings,partRole,robotLinks,roleNames,type PartRole,type RobotJoint,type RobotPart,type Vector3Tuple} from '../../lib/robot-description.mjs';
import type {useRobotDraft} from './use-robot-draft';

function download(name:string,bytes:Uint8Array){const url=URL.createObjectURL(new Blob([new Uint8Array(bytes).buffer],{type:'application/octet-stream'}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export function RobotEditor({state,part,parts,modelFile,busy,onBusy}:{state:ReturnType<typeof useRobotDraft>;part:RobotPart|null;parts:RobotPart[];modelFile:string;busy:boolean;onBusy:(value:boolean)=>void}){
  const {draft,dirty,ready,error,message,change,save,load,setError,setMessage}=state;
  const {setDraftDirty,setSaving}=useConsole();
  const [progress,setProgress]=useState(0);
  const input=useRef<HTMLInputElement>(null);
  const joint=part?jointSettings(part,draft):null;
  useEffect(()=>{
    setDraftDirty(dirty||busy);
    const guard=(e:BeforeUnloadEvent)=>{if(dirty||busy){e.preventDefault();e.returnValue='';}};
    window.addEventListener('beforeunload',guard);return()=>{window.removeEventListener('beforeunload',guard);setDraftDirty(false);};
  },[dirty,busy,setDraftDirty]);
  useEffect(()=>()=>setSaving(false),[setSaving]);
  function updateJoint(update:(value:RobotJoint)=>void){if(!part||!joint)return;const next=structuredClone(draft),value=structuredClone(joint);update(value);value.confirmed=false;next.joints[part.id]=value;change(next);}
  async function exportPackage(){
    onBusy(true);setSaving(true);setError('');setMessage('');setProgress(0);
    try{
      const {createRobotPackage}=await import('../../lib/urdf-export.mjs');
      const response=await fetch(`/models/${modelFile}.glb`);if(!response.ok)throw new Error('本机模型文件无法读取。');
      const result=await createRobotPackage(await response.arrayBuffer(),draft,parts,setProgress);
      download(draft.packageName+'.zip',result.archive);setMessage('URDF 包已生成');
    }catch(cause){setError(cause instanceof Error&&/[\u4e00-\u9fff]/.test(cause.message)?cause.message:'描述包生成失败，请检查模型资源后重试。');}
    finally{onBusy(false);setSaving(false);}
  }
  function vector(key:'xyz'|'rpy'|'axis',label:string,factor:number){return <fieldset className="fg-vector-fields"><legend>{label}</legend>{['X','Y','Z'].map((axis,i)=><label key={axis}>{axis}<input aria-label={`${label} ${axis}`} type="number" step="any" value={joint&&Number.isFinite(joint[key][i])?Number((joint[key][i]*factor).toFixed(6)):''} onChange={e=>updateJoint(j=>{j[key]=j[key].map((v,k)=>k===i?e.target.valueAsNumber/factor:v) as Vector3Tuple;})}/></label>)}</fieldset>;}
  return <aside className="fg-profile-editor fg-robot-editor" aria-label="机器人描述配置">
    <div className="fg-config-heading"><div><h2>机器人描述</h2><span>本机草稿</span></div><Icon name="cpu"/></div>
    <div className="fg-robot-export"><button className="fg-button fg-primary" aria-describedby="urdf-export-note" disabled={!ready||busy||!parts.length} onClick={()=>void exportPackage()}><Icon name="download"/>{busy?'正在生成…':'生成 URDF 包'}</button>{busy&&<progress aria-label="描述包生成进度" max="100" value={progress}/>}<p id="urdf-export-note">未配置运动的关节按固定安装导出；不含惯量与控制驱动。</p></div>
    <div className="fg-config-feedback" aria-live="polite">{error&&<p className="fg-config-error" role="alert">{error}</p>}{message&&<p className="fg-config-message" role="status">{message}</p>}</div>
    <ConsoleLink href="/preview" className="fg-widget-link">返回执行机构 <Icon name="arrow"/></ConsoleLink>
    <form onSubmit={e=>{e.preventDefault();save();}}>
      <fieldset disabled={!ready||busy} className="fg-robot-fields">
        {part&&joint?<><div className="fg-selected-part"><Icon name="preview"/><div><h3>{part.name}</h3><span>{roleNames[partRole(part,draft)]}</span></div></div>
          <label>部件用途<select aria-label="部件用途" value={partRole(part,draft)} onChange={e=>{const next=structuredClone(draft);next.roles[part.id]=e.target.value as PartRole;change(next);}}>{Object.entries(roleNames).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
          <details className="fg-joint-details"><summary>安装与运动关系</summary>
            <label>连杆名称<input aria-label="连杆名称" value={joint.name} maxLength={64} onChange={e=>updateJoint(j=>{j.name=e.target.value;})}/></label>
            <label>安装到<select aria-label="安装到" value={joint.parent} onChange={e=>updateJoint(j=>{j.parent=e.target.value;})}><option value="base_link">整机主体</option>{robotLinks(draft,parts).filter(p=>p.id!==part.id).map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
            <label>运动方式<select aria-label="运动方式" value={joint.type} onChange={e=>updateJoint(j=>{j.type=e.target.value as RobotJoint['type'];})}><option value="fixed">固定安装</option><option value="continuous">连续旋转</option><option value="revolute">限位旋转</option><option value="prismatic">直线伸缩</option></select></label>
            <p className="fg-muted-copy">坐标相对整机。默认原点为部件外观中心，请按实际轴心或光心校准。</p>
            {vector('xyz','位置 / mm',1000)}{vector('rpy','朝向 / °',180/Math.PI)}
            {joint.type!=='fixed'&&<>{vector('axis','运动轴',1)}<div className="fg-dimension-fields">{(['lower','upper','effort','velocity'] as const).filter(key=>joint.type!=='continuous'||!['lower','upper'].includes(key)).map(key=><label key={key}>{({lower:'下限',upper:'上限',effort:'最大力 / 力矩',velocity:'最大速度'})[key]}<input aria-label={key} type="number" step="any" value={joint[key]??''} onChange={e=>updateJoint(j=>{j[key]=e.target.value===''?null:e.target.valueAsNumber;})}/></label>)}</div><p className="fg-muted-copy">旋转使用 rad、N·m、rad/s；伸缩使用 m、N、m/s。请填写实机限值。</p></>}
            {(joint.optical||(partRole(part,draft)==='perception'&&/相机|摄像/.test(part.name)))&&<label className="fg-inline-check"><input type="checkbox" checked={joint.optical} onChange={e=>updateJoint(j=>{j.optical=e.target.checked;})}/>生成相机光学坐标系</label>}
            {(joint.type!=='fixed'||joint.optical)&&<label className="fg-inline-check"><input type="checkbox" checked={joint.confirmed} onChange={e=>{const next=structuredClone(draft);next.joints[part.id]={...joint,confirmed:e.target.checked};change(next);}}/>已核对安装坐标、运动轴与限值</label>}
          </details>
        </>:<div className="fg-select-prompt"><h3>选择部件以设置安装关系</h3></div>}
        <details className="fg-config-files"><summary>整机描述设置</summary><label>描述名称<input aria-label="描述名称" value={draft.name} maxLength={64} onChange={e=>change({...draft,name:e.target.value})}/></label><label>描述包名称<input aria-label="描述包名称" value={draft.packageName} maxLength={63} onChange={e=>change({...draft,packageName:e.target.value})}/></label><label>CAD 前向<select aria-label="CAD 前向" value={draft.forward} disabled={Object.keys(draft.joints).length>0} onChange={e=>change({...draft,forward:e.target.value})}>{['-y','+y','+x','-x'].map(v=><option key={v} value={v}>{v.toUpperCase()}</option>)}</select></label><p>请按实机确认前向。设置部件坐标后，前向将锁定。URDF 单位：m、rad。</p></details>
        <div className="fg-config-save"><button className="fg-button fg-primary" type="submit">保存描述</button>{dirty&&<span>未保存</span>}</div>
      </fieldset>
    </form>
    <details className="fg-config-files"><summary>描述备份</summary><div><button className="fg-button" disabled={busy} onClick={()=>download('robot-description.json',new TextEncoder().encode(JSON.stringify(draft,null,2)))}>导出描述</button><button className="fg-button" disabled={busy||!parts.length} onClick={()=>input.current?.click()}>导入描述</button><input ref={input} type="file" accept=".json" hidden aria-label="导入机器人描述" onChange={async e=>{const f=e.target.files?.[0];if(!f)return;try{if(f.size>128*1024)throw new Error('描述文件超过 128 KB。');if(dirty&&!confirm('当前描述尚未保存。要载入其他描述吗？'))return;load(await f.text());}catch{setError('描述无法读取或文件过大。');}finally{if(input.current)input.current.value='';}}}/></div></details>
  </aside>;
}
