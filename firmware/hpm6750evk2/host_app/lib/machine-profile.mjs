import Ajv from 'ajv';
import schema from '../../profiles/fgmp-v1.schema.json' with { type: 'json' };
import { crc32 } from './serial-frame.mjs';

export const PROFILE_HEADER_SIZE = 128;
export const PROFILE_MESSAGE_SIZE = 176;
export const PROFILE_MAX_SIZE = 16384;
export const PROFILE_SOURCES = Object.freeze([
  'drive.leftPercent', 'drive.rightPercent', 'lighting.headlight',
  'work.fanGear', 'work.pumpGear', 'work.shredderX100',
  'drive.forwardMmS', 'drive.yawMradS', 'drive.leftSteeringMrad', 'drive.rightSteeringMrad',
]);
const machineTypes = ['tracked_differential', 'differential', 'ackermann'];
// Validation runs in the browser or offline CLI, never while rendering a page in a Worker.
let validateShape;
const encoder = new TextEncoder();
const decoder = new TextDecoder('utf-8', { fatal: true });

function fail(path, message) { throw new Error(`FGMP ${path}: ${message}`); }
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value !== null && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])]));
  return value;
}
export function fieldRange(field) {
  return field.signed ? [-(2 ** (field.bitLength - 1)), 2 ** (field.bitLength - 1) - 1] : [0, 2 ** field.bitLength - 1];
}

export function validateMachineProfile(profile, { deployment = false } = {}) {
  validateShape ??= new Ajv({ allErrors: true, ownProperties: true, strictKeywords: true }).compile(schema);
  if (!validateShape(profile)) fail('schema', new Ajv().errorsText(validateShape.errors, { separator: '; ' }));
  const { machine, routing, safety, identity } = profile;
  if (deployment && !identity.commissioned) fail('identity.commissioned', '尚未完成实机验证，禁止作为运行配置启用');
  if (machine.inputModel === 'calibrated_virtual_differential') {
    if (!machine.geometry || !machine.calibration) fail('machine', '百分比转运动指令需要显式几何与标定参数');
    if (machine.calibration.minimumMovingMmS > machine.calibration.wheelSpeedAt100PercentMmS) fail('machine.calibration', '低速阈值超过最大轮速');
  } else if (machine.geometry || machine.calibration || machine.type === 'ackermann') fail('machine', '阿克曼必须声明转换模型；原生百分比不接受被忽略的标定参数');
  if (routing.mode === 'passthrough' && (routing.messages.length || machine.inputModel !== 'native_percent')) fail('routing', '透传模式不得混入转换报文');
  if (routing.mode === 'mapped_can' && !routing.messages.length) fail('routing.messages', '映射模式至少需要一条输出报文');
  if (!safety.requiredFeedbackIds.includes(0x532)) fail('safety.requiredFeedbackIds', 'v1 必须接入原协议车辆安全状态 0x532');
  for (const id of safety.requiredFeedbackIds) if (!routing.feedbackPassthroughIds.includes(id)) fail('safety.requiredFeedbackIds', '安全反馈必须包含在接收白名单内');
  const ids = new Set();
  for (const [index, message] of routing.messages.entries()) {
    const path = `routing.messages[${index}]`;
    if (ids.has(message.id) || routing.feedbackPassthroughIds.includes(message.id)) fail(path, '输出 ID 重复或与反馈 ID 冲突');
    ids.add(message.id);
    const occupied = new Set();
    if (message.counter === 'nibble6') for (let bit = 48; bit < 52; bit++) occupied.add(bit);
    if (message.checksum === 'xor7') for (let bit = 56; bit < 64; bit++) occupied.add(bit);
    for (const field of message.fields) {
      if (field.startBit + field.bitLength > 64) fail(path, '信号超出 8 字节');
      if (field.byteOrder === 'big_byte_aligned' && (field.startBit % 8 || ![8,16].includes(field.bitLength))) fail(path, '大端字段须整字节对齐；此规则不使用 DBC Motorola 起始位编号');
      if (field.source.startsWith('work.') !== (message.domain === 'work')) fail(path, '同一报文不能混合独立更新的行走与作业输入');
      const sourceId = PROFILE_SOURCES.indexOf(field.source);
      if (sourceId >= 6 && machine.inputModel !== 'calibrated_virtual_differential') fail(path, '禁止将原始百分比直接作为速度或转角');
      if (sourceId >= 8 && machine.type !== 'ackermann') fail(path, '转角源仅用于阿克曼结构');
      for (let bit = field.startBit; bit < field.startBit + field.bitLength; bit++) {
        if (occupied.has(bit)) fail(path, '信号与其他字段、计数器或校验位重叠');
        occupied.add(bit);
      }
      const [min,max] = fieldRange(field);
      if (field.safeRaw < min || field.safeRaw > max) fail(path, '安全输出超出字段表示范围');
    }
  }
  const devices = new Set();
  for (const device of profile.devices) {
    if (devices.has(device.id)) fail('devices', '设备标识重复');
    devices.add(device.id);
    if ((device.feedback === 'native_three_in_one') !== (device.feedbackFrameId !== undefined)) fail('devices', '反馈能力与反馈报文必须同时声明');
    if (device.feedbackFrameId !== undefined && !routing.feedbackPassthroughIds.includes(device.feedbackFrameId)) fail('devices', '反馈报文不在接收白名单中');
    if (routing.mode === 'mapped_can' && device.control !== 'none') {
      const message = routing.messages.find(item => item.id === device.outputMessageId);
      if (!message || !message.fields.some(field => field.source === device.control && (device.channel === undefined || field.startBit === device.channel))) fail('devices', '设备控制源没有对应输出字段');
    } else if (device.outputMessageId !== undefined || device.channel !== undefined) fail('devices', '当前设备没有可绑定的映射报文');
  }
  const warnings = [];
  if (!identity.commissioned) warnings.push('未完成实机验证：生成的数据包保持输出禁止。');
  if (profile.devices.filter(item => item.category === 'light').length > 1) warnings.push('原三合一只有一个灯光开关：多个灯跟随联动，不具备独立输入控制。');
  if (machine.inputModel !== 'native_percent') warnings.push('百分比到速度使用显式标定模型，不代表原协议提供了速度，也不代表闭环标定已完成。');
  if (routing.mode === 'mapped_can') warnings.push('v1 支持固定 CAN 映射及原协议反馈透传；特殊握手和异构反馈转换需要新增已验证适配器。');
  return { valid: true, warnings };
}

