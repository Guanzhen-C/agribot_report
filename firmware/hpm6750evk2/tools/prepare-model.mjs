// Local-only CAD asset packaging. Geometry and instance transforms are unchanged.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const [input, output, modelId] = process.argv.slice(2);
if (!input || !output || !modelId || fs.existsSync(output)) throw new Error('Provide native GLB, NEW output GLB, and model ID');
const bytes=fs.readFileSync(input);
if(bytes.readUInt32LE(0)!==0x46546c67 || bytes.readUInt32LE(4)!==2 || bytes.readUInt32LE(8)!==bytes.length)throw new Error('Invalid GLB');
const jsonLength=bytes.readUInt32LE(12), document=JSON.parse(bytes.subarray(20,20+jsonLength));
const tail=bytes.subarray(20+jsonLength);
const provenance=[];
const parts=[];
const names=new Map();
function label(raw) {
  if(/STM32/i.test(raw))return '控制模块';
  if(/主控.*(jetson|orin)|ROS主控/i.test(raw))return '算力模块';
  if(/电池/.test(raw))return '电池组';
  if(/轮胎/.test(raw))return '轮胎';
  if(/法兰/.test(raw))return '轮毂法兰';
  if(/激光雷达/.test(raw))return /驱动板/.test(raw)?'雷达驱动模块':'激光雷达';
  if(/相机/.test(raw))return /USB/.test(raw)?'相机接口模块':'相机组件';
  if(/^=>/.test(raw))return '语音模块部件';
  if(/型材/.test(raw))return /横梁中/.test(raw)?'中横梁':/横梁/.test(raw)?'横梁':/侧梁/.test(raw)?'侧梁':'框架型材';
  if(/卡丁车|小车|底盘|地盘|禾川/i.test(raw))return /铜柱/.test(raw)?'铜柱':'机械组件';
  const value=raw.replace(/^组装-/,'').split('_')[0].split('^')[0].replace('车底板','下承载板').replace('车上板','上承载板');
  return !value||/^\d+$/.test(value)?'框架连接件':value.slice(0,24);
}
for(const [index,node] of document.nodes.entries()){
  provenance.push({node:index,name:node.name,mesh:node.mesh});
  if(node.mesh!==undefined){
    const base=label(node.name??''),count=(names.get(base)??0)+1;names.set(base,count);
    const id=`part-${String(parts.length+1).padStart(4,'0')}`;
    parts.push({id,name:`${base} ${String(count).padStart(2,'0')}`});
    node.name=id;node.extras={partId:id};
  }else node.name=`assembly-${index}`;
}
for(const [i,mesh] of document.meshes.entries())mesh.name=`geometry-${i}`;
for(const [i,material] of document.materials.entries())material.name=`material-${i}`;
document.asset={version:'2.0',generator:'FieldGate local CAD conversion'};
const triangles=document.meshes.reduce((sum,mesh)=>sum+mesh.primitives.reduce((n,p)=>n+((p.mode??4)===4?document.accessors[p.indices].count/3:0),0),0);
if(!triangles || !parts.length)throw new Error('Empty geometry');
const json=Buffer.from(JSON.stringify(document)),padding=Buffer.alloc((4-json.length%4)%4,32);
const header=Buffer.alloc(20);header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);header.writeUInt32LE(20+json.length+padding.length+tail.length,8);header.writeUInt32LE(json.length+padding.length,12);header.writeUInt32LE(0x4e4f534a,16);
fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,Buffer.concat([header,json,padding,tail]),{flag:'wx'});
fs.writeFileSync(output.replace(/\.glb$/,'.parts.json'),JSON.stringify({modelId,parts,triangles},null,2),{flag:'wx'});
fs.writeFileSync(`${input}.provenance.json`,JSON.stringify({sourceSha256:crypto.createHash('sha256').update(bytes).digest('hex'),nodes:provenance},null,2),{flag:'wx'});
console.log(JSON.stringify({output,bytes:fs.statSync(output).size,parts:parts.length,triangles}));
