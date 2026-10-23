import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { compileMachineProfile, readMachineProfilePackage, validateMachineProfile, decodeThreeInOneCommand, resolveProfileSignals, encodeMappedMessage } from '../lib/machine-profile.mjs';

const [action,input,output,...options]=process.argv.slice(2);
try {
  if(!input || !['validate','compile','inspect','simulate'].includes(action)) throw new Error('用法：profile-tool.mjs validate <json> | compile <json> <新文件.fgmp> [--deployment] | inspect <fgmp> | simulate <json> <CAN-ID:16位十六进制数据>');
  if(options.some(value=>value!=='--deployment')) throw new Error('未知选项');
  if(action==='inspect') {
    console.log(JSON.stringify(readMachineProfilePackage(new Uint8Array(await readFile(input))),null,2));
  } else {
    const source=await readFile(input,'utf8');
    if(Buffer.byteLength(source)>65536) throw new Error('输入描述文件超过 64 KiB');
    const profile=JSON.parse(source);
    const result=validateMachineProfile(profile,{deployment:options.includes('--deployment')});
    if(action==='compile') {
      if(!output || resolve(input)===resolve(output)) throw new Error('请指定与源文件不同的新输出文件');
      const bytes=compileMachineProfile(profile,{deployment:options.includes('--deployment')});
      await writeFile(output,bytes,{flag:'wx'});
      console.log(JSON.stringify({file:resolve(output),bytes:bytes.length,...result},null,2));
    } else if(action==='simulate') {
      if(!/^(?:0x)?[0-9a-fA-F]{1,3}:[0-9a-fA-F]{16}$/.test(output??'')) throw new Error('报文示例：0x514:0032320100000100');
      const [idText,hex]=output.split(':');
      const bytes=Uint8Array.from(hex.match(/../g).map(value=>parseInt(value,16)));
      const command=decodeThreeInOneCommand(parseInt(idText,16),bytes);
      const values=resolveProfileSignals(profile,command);
      const frames=profile.routing.mode==='passthrough'?[{id:parseInt(idText,16),bytes}]:profile.routing.messages.filter(item=>item.domain===command.domain).map(message=>({id:message.id,bytes:encodeMappedMessage(message,values,{counter:command.counter})}));
      console.log(JSON.stringify({mode:'offline preview only; no serial or CAN access',values,frames:frames.map(frame=>({id:`0x${frame.id.toString(16)}`,hex:Buffer.from(frame.bytes).toString('hex')})),warnings:result.warnings},null,2));
    } else console.log(JSON.stringify(result,null,2));
  }
} catch(error) { console.error(error.message); process.exitCode=1; }
