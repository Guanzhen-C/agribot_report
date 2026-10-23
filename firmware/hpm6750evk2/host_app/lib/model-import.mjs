import {LoadingManager,Matrix4,Mesh,MeshStandardMaterial} from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {STLLoader} from 'three/addons/loaders/STLLoader.js';
import {ColladaLoader} from 'three/addons/loaders/ColladaLoader.js';
import {GLTFExporter} from 'three/addons/exporters/GLTFExporter.js';
import {unzipSync} from 'three/addons/libs/fflate.module.js';
import URDFLoader from 'urdf-loader';
import {prepareModel,tunedScene,disposeModel} from './model-tuning.mjs';

const limit=160*1024*1024;
export function cleanPath(raw){
  const value=decodeURIComponent(raw).replaceAll('\\','/');
  if(/^(?:[a-z]+:|\/)/i.test(value)||value.includes('\0'))throw new Error('资源路径须为包内相对路径，不允许外部地址。');
  const parts=[];for(const p of value.split('/')){if(p==='..'){if(!parts.length)throw new Error('资源路径超出导入目录。');parts.pop();}else if(p&&p!=='.')parts.push(p);}
  return parts.join('/');
}
export async function readModelFiles(files){
  if(!files.length)throw new Error('请选择模型文件。');
  if(files.reduce((n,f)=>n+f.size,0)>limit)throw new Error('导入文件合计不能超过 160 MB。');
  const entries=new Map();let bytes=0,count=0;
  const add=(name,data)=>{name=cleanPath(name);if(!name||name.endsWith('/'))return;bytes+=data.byteLength;if(++count>1024||bytes>limit)throw new Error('解压后最多 1024 个文件、160 MB。');if(entries.has(name))throw new Error('存在重名资源，请保留目录结构并打包 ZIP。');entries.set(name,data);};
  for(const file of files){
    const data=new Uint8Array(await file.arrayBuffer());
    if(/\.zip$/i.test(file.name)){
      let expanded=bytes,items=count;
      const unpacked=unzipSync(data,{filter:entry=>{cleanPath(entry.name);expanded+=entry.originalSize;if(++items>1024||expanded>limit)throw new Error('压缩包超过 1024 个文件或 160 MB，已停止解压。');return !entry.name.endsWith('/');}});
      for(const [name,value]of Object.entries(unpacked))add(name,value);
    }else add(file.webkitRelativePath||file.name,data);
  }
  const candidates=[...entries.keys()].filter(p=>/\.(urdf|xacro|xcaro|glb|gltf)$/i.test(p));
  const xacros=candidates.filter(p=>/\.(xacro|xcaro)$/i.test(p));
  if(xacros.length){const roots=candidates.filter(p=>/\.(urdf|xacro|xcaro)$/i.test(p));return {entries,entry:roots.length===1?roots[0]:'',candidates:roots};}
  // URDF packages may contain glTF mesh dependencies; the URDF is their entry point.
  const urdfs=candidates.filter(p=>/\.urdf$/i.test(p));
  if(!candidates.length)throw new Error('未找到 GLB、glTF、URDF 或 Xacro 文件。');
  if(urdfs.length>1||(!urdfs.length&&candidates.length!==1))throw new Error('请一次导入一个模型入口；ZIP 中只保留一个 URDF，或一个 GLB/glTF。');
  return {entries,entry:urdfs[0]||candidates[0]};
}

export function findResource(entries,path){
  const key=cleanPath(path);
  if(entries.has(key))return key;
  const matches=[...entries.keys()].filter(p=>p.endsWith('/'+key)||key.endsWith('/'+p));
  if(matches.length===1)return matches[0];
  throw new Error(matches.length?'资源路径不唯一：'+key:'缺少资源：'+key+'。请将网格与贴图一起打包 ZIP。');
}

