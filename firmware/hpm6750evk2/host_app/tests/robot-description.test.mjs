import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import {Matrix4,Vector3} from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {unzipSync,strFromU8} from 'three/addons/libs/fflate.module.js';
import {createRobotDescription,defaultPartRole,isActuator,jointSettings,readRobotDescription,robotLinks,robotMeasurements,validateRobotDescription} from '../lib/robot-description.mjs';
import {createRobotPackage,createUrdf} from '../lib/urdf-export.mjs';
const manifest=file=>JSON.parse(fs.readFileSync(new URL(`../public/models/${file}.parts.json`,import.meta.url)));
const standard=manifest('ackermann');

test('CAD classification keeps sensors, power and mechanical fasteners out of actuator mapping',()=>{
  assert.equal(defaultPartRole({name:'雷达驱动模块'}),'perception');
  assert.equal(defaultPartRole({name:'相机支架'}),'structure');
  assert.equal(defaultPartRole({name:'急停开关'}),'support');
  assert.equal(defaultPartRole({name:'轮胎'}),'actuator');
  assert.equal(standard.parts.filter(p=>defaultPartRole(p)==='actuator').length,7);
  assert.equal(manifest('ackermann-flagship').parts.filter(p=>defaultPartRole(p)==='actuator').length,5);
  assert.equal(isActuator({category:'battery',control:'none'}),false);
  assert.equal(isActuator({category:'steering',control:'drive.leftSteeringMrad'}),true);
});

test('robot descriptions are separate, fixed by default, optional and contain no fabricated dynamics',()=>{
  const draft=createRobotDescription(standard.modelId),before=JSON.stringify(draft);
  const {xml,links}=createUrdf(draft,standard.parts);
  assert.ok(links.length<standard.parts.length);
  assert.equal((xml.match(/type="fixed"/g)||[]).length,links.length-1);
  assert.doesNotMatch(xml,/<inertial|<collision|ros2_control|<transmission/);
  assert.equal(JSON.stringify(draft),before);
  assert.deepEqual(readRobotDescription(before,standard.modelId,standard.parts),draft);
  assert.throws(()=>readRobotDescription(before,'ackermann-flagship-v1',standard.parts));
  assert.throws(()=>readRobotDescription('bad',standard.modelId,standard.parts));
});

test('kinematic export rejects unconfirmed motion, invalid axes/limits, cycles, duplicate names and unsafe paths',()=>{
  const draft=createRobotDescription(standard.modelId),wheel=standard.parts.find(p=>p.id==='part-0043');
  draft.joints[wheel.id]={...jointSettings(wheel,draft),type:'revolute'};
  validateRobotDescription(draft,standard.parts); // Incomplete draft can be saved.
  assert.throws(()=>createUrdf(draft,standard.parts),/核对/);
  const j=draft.joints[wheel.id];j.confirmed=true;
  assert.throws(()=>createUrdf(draft,standard.parts),/运动轴/);
  Object.assign(j,{axis:[0,2/3,0],effort:10,velocity:2,lower:-.5,upper:.5});
  assert.match(createUrdf(draft,standard.parts).xml,/<axis xyz="0 1 0"/);
  for(const update of [{axis:[0,0,0]},{velocity:0},{lower:1,upper:0},{name:'../bad'},{name:undefined},{xyz:[NaN,0,0]},{parent:wheel.id}]){
    const bad=structuredClone(draft);Object.assign(bad.joints[wheel.id],update);assert.throws(()=>createUrdf(bad,standard.parts));
  }
  const second=standard.parts.find(p=>p.id==='part-0045');draft.joints[second.id]={...jointSettings(second,draft),name:j.name};
  assert.throws(()=>createUrdf(draft,standard.parts),/重复/);
  delete draft.joints[second.id];draft.packageName='../unsafe';assert.throws(()=>createUrdf(draft,standard.parts));
  draft.packageName=undefined;assert.throws(()=>createUrdf(draft,standard.parts));
});

