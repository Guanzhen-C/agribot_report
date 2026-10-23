import test from 'node:test';
import assert from 'node:assert/strict';
import {BoxGeometry,CylinderGeometry,Group,Mesh,MeshStandardMaterial,Box3,Vector3} from 'three';
import {emptyTuning,prepareModel,tuningTransforms,tunedScene,wheelSlots,disposeModel} from '../lib/model-tuning.mjs';
import {cleanPath,findResource,readModelFiles} from '../lib/model-import.mjs';
import {zipSync,strToU8} from 'three/addons/libs/fflate.module.js';

function fixture(){const root=new Group();for(const [i,key]of wheelSlots.entries()){const mesh=new Mesh(new CylinderGeometry(.3,.3,.2,32),new MeshStandardMaterial());mesh.name=key;mesh.position.set(i<2?1:-1,i%2?-.6:.6,.3);root.add(mesh);}const body=new Mesh(new BoxGeometry(2,1,.3),new MeshStandardMaterial());body.name='body';body.position.z=.6;root.add(body);return root;}
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-5,`${a} != ${b}`);
test('model parameters move wheel centres, resize radial/axial dimensions, and keep chassis/pristine untouched',()=>{
  const source=fixture(),asset=prepareModel(source),t=emptyTuning();
  wheelSlots.forEach((key,i)=>{t.wheels[key]=[asset.parts[i].id];});
  Object.assign(t,{trackMm:1600,wheelbaseMm:2600,widthMm:320,radiusMm:450});
  const before=JSON.stringify(asset.scene.toJSON()),next=tunedScene(asset,t),transform=tuningTransforms(asset.parts,t);
  wheelSlots.forEach((key,i)=>{const id=asset.parts[i].id,b=new Box3().setFromObject(next.children[i]),s=b.getSize(new Vector3()),c=b.getCenter(new Vector3());near(c.x,i<2?1.3:-1.3);near(c.y,i%2?-.8:.8);near(c.z,.45);near(s.y,.32);near(s.x,.9);near(s.z,.9);near(transform[id].scale[0],1.5);});
  assert.deepEqual(next.children[4].position.toArray(),asset.scene.children[4].position.toArray());
  assert.equal(JSON.stringify(asset.scene.toJSON()),before);
  assert.deepEqual(tuningTransforms(asset.parts,emptyTuning())[asset.parts[0].id].position,asset.parts[0].center);
  disposeModel(source);disposeModel(asset.scene);
});
test('part groups share wheel centre, reject duplicate membership and invalid/incomplete geometry',()=>{
  const asset=prepareModel(fixture()),t=emptyTuning();t.wheels.frontLeft=[asset.parts[0].id];t.widthMm=250;
  assert.doesNotThrow(()=>tuningTransforms(asset.parts,t));t.trackMm=1500;assert.throws(()=>tuningTransforms(asset.parts,t),/四个/);t.trackMm=null;t.wheels.rearLeft=[asset.parts[0].id];assert.throws(()=>tuningTransforms(asset.parts,t),/只能/);
  t.wheels.rearLeft=[];for(const v of [NaN,Infinity,0,-5,20001]){t.widthMm=v;assert.throws(()=>tuningTransforms(asset.parts,t));}
  assert.throws(()=>prepareModel(fixture(),{up:'z',forward:'-z'}),/不能重合/);
  const mm=prepareModel(fixture(),{unit:.001});near(mm.parts[0].center[0],.001);
});
test('local resource resolution rejects external/escaping/ambiguous paths',()=>{
  for(const path of ['http://example.com/a','/etc/a','../../a','C:\\model.glb'])assert.throws(()=>cleanPath(path));
  assert.equal(cleanPath('robot/urdf/../meshes/a.stl'),'robot/meshes/a.stl');
  const entries=new Map([['robot/meshes/a.stl',new Uint8Array()],['other/meshes/a.stl',new Uint8Array()]]);
  assert.equal(findResource(entries,'robot/meshes/a.stl'),'robot/meshes/a.stl');assert.throws(()=>findResource(entries,'meshes/a.stl'),/不唯一/);assert.throws(()=>findResource(entries,'missing.stl'),/缺少/);
});
test('ZIP import identifies one URDF entry and refuses multiple roots, oversize and traversal',async()=>{
  const packed=zipSync({'pkg/urdf/robot.urdf':strToU8('<robot/>'),'pkg/meshes/wheel.glb':new Uint8Array([1])});
  const bundle=await readModelFiles([new File([packed],'robot.zip')]);assert.equal(bundle.entry,'pkg/urdf/robot.urdf');assert.equal(bundle.entries.size,2);
  await assert.rejects(()=>readModelFiles([new File(['a'],'a.urdf'),new File(['b'],'b.urdf')]),/一个/);
  await assert.rejects(()=>readModelFiles([{size:170*1024*1024}]),/160 MB/);
  await assert.rejects(()=>readModelFiles([new File([zipSync({'../escape.urdf':strToU8('x')})],'bad.zip')]),/超出/);
});