export function compileMachineProfile(profile, options = {}) {
  validateMachineProfile(profile, options);
  const source = encoder.encode(JSON.stringify(canonical(profile)));
  const compiledLength = PROFILE_HEADER_SIZE + profile.routing.messages.length * PROFILE_MESSAGE_SIZE;
  const bytes = new Uint8Array(compiledLength + source.length);
  if (bytes.length > PROFILE_MAX_SIZE) fail('size', '描述文件和执行表合计超过 16 KiB');
  const view = new DataView(bytes.buffer);
  const u16 = (at, value) => view.setUint16(at, value, true);
  const u32 = (at, value) => view.setUint32(at, value, true);
  bytes.set(encoder.encode('FGMP')); u16(4,1); u16(6,PROFILE_HEADER_SIZE); u32(8,bytes.length);
  bytes.set(profile.identity.id.replaceAll('-', '').match(/../g).map(value => parseInt(value,16)),16);
  u32(32,profile.identity.revision); bytes[36]=machineTypes.indexOf(profile.machine.type);
  bytes[37]=profile.routing.mode === 'mapped_can' ? 1 : 0; bytes[38]=Number(profile.identity.commissioned); bytes[39]=1;
  u32(40,profile.upstream.bitrate); u32(44,profile.downstream.bitrate);
  u16(48,profile.safety.commandTimeoutMs); u16(50,profile.safety.feedbackTimeoutMs);
  u16(52,profile.machine.geometry?.trackMm ?? 0); u16(54,profile.machine.geometry?.wheelbaseMm ?? 0);
  u16(56,profile.machine.calibration?.wheelSpeedAt100PercentMmS ?? 0);
  u16(58,profile.machine.calibration?.maxSteeringMrad ?? 0); u16(60,profile.machine.calibration?.minimumMovingMmS ?? 0);
  bytes[62]=profile.machine.inputModel === 'calibrated_virtual_differential' ? 1 : 0;
  bytes[63]=profile.routing.messages.length; bytes[64]=profile.routing.feedbackPassthroughIds.length; bytes[65]=profile.safety.requiredFeedbackIds.length;
  profile.routing.feedbackPassthroughIds.forEach((id,i)=>u16(68+i*2,id));
  profile.safety.requiredFeedbackIds.forEach((id,i)=>u16(100+i*2,id));
  u32(116,source.length); u32(120,compiledLength);
  profile.routing.messages.forEach((message,i)=>{
    const at=PROFILE_HEADER_SIZE+i*PROFILE_MESSAGE_SIZE;
    u16(at,message.id); bytes[at+2]=message.domain === 'work' ? 1 : 0;
    bytes[at+3]=message.counter === 'nibble6' ? 1 : 0; bytes[at+4]=message.checksum === 'xor7' ? 1 : 0;
    bytes[at+5]=message.fields.length; bytes.set(message.base,at+8);
    message.fields.forEach((field,j)=>{
      const pos=at+16+j*20;
      bytes[pos]=PROFILE_SOURCES.indexOf(field.source); bytes[pos+1]=field.startBit; bytes[pos+2]=field.bitLength;
      bytes[pos+3]=Number(field.signed)|(field.byteOrder === 'big_byte_aligned' ? 2 : 0);
      view.setInt32(pos+4,field.numerator,true); u32(pos+8,field.denominator);
      view.setInt32(pos+12,field.offset,true); view.setInt32(pos+16,field.safeRaw,true);
    });
  });
  bytes.set(source,compiledLength); u32(12,crc32(bytes.subarray(16)));
  return bytes;
}