test('nested joints use parent-relative transforms and optional camera optical frame convention',()=>{
  const draft=createRobotDescription(standard.modelId),links=robotLinks(draft,standard.parts),a=links[0],b=links[1];
  draft.joints[a.id]={...jointSettings(a,draft),xyz:[1,2,3],rpy:[0,0,Math.PI/2]};
  draft.joints[b.id]={...jointSettings(b,draft),parent:a.id,xyz:[1,3,3],optical:true};
  assert.throws(()=>createUrdf(draft,standard.parts),/相机安装/);
  draft.joints[b.id].confirmed=true;
  const {xml}=createUrdf(draft,standard.parts);
  assert.match(xml,/origin xyz="1 0 0" rpy="0 0 -1.570796327"/);
  assert.match(xml,/rpy="-1.570796327 0 -1.570796327"/);
  draft.joints[a.id].parent=b.id;assert.throws(()=>createUrdf(draft,standard.parts),/循环/);
});

for(const file of ['ackermann','ackermann-flagship'])test(`${file}: complete ZIP, mesh references, physical scale and zero-pose geometry round trip`,async()=>{
  const {modelId,parts}=manifest(file),draft=createRobotDescription(modelId),part=parts.find(p=>defaultPartRole(p)==='actuator');
  // Nontrivial pivot/orientation must NOT shift the original zero-pose geometry.
  draft.joints[part.id]={...jointSettings(part,draft),xyz:[.1,.2,.3],rpy:[.2,.3,.4]};
  const before=JSON.stringify(draft),bytes=fs.readFileSync(new URL(`../public/models/${file}.glb`,import.meta.url));
  const result=await createRobotPackage(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),draft,parts);
  const files=unzipSync(result.archive),prefix=draft.packageName+'/';
  assert.equal(strFromU8(files[prefix+'urdf/robot.urdf']),result.xml);
  assert.equal(Object.keys(result.mapping).length,parts.length);
  const {links}=createUrdf(draft,parts),byName=new Map(links.map(l=>[l.name,l]));
  const min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity],point=new Vector3();let exportedTriangles=0;
  for(const [name,data]of Object.entries(files).filter(([key])=>key.endsWith('.stl'))){
    const view=new DataView(data.buffer,data.byteOffset,data.byteLength),count=view.getUint32(80,true);
    assert.ok(count>0);assert.equal(data.length,84+50*count);
    exportedTriangles+=count;
    const link=byName.get(name.split('/').at(-1).slice(0,-4));assert.ok(link);
    assert.ok(result.xml.includes(`package://${name}`));
    for(let t=0;t<count;t++)for(let v=0;v<3;v++){
      const offset=84+50*t+12+12*v;
      point.set(view.getFloat32(offset,true),view.getFloat32(offset+4,true),view.getFloat32(offset+8,true)).applyMatrix4(link.matrix);
      for(const [i,value]of [point.x,point.y,point.z].entries()){assert.ok(Number.isFinite(value));min[i]=Math.min(min[i],value);max[i]=Math.max(max[i],value);}
    }
  }
  const source=(await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'')).scene;
  source.updateMatrixWorld(true);
  const expectedMin=[Infinity,Infinity,Infinity],expectedMax=[-Infinity,-Infinity,-Infinity];let sourceTriangles=0;
  const basis=new Matrix4().makeRotationZ(Math.PI/2).multiply(new Matrix4().makeTranslation(...robotMeasurements(modelId).origin.map(v=>-v)));
  source.traverse(mesh=>{if(mesh.isMesh){
    const position=mesh.geometry.getAttribute('position'),indices=mesh.geometry.getIndex(),count=indices?indices.count:position.count;
    sourceTriangles+=count/3;const matrix=basis.clone().multiply(mesh.matrixWorld);
    for(let i=0;i<count;i++){
      point.fromBufferAttribute(position,indices?indices.getX(i):i).applyMatrix4(matrix);
      [point.x,point.y,point.z].forEach((value,k)=>{expectedMin[k]=Math.min(expectedMin[k],value);expectedMax[k]=Math.max(expectedMax[k],value);});
    }
    mesh.geometry.dispose();for(const m of Array.isArray(mesh.material)?mesh.material:[mesh.material])m.dispose();
  }});
  assert.equal(exportedTriangles,sourceTriangles);
  for(let i=0;i<3;i++){assert.ok(Math.abs(min[i]-expectedMin[i])<1e-5);assert.ok(Math.abs(max[i]-expectedMax[i])<1e-5);}
  assert.ok(max[0]-min[0]<1);assert.equal(JSON.stringify(draft),before);
});
