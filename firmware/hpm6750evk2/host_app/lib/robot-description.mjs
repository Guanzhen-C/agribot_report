import standard from './model-data/ackermann.kinematics.json' with {type:'json'};
import extended from './model-data/ackermann-flagship.kinematics.json' with {type:'json'};

export const ROBOT_STORAGE_KEY='fieldgate.robot-descriptions.v1';
export const roleNames={actuator:'执行机构',perception:'算力与感知',structure:'结构外观',support:'供电与安全'};
const measurements={'ackermann-independent-v1':standard,'ackermann-flagship-v1':extended};
export const isActuator=device=>['drive','steering','light','work','pump','fan','shredder','brake'].includes(device.category) || (device.control!=='none' && device.category!=='safety' && device.category!=='battery');
export function defaultPartRole(part){
  const name=part.name.replace(/\s/g,'');
  if(/支架|承接板|铜柱|法兰|外壳/.test(name))return 'structure';
  if(/雷达|相机|算力|超声|传感器|天线|语音|麦克风|扬声器|降噪|显示[屏频]|拓展坞|4G|H30|G70|AFM711/.test(name))return 'perception';
  if(/轮胎|驱动总成|后桥|电机驱动|转向|制动|作业灯|液压|水泵|割刀/.test(name))return 'actuator';
  if(/电池|电源|充电|回充|急停|继电器|控制模块|分线器|接收器|风扇|防撞|电量/.test(name))return 'support';
  return 'structure';
}
export function createRobotDescription(modelId){
  if(!Object.hasOwn(measurements,modelId))throw new Error('不支持的装配模型。');
  return {format:'FGROBOT/1',modelId,name:'阿克曼农机',author:'刘泽禹',packageName:modelId==='ackermann-independent-v1'?'fieldgate_standard_description':'fieldgate_extended_description',forward:'-y',roles:{},joints:{}};
}
export function partRole(part,draft){return draft?.roles?.[part.id]??defaultPartRole(part);}
export function robotMeasurements(modelId){if(!Object.hasOwn(measurements,modelId))throw new Error('装配尺寸不可用。');return measurements[modelId];}
export function robotYaw(draft){return {'-y':Math.PI/2,'+y':-Math.PI/2,'+x':0,'-x':Math.PI}[draft.forward];}
export function robotPoint(point,draft){
  const base=robotMeasurements(draft.modelId).origin,yaw=robotYaw(draft),x=point[0]-base[0],y=point[1]-base[1];
  return [x*Math.cos(yaw)-y*Math.sin(yaw),x*Math.sin(yaw)+y*Math.cos(yaw),point[2]-base[2]].map(v=>Number(v.toFixed(9)));
}
export function jointSettings(part,draft){
  return draft.joints[part.id]??{name:part.id.replace('-','_'),parent:'base_link',type:'fixed',xyz:robotPoint(robotMeasurements(draft.modelId).parts[part.id],draft),rpy:[0,0,0],axis:[0,0,0],lower:null,upper:null,effort:null,velocity:null,confirmed:false,optical:false};
}
export function robotLinks(draft,parts){return parts.filter(p=>['actuator','perception'].includes(partRole(p,draft)) || Object.hasOwn(draft.joints,p.id));}
function object(value){return !!value && typeof value==='object'&&!Array.isArray(value);}
function vector(value,max){return Array.isArray(value)&&value.length===3&&value.every(v=>Number.isFinite(v)&&Math.abs(v)<=max);}
export function validateRobotDescription(draft,parts,{exporting=false}={}){
  if(!object(draft)||draft.format!=='FGROBOT/1'||!Object.hasOwn(measurements,draft.modelId))throw new Error('机器人描述格式或模型不正确。');
  if(!['-y','+y','+x','-x'].includes(draft.forward))throw new Error('请选择模型前向。');
  for(const key of ['name','author'])if(typeof draft[key]!=='string'||!draft[key].trim()||draft[key].length>64||/[\u0000-\u001f]/.test(draft[key]))throw new Error('请填写有效的名称与作者。');
  if(typeof draft.packageName!=='string'||!/^[a-z][a-z0-9_]{0,62}$/.test(draft.packageName))throw new Error('描述包名称使用小写字母、数字和下划线，以字母开头。');
  if(!object(draft.roles)||!object(draft.joints))throw new Error('部件设置格式不正确。');
  const ids=new Set(parts.map(p=>p.id));
  for(const [id,role]of Object.entries(draft.roles))if(!ids.has(id)||!Object.hasOwn(roleNames,role))throw new Error('部件分类包含未知项目。');
  for(const id of Object.keys(draft.joints))if(!ids.has(id))throw new Error('关节引用了不存在的模型部件。');
  const links=robotLinks(draft,parts),active=new Set(links.map(p=>p.id)),names=new Set(['base_link']);
  for(const part of links){
    const j=jointSettings(part,draft);
    if(!object(j)||typeof j.name!=='string'||!/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(j.name)||names.has(j.name))throw new Error('连杆名称必须有效且不能重复。');
    names.add(j.name);
    if(j.optical){if(names.has(j.name+'_optical'))throw new Error('相机坐标系名称重复。');names.add(j.name+'_optical');}
    if(!['fixed','revolute','continuous','prismatic'].includes(j.type)||!vector(j.xyz,100)||!vector(j.rpy,Math.PI*2)||!vector(j.axis,1)||typeof j.confirmed!=='boolean'||typeof j.optical!=='boolean')throw new Error('请检查关节类型、位置、角度和转轴参数。');
    if(j.parent!=='base_link'&&!active.has(j.parent))throw new Error('安装对象不是有效连杆。');
    if(exporting&&j.optical&&!j.confirmed)throw new Error(`${part.name}：请先核对相机安装坐标。`);
    for(const key of ['lower','upper','effort','velocity'])if(j[key]!==null&&(!Number.isFinite(j[key])||Math.abs(j[key])>1000000))throw new Error('关节限值必须是有限数值。');
    if(exporting&&j.type!=='fixed'){
      if(!j.confirmed)throw new Error(`${part.name}：请先核对关节参数。`);
      if(Math.hypot(...j.axis)<1e-6)throw new Error(`${part.name}：请填写有效的运动轴方向。`);
      if(!(j.effort>0&&j.velocity>0))throw new Error(`${part.name}：请填写正的力/力矩和速度限值。`);
      if(j.type!=='continuous'&&!(j.lower!==null&&j.upper!==null&&j.lower<j.upper))throw new Error(`${part.name}：请填写正确的运动范围。`);
    }
  }
  for(const part of links){
    const seen=new Set();let id=part.id;
    while(id!=='base_link'){
      if(seen.has(id))throw new Error('安装关系存在循环，请检查上级连杆。');
      seen.add(id);id=jointSettings(parts.find(p=>p.id===id),draft).parent;
    }
  }
  if(JSON.stringify(draft).length>128*1024)throw new Error('机器人描述过大。');
  return draft;
}
export function readRobotDescription(raw,modelId,parts){
  if(!raw)return createRobotDescription(modelId);
  if(raw.length>128*1024)throw new Error('机器人描述过大。');
  const value=JSON.parse(raw);
  if(value.modelId!==modelId)throw new Error('描述与当前装配模型不匹配。');
  return validateRobotDescription(value,parts);
}