export function readMachineProfilePackage(bytes) {
  if (!(bytes instanceof Uint8Array) || bytes.length < PROFILE_HEADER_SIZE || bytes.length > PROFILE_MAX_SIZE) fail('package','长度错误');
  const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);
  if (decoder.decode(bytes.subarray(0,4)) !== 'FGMP' || view.getUint16(4,true)!==1 || view.getUint16(6,true)!==PROFILE_HEADER_SIZE) fail('package','不支持的版本');
  if (view.getUint32(8,true)!==bytes.length || view.getUint32(12,true)!==crc32(bytes.subarray(16))) fail('package','长度或校验失败');
  const start=view.getUint32(120,true), length=view.getUint32(116,true);
  if (start<PROFILE_HEADER_SIZE || start+length!==bytes.length) fail('package','描述源边界错误');
  const profile=JSON.parse(decoder.decode(bytes.subarray(start)));
  const expected=compileMachineProfile(profile);
  if (expected.length!==bytes.length || expected.some((value,index)=>value!==bytes[index])) fail('package','描述源与执行表不一致');
  return profile;
}

export function decodeThreeInOneCommand(id, bytes) {
  if (!(bytes instanceof Uint8Array) || bytes.length!==8 || bytes.subarray(0,7).reduce((a,b)=>a^b,0)!==bytes[7]) fail('command','长度或异或校验错误');
  const signed=value=>value>127?value-256:value;
  if (id===0x514) {
    const left=signed(bytes[1]),right=signed(bytes[2]);
    if (Math.abs(left)>100 || Math.abs(right)>100) fail('command','行走百分比越界');
    return { domain:'drive',counter:bytes[6]&15,values:{'drive.leftPercent':left,'drive.rightPercent':right,'lighting.headlight':bytes[3]&1} };
  }
  if (id===0x580) {
    const fan=bytes[0]&15,pump=bytes[0]>>4,shredder=bytes[1]|bytes[2]<<8;
    if(fan>8 || pump>8 || shredder>10000) fail('command','作业档位或百分比越界');
    return { domain:'work',counter:bytes[6]&15,values:{'work.fanGear':fan,'work.pumpGear':pump,'work.shredderX100':shredder} };
  }
  fail('command','非三合一控制报文');
}

export function resolveProfileSignals(profile, command) {
  const values={...command.values};
  if (command.domain!=='drive' || profile.machine.inputModel==='native_percent') return values;
  const { geometry:g,calibration:c,type }=profile.machine;
  const left=values['drive.leftPercent']*c.wheelSpeedAt100PercentMmS/100;
  const right=values['drive.rightPercent']*c.wheelSpeedAt100PercentMmS/100;
  let forward=(left+right)/2, yaw=(right-left)/g.trackMm, leftAngle=0,rightAngle=0;
  if(type==='ackermann') {
    if(Math.abs(forward)<c.minimumMovingMmS) {
      if(Math.abs(yaw)>1e-9) fail('command','阿克曼不支持原地差速旋转');
      forward=0; yaw=0;
    } else {
      const limit=Math.tan(c.maxSteeringMrad/1000)/(g.wheelbaseMm+g.trackMm/2*Math.tan(c.maxSteeringMrad/1000));
      const curvature=Math.max(-limit,Math.min(limit,yaw/forward));
      yaw=curvature*forward;
      leftAngle=Math.atan(g.wheelbaseMm*curvature/(1-g.trackMm*curvature/2));
      rightAngle=Math.atan(g.wheelbaseMm*curvature/(1+g.trackMm*curvature/2));
    }
  }
  return {...values,'drive.forwardMmS':Math.trunc(forward)+0,'drive.yawMradS':Math.trunc(yaw*1000)+0,'drive.leftSteeringMrad':Math.trunc(leftAngle*1000)+0,'drive.rightSteeringMrad':Math.trunc(rightAngle*1000)+0};
}

export function encodeMappedMessage(message, values, { safe=false,counter=0 }={}) {
  const bytes=Uint8Array.from(message.base);
  for(const field of message.fields) {
    if(!safe && !Number.isFinite(values[field.source])) fail('mapping','控制源未获取');
    const raw=safe?field.safeRaw:Math.trunc(values[field.source]*field.numerator/field.denominator)+field.offset;
    const [min,max]=fieldRange(field);
    if(raw<min || raw>max) fail('mapping','输出溢出，禁止截断或回绕');
    const bits=raw<0?raw+2**field.bitLength:raw;
    for(let i=0;i<field.bitLength;i++) {
      const target=field.byteOrder==='little'?field.startBit+i:field.startBit+(field.bitLength/8-1-Math.floor(i/8))*8+i%8;
      bytes[target>>3]=(bytes[target>>3]&~(1<<(target&7)))|((Math.floor(bits/2**i)&1)<<(target&7));
    }
  }
  if(message.counter==='nibble6') bytes[6]=(bytes[6]&0xf0)|(counter&15);
  if(message.checksum==='xor7') bytes[7]=bytes.subarray(0,7).reduce((a,b)=>a^b,0);
  return bytes;
}
