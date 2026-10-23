"use client";

import { useCallback, useState } from 'react';
import { ConsoleLink, Icon, PageHeading, useConsole } from '../console-shell';
import { VehicleModel, type ModelPart } from './vehicle-model';
import { ProfileEditor } from './profile-editor';
import { modelDefinitions } from '../../lib/profile-workspace.mjs';
import {partRole,roleNames} from '../../lib/robot-description.mjs';
import {useRobotDraft} from '../robot/use-robot-draft';
import {RobotEditor} from '../robot/robot-editor';
import {ModelImportWorkspace} from './model-import-workspace';

export function VehiclePreview({workspace='actuators'}:{workspace?:'actuators'|'robot'}) {
  const [importMode,setImportMode]=useState(false);
  const {draftDirty}=useConsole();
  const [parts, setParts] = useState<ModelPart[]>([]);
  const [selected, setSelected] = useState('');
  const [explode, setExplode] = useState(0);
  const [isolate, setIsolate] = useState(false);
  const [view, setView] = useState({ name: 'perspective', revision: 0 });
  const [search, setSearch] = useState('');
  const [bindings, setBindings] = useState<Record<string, string>>({});
  const [ready, setReady] = useState(false);
  const [exporting,setExporting]=useState(false);
  const [modelIndex, setModelIndex] = useState(0);
  const [scope,setScope]=useState(workspace==='robot'?'perception':'actuator');
  const model=modelDefinitions[modelIndex];
  const robot=useRobotDraft(model.id,parts);
  const onParts = useCallback((items: ModelPart[]) => { setParts(items); }, []);
  const selectedPart = parts.find(item => item.id === selected);
  const selectable=parts.filter(item=>scope==='all'||partRole(item,robot.draft)===scope);
  const visibleParts = selectable.filter(item => (item.name + ' ' + (bindings[item.id] ?? '')).includes(search));
  function select(id: string) { setSelected(id); }
  if(importMode)return <ModelImportWorkspace onClose={()=>setImportMode(false)}/>;
  return <>
    <div className="fg-import-entry"><button className="fg-button" onClick={()=>{if(draftDirty&&!confirm('当前配置尚未保存。要进入模型导入吗？'))return;setImportMode(true);}}>导入模型 / 调尺寸</button></div>
    <PageHeading title={workspace==='robot'?'算力与感知':'车辆预览'}><label className="fg-model-picker">外观模型<select aria-label="外观模型" value={modelIndex} disabled={exporting} onChange={event=>{if(workspace==='robot'&&robot.dirty&&!confirm('机器人描述有未保存的修改。要切换模型吗？'))return;setModelIndex(Number(event.target.value));setSelected('');setParts([]);setReady(false);setIsolate(false);setExplode(0);}}>{modelDefinitions.map((model,index)=><option key={model.id} value={index}>{model.name}</option>)}</select></label></PageHeading>
    <div className="fg-model-scope"><label>查看对象<select aria-label="查看对象" value={scope} onChange={e=>{setScope(e.target.value);setSelected('');setIsolate(false);}}>{workspace==='robot'?Object.entries(roleNames).map(([key,name])=><option key={key} value={key}>{name}</option>):<option value="actuator">执行机构</option>}<option value="all">全部外观</option></select></label><span>{parts.length?`${selectable.length} 个可选部件`:'正在载入'}</span><ConsoleLink href={workspace==='robot'?'/preview':'/robot'} className="fg-button fg-quiet"><Icon name={workspace==='robot'?'settings':'cpu'}/>{workspace==='robot'?'执行机构配置':'算力与感知'}</ConsoleLink></div>
    <div className="fg-visual-workspace">
      <section className="fg-model-area" aria-label="三维装配预览">
        <div className="fg-model-actions"><div className="fg-view-segments" role="group" aria-label="模型视角">{[['perspective','立体'],['top','俯视'],['side','侧视']].map(([key,label])=><button key={key} className={view.name===key?'is-active':''} aria-pressed={view.name===key} onClick={()=>setView({name:key,revision:view.revision+1})}>{label}</button>)}</div><button className="fg-button fg-quiet" disabled={!ready} onClick={()=>{setView({name:'perspective',revision:view.revision+1});setExplode(0);setIsolate(false);setSelected('');}}><Icon name="reset"/>复位</button></div>
        <VehicleModel modelFile={model.file} selected={selected} explode={explode} isolate={isolate} view={view} onParts={onParts} onSelect={select} onReady={setReady} selectableIds={selectable.map(p=>p.id)}/>
        <div className="fg-model-hint"><Icon name="eye" size={16}/><span>拖动旋转 · 双指缩放 · 点选部件</span></div>
        <div className="fg-explode-control"><button className={`fg-button ${explode ? 'fg-primary' : ''}`} disabled={!ready} onClick={()=>setExplode(explode?0:65)}><Icon name="preview"/>{explode ? '恢复装配' : '爆炸展开'}</button><label><span>展开程度 <output>{explode}%</output></span><input aria-label="展开程度" type="range" min="0" max="100" value={explode} disabled={!ready} onChange={e=>setExplode(Number(e.target.value))}/></label><button className="fg-button" disabled={!selected} aria-pressed={isolate} onClick={()=>setIsolate(!isolate)}><Icon name="eye"/>{isolate ? '显示全部' : '单独查看'}</button></div>
        <details className="fg-parts-browser" open><summary>部件目录 <span>{parts.length ? `${selectable.length} 个可选部件` : '正在载入'}</span></summary><label className="fg-part-search">查找部件<input type="search" value={search} onChange={e=>setSearch(e.target.value)} placeholder="名称或已配置功能"/></label><div className="fg-part-list" aria-label="可选部件">{visibleParts.map(part=><button key={part.id} aria-pressed={part.id===selected} onClick={()=>select(part.id)}><span>{part.name}</span>{workspace==='actuators'&&partRole(part,robot.draft)==='actuator'?(bindings[part.id]&&<small>{bindings[part.id]}</small>):<small>{roleNames[partRole(part,robot.draft)]}</small>}</button>)}{parts.length>0 && !visibleParts.length && <p className="fg-muted-copy">未找到部件，请调整筛选或搜索条件。</p>}</div></details>
      </section>
      {workspace==='robot'?<RobotEditor state={robot} modelFile={model.file} part={selectedPart??null} parts={parts} busy={exporting} onBusy={setExporting}/>:<ProfileEditor modelId={model.id} part={selectedPart ?? null} role={selectedPart?partRole(selectedPart,robot.draft):undefined} parts={parts} onBindings={setBindings}/>}
    </div>
  </>;
}
