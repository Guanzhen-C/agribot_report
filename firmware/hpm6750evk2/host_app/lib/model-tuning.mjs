import {Box3, Group, Matrix4, Mesh, Vector3} from 'three';

export const wheelSlots = ['frontLeft','frontRight','rearLeft','rearRight'];
export const emptyTuning = () => ({wheels:Object.fromEntries(wheelSlots.map(key=>[key,[]])),trackMm:null,wheelbaseMm:null,widthMm:null,radiusMm:null});
const axis = value => { const v=new Vector3(); const i='xyz'.indexOf(value.replace('-','')); if(i<0)throw new Error('坐标轴无效。'); v.setComponent(i,value.startsWith('-')?-1:1);return v; };

// Bake the zero-pose assembly into independent parts in metres: X forward, Y left, Z up.
// Never infer control calibration or rewrite an imported kinematic tree.
export function prepareModel(source,{up='z',forward='x',unit=1}={}){
  const z=axis(up),x=axis(forward);
  if(Math.abs(z.dot(x))>0.1||![1,.001,.01].includes(unit))throw new Error('前向与向上方向不能重合，单位必须为 m、cm 或 mm。');
  const y=z.clone().cross(x),basis=new Matrix4().makeBasis(x,y,z).transpose().multiply(new Matrix4().makeScale(unit,unit,unit));
  const groups=new Map(),root=new Group();let vertices=0;
  source.updateMatrixWorld(true);
  source.traverse(mesh=>{
    if(!mesh.isMesh)return;
    if(mesh.isSkinnedMesh||mesh.isInstancedMesh||Object.keys(mesh.geometry.morphAttributes).length)throw new Error('暂不支持骨骼、实例或变形动画，请先导出独立部件的静态装配。');
    vertices+=mesh.geometry.attributes.position?.count??0;
    if(vertices>3000000)throw new Error('模型超过 300 万顶点，请简化后再导入。');
    let owner=mesh;
    for(let p=mesh;p&&p!==source;p=p.parent){if(p.isURDFLink||p.userData.partId){owner=p;break;}}
    if(!groups.has(owner))groups.set(owner,new Group());
    const g=mesh.geometry.clone().applyMatrix4(basis.clone().multiply(mesh.matrixWorld));
    const materials=Array.isArray(mesh.material)?mesh.material.map(m=>m.clone()):mesh.material.clone();
    groups.get(owner).add(new Mesh(g,materials));
  });
  if(!groups.size||groups.size>1024)throw new Error('模型须包含 1–1024 个可显示部件。');
  const parts=[];
  for(const [owner,group]of groups){
    const box=new Box3().setFromObject(group),center=box.getCenter(new Vector3()),size=box.getSize(new Vector3());
    if(![...center.toArray(),...size.toArray()].every(Number.isFinite)||size.length()===0)throw new Error('模型包含无效尺寸。');
    const id=`import-part-${parts.length+1}`;
    group.name=owner.name||`部件 ${parts.length+1}`;group.userData={partId:id};
    for(const mesh of group.children)mesh.geometry.translate(-center.x,-center.y,-center.z);
    group.position.copy(center);root.add(group);
    parts.push({id,name:group.name,center:center.toArray(),size:size.toArray()});
  }
  root.updateMatrixWorld(true);
  return {scene:root,parts};
}

export function validateTuning(parts,tuning){
  const ids=new Set(parts.map(p=>p.id)),seen=new Set();
  for(const slot of wheelSlots){
    const selection=tuning.wheels[slot];
    if(!Array.isArray(selection))throw new Error('车轮关联格式不正确。');
    for(const id of selection){if(!ids.has(id)||seen.has(id))throw new Error('同一部件只能属于一个车轮，且必须存在于当前模型。');seen.add(id);}
  }
  for(const key of ['trackMm','wheelbaseMm','widthMm','radiusMm']){
    const value=tuning[key];
    if(value!==null&&(!Number.isFinite(value)||value<1||value>20000))throw new Error('尺寸应为 1–20000 mm，留空则保留原尺寸。');
  }
  if(['trackMm','wheelbaseMm'].some(k=>tuning[k]!==null)&&wheelSlots.some(k=>!tuning.wheels[k].length))throw new Error('调整轮距或轴距前，请指定四个车轮。');
  if(['widthMm','radiusMm'].some(k=>tuning[k]!==null)&&!seen.size)throw new Error('请先指定需要调整的车轮部件。');
}

export function wheelBounds(parts,ids){
  const box=new Box3();
  for(const p of parts.filter(p=>ids.includes(p.id))){const c=new Vector3(...p.center),s=new Vector3(...p.size).multiplyScalar(.5);box.expandByPoint(c.clone().sub(s));box.expandByPoint(c.add(s));}
  return {center:box.getCenter(new Vector3()),size:box.getSize(new Vector3())};
}

export function tuningTransforms(parts,tuning){
  validateTuning(parts,tuning);
  const transforms=Object.fromEntries(parts.map(p=>[p.id,{position:[...p.center],scale:[1,1,1]}]));
  const bounds=Object.fromEntries(wheelSlots.filter(k=>tuning.wheels[k].length).map(k=>[k,wheelBounds(parts,tuning.wheels[k])]));
  const complete=wheelSlots.every(k=>bounds[k]);
  const center=complete?wheelSlots.reduce((v,k)=>v.add(bounds[k].center),new Vector3()).multiplyScalar(.25):new Vector3();
  for(const slot of Object.keys(bounds)){
    const b=bounds[slot],target=b.center.clone(),scale=new Vector3(1,1,1);
    if(tuning.trackMm!==null)target.y=center.y+(slot.endsWith('Left')?1:-1)*tuning.trackMm/2000;
    if(tuning.wheelbaseMm!==null)target.x=center.x+(slot.startsWith('front')?1:-1)*tuning.wheelbaseMm/2000;
    if(tuning.widthMm!==null){if(b.size.y<1e-8)throw new Error('所选车轮没有有效轮宽。');scale.y=tuning.widthMm/1000/b.size.y;}
    if(tuning.radiusMm!==null){const diameter=Math.max(b.size.x,b.size.z);if(diameter<1e-8)throw new Error('所选车轮没有有效半径。');scale.x=scale.z=tuning.radiusMm/500/diameter;target.z+=tuning.radiusMm/1000-diameter/2;}
    for(const p of parts.filter(p=>tuning.wheels[slot].includes(p.id)))transforms[p.id]={position:new Vector3(...p.center).sub(b.center).multiply(scale).add(target).toArray(),scale:scale.toArray()};
  }
  return transforms;
}

export function tunedScene(asset,tuning){
  const transforms=tuningTransforms(asset.parts,tuning),scene=asset.scene.clone(true);
  scene.traverse(n=>{const t=transforms[n.userData.partId];if(t){n.position.fromArray(t.position);n.scale.fromArray(t.scale);}});
  scene.updateMatrixWorld(true);return scene;
}

export function disposeModel(scene){
  const geometries=new Set(),materials=new Set(),textures=new Set();
  scene.traverse(n=>{if(n.isMesh){geometries.add(n.geometry);for(const m of Array.isArray(n.material)?n.material:[n.material]){materials.add(m);for(const v of Object.values(m))if(v?.isTexture)textures.add(v);}}});
  geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());textures.forEach(t=>{t.dispose();t.source?.data?.close?.();});
}
