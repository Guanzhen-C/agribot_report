import {Box3,Euler,Group,Matrix4,Mesh,Vector3} from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {STLExporter} from 'three/addons/exporters/STLExporter.js';
import {zipSync,strToU8} from 'three/addons/libs/fflate.module.js';
import {jointSettings,partRole,robotLinks,robotMeasurements,robotYaw,validateRobotDescription} from './robot-description.mjs';

const esc=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const fmt=value=>Number(value.toFixed(9)).toString();
const vec=value=>value.map(fmt).join(' ');
export function frameMatrix(joint){return new Matrix4().makeRotationFromEuler(new Euler(...joint.rpy,'ZYX')).setPosition(...joint.xyz);}
export function createUrdf(draft,parts,{emptyLinks=new Set()}={}){
  validateRobotDescription(draft,parts,{exporting:true});
  const active=robotLinks(draft,parts),joints=new Map(active.map(p=>[p.id,jointSettings(p,draft)]));
  const links=[{id:'base_link',name:'base_link',matrix:new Matrix4()},...active.map(p=>({id:p.id,name:joints.get(p.id).name,matrix:frameMatrix(joints.get(p.id))}))];
  const byId=new Map(links.map(link=>[link.id,link]));
  let xml=`<?xml version="1.0" encoding="UTF-8"?>\n<!-- ${esc(draft.author).replace(/--/g,'- -')} | Assembly description, not a validated dynamics model. -->\n<robot name="${esc(draft.packageName)}">\n`;
  for(const link of links){
    xml+=`  <link name="${esc(link.name)}">${emptyLinks.has(link.name)?'':`<visual><geometry><mesh filename="package://${draft.packageName}/meshes/${link.name}.stl"/></geometry></visual>`}</link>\n`;
    if(link.id==='base_link')continue;
    const j=joints.get(link.id),parent=byId.get(j.parent);
    const relative=parent.matrix.clone().invert().multiply(link.matrix),xyz=new Vector3().setFromMatrixPosition(relative),rpy=new Euler().setFromRotationMatrix(relative,'ZYX');
    xml+=`  <joint name="${esc(link.name)}_joint" type="${j.type}"><parent link="${esc(parent.name)}"/><child link="${esc(link.name)}"/><origin xyz="${vec(xyz.toArray())}" rpy="${vec([rpy.x,rpy.y,rpy.z])}"/>`;
    if(j.type!=='fixed'){
      const length=Math.hypot(...j.axis);
      xml+=`<axis xyz="${vec(j.axis.map(v=>v/length))}"/><limit effort="${fmt(j.effort)}" velocity="${fmt(j.velocity)}"${j.type==='continuous'?'':` lower="${fmt(j.lower)}" upper="${fmt(j.upper)}"`}/>`;
    }
    xml+='</joint>\n';
    if(j.optical)xml+=`  <link name="${esc(link.name)}_optical"/><joint name="${esc(link.name)}_optical_joint" type="fixed"><parent link="${esc(link.name)}"/><child link="${esc(link.name)}_optical"/><origin xyz="0 0 0" rpy="${fmt(-Math.PI/2)} 0 ${fmt(-Math.PI/2)}"/></joint>\n`;
  }
  return {xml:xml+'</robot>\n',links};
}
export async function createRobotPackage(buffer,draft,parts,onProgress=()=>{}){
  const {links}=createUrdf(draft,parts),files={},prefix=draft.packageName+'/',groups=new Map(links.map(l=>[l.id,new Group()]));
  const source=(await new GLTFLoader().parseAsync(buffer,'')).scene;
  source.updateMatrixWorld(true);
  const origin=robotMeasurements(draft.modelId).origin;
  const basis=new Matrix4().makeRotationZ(robotYaw(draft)).multiply(new Matrix4().makeTranslation(...origin.map(v=>-v)));
  const nodes=new Map();source.traverse(node=>{if(node.userData.partId)nodes.set(node.userData.partId,node);});
  const linkMap=new Map(links.map(l=>[l.id,l]));
  const mapping={};
  const dispose=()=>{source.traverse(n=>{if(n.isMesh){n.geometry.dispose();for(const m of Array.isArray(n.material)?n.material:[n.material])m.dispose();}});for(const group of groups.values())group.traverse(n=>{if(n.isMesh)n.geometry.dispose();});};
  try{
    for(const [index,part]of parts.entries()){
      const node=nodes.get(part.id);if(!node)throw new Error('模型缺少指定部件，无法生成完整描述包。');
      const id=groups.has(part.id)?part.id:'base_link',link=linkMap.get(id),group=groups.get(id);
      mapping[part.id]={name:part.name,role:partRole(part,draft),link:link.name};
      node.traverse(child=>{if(child.isMesh){
        const geometry=child.geometry.clone().applyMatrix4(link.matrix.clone().invert().multiply(basis).multiply(child.matrixWorld));
        group.add(new Mesh(geometry,child.material));
      }});
      if(index%8===0){onProgress(Math.round(index/parts.length*65));await new Promise(resolve=>setTimeout(resolve,0));}
    }
    const bounds={},emptyLinks=new Set();let index=0;
    for(const link of links){
      const group=groups.get(link.id);group.updateMatrixWorld(true);
      if(!group.children.length){emptyLinks.add(link.name);continue;}
      const data=new STLExporter().parse(group,{binary:true});
      files[prefix+`meshes/${link.name}.stl`]=new Uint8Array(data.buffer,data.byteOffset,data.byteLength);
      const box=new Box3().setFromObject(group);bounds[link.name]={min:box.min.toArray(),max:box.max.toArray()};
      onProgress(65+Math.round(++index/links.length*25));await new Promise(resolve=>setTimeout(resolve,0));
    }
    const {xml}=createUrdf(draft,parts,{emptyLinks});
    files[prefix+'urdf/robot.urdf']=strToU8(xml);
    files[prefix+'robot-description.json']=strToU8(JSON.stringify(draft,null,2));
    files[prefix+'part-links.json']=strToU8(JSON.stringify({modelId:draft.modelId,units:'m',parts:mapping,bounds,linksWithoutSolidMesh:[...emptyLinks]},null,2));
    files[prefix+'package.xml']=strToU8(`<?xml version="1.0"?><package format="3"><name>${draft.packageName}</name><version>0.1.0</version><description>Local assembly description; validate joint calibration before use.</description><maintainer email="maintainer@example.invalid">${esc(draft.author)}</maintainer><license>Proprietary</license><buildtool_depend>ament_cmake</buildtool_depend><exec_depend>robot_state_publisher</exec_depend><export><build_type>ament_cmake</build_type></export></package>`);
    files[prefix+'CMakeLists.txt']=strToU8(`cmake_minimum_required(VERSION 3.8)\nproject(${draft.packageName})\nfind_package(ament_cmake REQUIRED)\ninstall(DIRECTORY urdf meshes DESTINATION share/\u0024{PROJECT_NAME})\nament_package()\n`);
    files[prefix+'README.md']=strToU8(`# ${draft.name}\n作者：${draft.author}\n\n装配描述草稿。单位 m/rad，X 前、Y 左、Z 上。所选 CAD 前向为 ${draft.forward}，必须按实物核验。base_link 位于模型包围盒底面中心，不等于实机标定原点。\n\n结构件自动合并，不需要 VCU 关联。未定义运动的关节为 fixed；关节原点默认取部件包围盒中心，不是已知轴心或光心。相机 optical 是从人工确认的标准机体坐标约定转换，不能代替外参标定。无惯量、碰撞体、控制器或 ros2_control 驱动，不能直接用于动力学仿真或实机控制。闭环连杆需人工建模，当前树结构不推断阿克曼机构运动学。\n\n将本目录放入 ROS 2 工作区 src，colcon build 后 source install/setup.bash。使用 urdf/robot.urdf 加载 robot_state_publisher/RViz；在配置中使用实际 robot_description。meshes 是本地模型导出的二进制 STL，保留尺寸和零位装配，不保留原材质颜色。part-links.json 保留装配映射，其中 linksWithoutSolidMesh 列出源 CAD 仅含线框的节点；这些节点保留坐标，不生成占位实体。package.xml 的联系邮箱是明确的占位，分发前更换并确认 CAD 授权。机器人描述与 FGMP 分别管理，不向 VCU 写入感知配置。\n`);
    onProgress(95);
    const archive=zipSync(files,{level:1});onProgress(100);
    return {archive,xml,files,links:links.length,mapping};
  }finally{dispose();}
}
