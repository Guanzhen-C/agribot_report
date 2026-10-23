"use client";

import {useCallback,useEffect,useMemo,useRef,useState} from 'react';
import {Icon,PageHeading,useConsole} from '../console-shell';
import {VehicleModel} from './vehicle-model';
import {emptyTuning,disposeModel,tuningTransforms,wheelSlots,type ModelAsset,type ModelTuning,type WheelSlot} from '../../lib/model-tuning.mjs';
import {type ModelBundle} from '../../lib/model-import.mjs';

const slotNames:Record<WheelSlot,string>={frontLeft:'前左轮',frontRight:'前右轮',rearLeft:'后左轮',rearRight:'后右轮'};
const dimensions=[['trackMm','轮距'],['wheelbaseMm','轴距'],['widthMm','轮宽'],['radiusMm','轮胎半径']] as const;
function download(name:string,data:ArrayBuffer|string){const url=URL.createObjectURL(new Blob([data]));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}

export function ModelImportWorkspace({onClose}:{onClose:()=>void}){
  const {setDraftDirty}=useConsole();
  const [asset,setAsset]=useState<ModelAsset|null>(null),[bundle,setBundle]=useState<ModelBundle|null>(null);
  const [pendingBundle,setPendingBundle]=useState<ModelBundle|null>(null),[entryChoice,setEntryChoice]=useState(''),[argumentText,setArgumentText]=useState('');
  const [loadedArguments,setLoadedArguments]=useState<Record<string,string>>({});
  const [tuning,setTuning]=useState<ModelTuning>(emptyTuning),[applied,setApplied]=useState<ModelTuning>(emptyTuning);
  const [up,setUp]=useState('y'),[forward,setForward]=useState('z'),[unit,setUnit]=useState(1);
  const [sourceCoordinates,setSourceCoordinates]=useState({up:'y',forward:'z',unit:1});
  const [busy,setBusy]=useState(false),[ready,setReady]=useState(false),[error,setError]=useState(''),[validation,setValidation]=useState(''),[message,setMessage]=useState('');
  const [selected,setSelected]=useState(''),[explode,setExplode]=useState(0),[isolate,setIsolate]=useState(false),[search,setSearch]=useState('');
  const [view,setView]=useState({name:'perspective',revision:0});
  const file=useRef<HTMLInputElement>(null),alive=useRef(true);
  useEffect(()=>{alive.current=true;return()=>{alive.current=false;};},[]);
  useEffect(()=>()=>{if(asset)disposeModel(asset.scene);},[asset]);
  useEffect(()=>{setDraftDirty(!!asset);return()=>setDraftDirty(false);},[asset,setDraftDirty]);
  useEffect(()=>{if(!asset)return;const warn=(e:BeforeUnloadEvent)=>{e.preventDefault();};window.addEventListener('beforeunload',warn);return()=>window.removeEventListener('beforeunload',warn);},[asset]);
  const onParts=useCallback(()=>{},[]);
  function update(next:ModelTuning){setTuning(next);setMessage('');try{if(asset)tuningTransforms(asset.parts,next);setApplied(next);setValidation('');}catch(cause){setValidation(cause instanceof Error?cause.message:'参数无效。');}}
  async function load(next:ModelBundle,settings:{up:string;forward:string;unit:number},args:Record<string,string>={}){
    const {importModel}=await import('../../lib/model-import.mjs');
    const model=await importModel(next,settings,args);
    if(!alive.current){disposeModel(model.scene);return;}
    setAsset(model);setBundle(next);setLoadedArguments(args);setSourceCoordinates(settings);setUp(settings.up);setForward(settings.forward);setUnit(settings.unit);setSelected('');setExplode(0);setIsolate(false);setReady(false);setTuning(emptyTuning());setApplied(emptyTuning());setValidation('');setMessage(`已导入 ${model.parts.length} 个部件`);
  }
  async function importFiles(files:File[]){
    if(!files.length)return;
    if(asset&&!confirm('导入新模型会清除当前尺寸调整。继续前请先导出。'))return;
    setBusy(true);setError('');
    try{const {readModelFiles}=await import('../../lib/model-import.mjs');const next=await readModelFiles(files);setPendingBundle(next);setEntryChoice(next.entry);setArgumentText('');if(!next.entry){setMessage('资源已读取，请选择 Xacro 主文件并载入。');return;}const urdf=/\.(urdf|xacro|xcaro)$/i.test(next.entry);await load(next,{up:urdf?'z':'y',forward:urdf?'x':'z',unit:1});}
    catch(cause){setError(cause instanceof Error?cause.message:'模型导入失败，原模型未替换。');}
    finally{setBusy(false);if(file.current)file.current.value='';}
  }
  async function loadBuiltin(fileName:string){
    if(asset&&!confirm('载入内置模型会清除当前调整。是否继续？'))return;
    setBusy(true);setError('');
    try{const response=await fetch(`/models/${fileName}.glb`);if(!response.ok)throw new Error('内置模型无法读取。');const entry=fileName+'.glb';await load({entry,entries:new Map([[entry,new Uint8Array(await response.arrayBuffer())]])},{up:'z',forward:'-y',unit:1});setPendingBundle(null);setArgumentText('');}
    catch(cause){setError(cause instanceof Error?cause.message:'模型无法读取。');}finally{setBusy(false);}
  }
  async function exportModel(){
    if(!asset)return;setBusy(true);setError('');
    try{const {exportTunedModel}=await import('../../lib/model-import.mjs');const bytes=await exportTunedModel(asset,applied);download(asset.name.replace(/\.[^.]+$/,'')+'-adjusted.glb',bytes);setMessage('已导出调整后的 GLB 模型');}
    catch(cause){setError(cause instanceof Error?cause.message:'导出失败，请重试。');}finally{setBusy(false);}
  }
  const parts=asset?.parts??[],shown=parts.filter(p=>p.name.toLowerCase().includes(search.toLowerCase()));
  const xacroFiles=pendingBundle&&(pendingBundle.candidates||[pendingBundle.entry]);
  const hasXacro=useMemo(()=>pendingBundle&&[...pendingBundle.entries].some(([name,data])=>/\.(xacro|xcaro)$/i.test(name)||(/\.urdf$/i.test(name)&&/<\/?xacro:|\$\{/.test(new TextDecoder().decode(data)))),[pendingBundle]);
  return <>
    <PageHeading title="模型导入与尺寸"><button className="fg-button" disabled={busy} onClick={()=>{if(!asset||confirm('返回后将清除当前模型工作区。请先导出需要保留的结果。'))onClose();}}>返回车辆配置</button></PageHeading>
    <div className="fg-import-toolbar"><button className="fg-button fg-primary" disabled={busy} onClick={()=>file.current?.click()}><Icon name="download"/>{busy?'正在处理…':'导入模型'}</button><input ref={file} type="file" multiple hidden accept=".glb,.gltf,.urdf,.xacro,.xcaro,.zip,.bin,.stl,.dae,.png,.jpg,.jpeg,.webp" aria-label="导入三维模型文件" onChange={e=>void importFiles(Array.from(e.target.files??[]))}/><button className="fg-button" disabled={busy} onClick={()=>void loadBuiltin('ackermann')}>使用标准装配</button><button className="fg-button" disabled={busy} onClick={()=>void loadBuiltin('ackermann-flagship')}>使用扩展装配</button><span>{asset?.name??'GLB · glTF · URDF · Xacro · ZIP'}</span></div>
    <p className="fg-muted-copy">Xacro、URDF、glTF 的引用文件、网格和贴图请一起打包 ZIP，或同时选取配套文件。模型仅在本页处理，离开前请导出。单次最多 160 MB。</p>
    {hasXacro&&pendingBundle&&<details className="fg-config-files fg-xacro-options" open><summary>Xacro 入口与参数</summary><form onSubmit={async e=>{e.preventDefault();if(!entryChoice)return;if(asset&&!confirm('重新展开 Xacro 会清除当前尺寸调整，是否继续？'))return;setBusy(true);setError('');setMessage('');try{const {parseXacroArguments}=await import('../../lib/xacro-import.mjs');await load({...pendingBundle,entry:entryChoice},{up:'z',forward:'x',unit:1},parseXacroArguments(argumentText));}catch(cause){setError((cause instanceof Error?cause.message:'Xacro 无法展开。')+(asset?' 原模型和尺寸调整已保留。':''));}finally{setBusy(false);}}}><fieldset disabled={busy}><label>主文件<select aria-label="Xacro 主文件" required value={entryChoice} onChange={e=>setEntryChoice(e.target.value)}><option value="" disabled>请选择包含整机定义的文件</option>{xacroFiles?.map(name=><option key={name} value={name}>{name}</option>)}</select></label><label>参数覆盖（可选）<textarea aria-label="Xacro 参数覆盖" rows={3} maxLength={8192} value={argumentText} onChange={e=>setArgumentText(e.target.value)} placeholder={'wheel_radius=0.3\nuse_camera=true'}/></label><p className="fg-muted-copy">每行 name=value，留空使用文件默认值。支持常用宏与表达式；复杂 Python、环境变量和命令替换请先用 ROS 展开为 URDF。</p><button className="fg-button fg-primary" type="submit" disabled={!entryChoice}>展开并载入 Xacro</button></fieldset></form></details>}
    <div className="fg-import-feedback" aria-live="polite">{error&&<p role="alert" className="fg-config-error">{error}</p>}{message&&<p role="status" className="fg-config-message">{message}</p>}</div>
    <div className="fg-visual-workspace fg-import-workspace">
      <section className="fg-model-area" aria-label="导入模型预览">
        <div className="fg-model-actions"><div className="fg-view-segments" role="group" aria-label="模型视角">{[['perspective','立体'],['top','俯视'],['side','侧视']].map(([name,label])=><button key={name} aria-pressed={view.name===name} className={view.name===name?'is-active':''} onClick={()=>setView({name,revision:view.revision+1})}>{label}</button>)}</div><button className="fg-button fg-quiet" disabled={!asset} onClick={()=>{setView({name:'perspective',revision:view.revision+1});setExplode(0);setIsolate(false);}}>复位视角</button></div>
        {asset?<VehicleModel modelFile="imported" customModel={asset} tuning={applied} selected={selected} explode={explode} isolate={isolate} view={view} onParts={onParts} onSelect={setSelected} onReady={setReady}/>:<div className="fg-import-empty"><Icon name="preview" size={44}/><h2>先导入模型，再指定车轮</h2><p>保留独立车轮部件的模型可调整尺寸。整块网格需要先在建模软件中拆分。</p></div>}
        <div className="fg-explode-control"><button className="fg-button" disabled={!ready} onClick={()=>setExplode(explode?0:65)}>{explode?'恢复装配':'爆炸展开'}</button><button className="fg-button" disabled={!selected} aria-pressed={isolate} onClick={()=>setIsolate(!isolate)}>{isolate?'显示全部':'单独查看'}</button><span className="fg-muted-copy">拖动旋转 · 滚轮缩放 · 点选部件</span></div>
        {!!parts.length&&<details className="fg-parts-browser" open><summary>部件目录 · {parts.length} 个</summary><label className="fg-part-search">查找部件<input type="search" value={search} onChange={e=>setSearch(e.target.value)}/></label><div className="fg-part-list">{shown.map(p=><button key={p.id} aria-pressed={selected===p.id} onClick={()=>setSelected(p.id)}>{p.name}</button>)}</div></details>}
      </section>
      <aside className="fg-profile-editor fg-import-editor" aria-label="模型尺寸调节">
        <div className="fg-config-heading"><div><h2>尺寸调节</h2><span>模型外观 · 不写入控制器</span></div><Icon name="settings"/></div>
        <fieldset disabled={!asset||busy} className="fg-import-fields">
          <details><summary>坐标与单位</summary><p className="fg-muted-copy">先确认车头方向和单位，再指定车轮。应用会清除尺寸调整。</p><label>向上方向<select value={up} onChange={e=>setUp(e.target.value)}>{['x','y','z'].map(v=><option key={v} value={v}>+{v.toUpperCase()}</option>)}</select></label><label>车头方向<select value={forward} onChange={e=>setForward(e.target.value)}>{['x','-x','y','-y','z','-z'].map(v=><option key={v} value={v}>{v.toUpperCase()}</option>)}</select></label><label>源模型单位<select value={unit} onChange={e=>setUnit(Number(e.target.value))}><option value={1}>米 m</option><option value={.01}>厘米 cm</option><option value={.001}>毫米 mm</option></select></label><button className="fg-button" type="button" onClick={async()=>{if(!bundle||!confirm('重新载入会清除当前尺寸调整，是否继续？'))return;setBusy(true);setError('');try{await load(bundle,{up,forward,unit},loadedArguments);}catch(cause){setError(cause instanceof Error?cause.message:'坐标设置无效。');}finally{setBusy(false);}}}>应用坐标与单位</button></details>
          <h3>指定车轮</h3><p className="fg-muted-copy">选中部件后添加到对应车轮；轮胎和轮毂可归为同一车轮。轮轴应沿左右方向。</p>
          {wheelSlots.map(slot=><div className="fg-wheel-binding" key={slot}><div><strong>{slotNames[slot]}</strong><button className="fg-button fg-quiet" aria-label={`添加选中部件到${slotNames[slot]}`} disabled={!selected} onClick={()=>{const next=structuredClone(tuning);for(const key of wheelSlots)next.wheels[key]=next.wheels[key].filter(id=>id!==selected);next.wheels[slot].push(selected);update(next);}}>添加选中部件</button></div><ul>{tuning.wheels[slot].map(id=><li key={id}><span>{parts.find(p=>p.id===id)?.name}</span><button className="fg-button fg-quiet" aria-label={`从${slotNames[slot]}移除${parts.find(p=>p.id===id)?.name}`} onClick={()=>update({...tuning,wheels:{...tuning.wheels,[slot]:tuning.wheels[slot].filter(p=>p!==id)}})}>移除</button></li>)}</ul>{!tuning.wheels[slot].length&&<span className="fg-muted-copy">未指定</span>}</div>)}
          <h3>尺寸 / mm</h3><div className="fg-dimension-fields">{dimensions.map(([key,label])=><label key={key}>{label}<input aria-label={`${label}（毫米）`} type="number" min="1" max="20000" step="any" placeholder="保留原尺寸" value={tuning[key]===null?'':Number.isNaN(tuning[key])?'':tuning[key]} onChange={e=>update({...tuning,[key]:e.target.value===''?null:e.target.valueAsNumber})}/></label>)}</div>
          <p className="fg-muted-copy">轮距、轴距按车轮中心计算；轮宽和半径统一应用于已指定车轮。只移动或缩放车轮，不重建车架与悬架。</p>
          {validation&&<p role="alert" className="fg-config-error">{validation} 预览保留上次有效结果。</p>}
          <div className="fg-config-save"><button className="fg-button" onClick={()=>update(emptyTuning())}>重置尺寸与关联</button><button className="fg-button fg-primary" disabled={!ready||!!validation} onClick={()=>void exportModel()}>导出调整后 GLB</button></div>
          <button className="fg-button fg-quiet" disabled={!!validation} onClick={()=>download('model-dimensions.json',JSON.stringify({schemaVersion:1,source:asset?.name,sourceCoordinates,xacroArguments:loadedArguments,...applied},null,2))}>导出尺寸记录</button>
          <p className="fg-muted-copy">导出为静态装配，保留外观尺寸，不包含原 URDF 关节、惯量或控制逻辑。实机参数仍需单独标定。</p>
        </fieldset>
      </aside>
    </div>
  </>;
}