function urdfDocument(text){
  if(/<!DOCTYPE|<!ENTITY|<\/?xacro:|\$\{/i.test(text))throw new Error('不支持 DTD、实体或 Xacro，请导入展开后的 URDF。');
  const doc=new DOMParser().parseFromString(text,'text/xml');
  if(doc.querySelector('parsererror')||doc.documentElement.tagName!=='robot')throw new Error('URDF XML 格式不正确。');
  const links=[...doc.querySelectorAll('robot > link')].map(n=>n.getAttribute('name'));
  if(!links.length||links.length>1024||links.some(n=>!n)||new Set(links).size!==links.length)throw new Error('URDF 连杆名称重复、缺失或数量超限。');
  const parents=new Map(),jointNames=new Set();
  for(const joint of doc.querySelectorAll('robot > joint')){
    const name=joint.getAttribute('name'),parent=joint.querySelector('parent')?.getAttribute('link'),child=joint.querySelector('child')?.getAttribute('link');
    if(!name||jointNames.has(name)||!links.includes(parent)||!links.includes(child)||parents.has(child))throw new Error('URDF 关节名称或父子连接无效。');
    jointNames.add(name);parents.set(child,parent);
  }
  if(links.filter(n=>!parents.has(n)).length!==1)throw new Error('URDF 必须是单根装配树。');
  for(const name of links){const seen=new Set();let p=name;while(parents.has(p)){if(seen.has(p))throw new Error('URDF 存在循环连接。');seen.add(p);p=parents.get(p);}}
  return doc;
}

export async function importModel(bundle,options,xacroArguments={}){
  const {entries,entry}=bundle,urls=new Map(),manager=new LoadingManager(),errors=[];
  const urlFor=path=>{
    if(/^data:(?:application\/octet-stream|application\/gltf-buffer|image\/(?:png|jpeg|webp));base64,/i.test(path))return path;
    if([...urls.values()].includes(path))return path;
    const key=findResource(entries,path);
    if(!urls.has(key))urls.set(key,URL.createObjectURL(new Blob([entries.get(key)])));
    return urls.get(key);
  };
  manager.setURLModifier(urlFor);manager.onError=url=>errors.push('资源无法读取：'+url);
  const base=entry.includes('/')?entry.slice(0,entry.lastIndexOf('/')+1):'';
  let source;
  try{
    if(!entry||!entries.has(entry))throw new Error('请选择要载入的模型入口文件。');
    const data=entries.get(entry),format=entry.split('.').pop().toLowerCase();
    if(['urdf','xacro','xcaro'].includes(format)){
      let text=new TextDecoder().decode(data);
      if(format!=='urdf'||/<\/?xacro:|\$\{/.test(text)){
        const {expandXacro}=await import('./xacro-import.mjs');
        text=await expandXacro(bundle,path=>findResource(entries,path),xacroArguments);
      }
      const doc=urdfDocument(text),loader=new URDFLoader(manager),pending=[];
      loader.workingPath=base;loader.packages=pkg=>pkg+'/';loader.parseCollision=false;
      // This callback is the documented v0.13 API; all paths resolve from selected local files.
      loader.loadMeshCb=(path,mgr,material,done)=>{
        const job=(async()=>{
          const key=findResource(entries,path),bytes=entries.get(key),dir=key.includes('/')?key.slice(0,key.lastIndexOf('/')+1):'';
          if(/\.stl$/i.test(key))return new Mesh(new STLLoader().parse(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength)),material||new MeshStandardMaterial());
          if(/\.dae$/i.test(key))return new ColladaLoader(mgr).parse(new TextDecoder().decode(bytes),dir).scene;
          if(/\.(glb|gltf)$/i.test(key))return (await new GLTFLoader(mgr).parseAsync(/\.gltf$/i.test(key)?new TextDecoder().decode(bytes):bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),dir)).scene;
          throw new Error('URDF 网格仅支持 STL、DAE、GLB 和 glTF：'+key);
        })().then(mesh=>done(mesh),error=>{errors.push(error.message);done(null,error);});pending.push(job);
      };
      // Keep the manager active while synchronous primitive and asynchronous mesh loads are scheduled.
      const loaded=new Promise(resolve=>{manager.onLoad=resolve;});manager.itemStart('__local_import__');
      source=loader.parse(doc);await Promise.all(pending);manager.itemEnd('__local_import__');await loaded;
    }else{
      source=(await new GLTFLoader(manager).parseAsync(format==='gltf'?new TextDecoder().decode(data):data.buffer.slice(data.byteOffset,data.byteOffset+data.byteLength),base)).scene;
    }
    if(errors.length)throw new Error(errors.join('\n'));
    const asset=prepareModel(source,options);
    // Materials are copied by prepareModel; textures remain owned by the returned asset.
    source.traverse(n=>{if(n.isMesh){n.geometry.dispose();for(const m of Array.isArray(n.material)?n.material:[n.material])m.dispose();}});
    return {...asset,name:entry.split('/').pop(),format};
  }catch(error){if(source)disposeModel(source);throw error;}
  finally{for(const url of urls.values())URL.revokeObjectURL(url);}
}

export async function exportTunedModel(asset,tuning){
  const scene=tunedScene(asset,tuning);
  // glTF uses Y up. Export a wrapper, not a changed camera or exploded/isolate state.
  // Match the import defaults: +Z forward, +Y up in glTF.
  scene.setRotationFromMatrix(new Matrix4().set(0,1,0,0, 0,0,1,0, 1,0,0,0, 0,0,0,1));
  scene.userData={fieldgate:{units:'m',geometryOnly:true,source:asset.name}};
  return new GLTFExporter().parseAsync(scene,{binary:true,onlyVisible:false});
}
