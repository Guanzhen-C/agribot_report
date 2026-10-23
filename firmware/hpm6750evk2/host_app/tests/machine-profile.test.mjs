import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { crc32 } from '../lib/serial-frame.mjs';
import { validateMachineProfile,compileMachineProfile,readMachineProfilePackage,decodeThreeInOneCommand,resolveProfileSignals,encodeMappedMessage } from '../lib/machine-profile.mjs';

const load=async name=>JSON.parse(await readFile(new URL(`../../profiles/${name}.json`,import.meta.url),'utf8'));
const native=await load('three-in-one-original');
const ackermann=await load('ackermann-four-lights.example');
const differential=await load('differential-work.example');
const frame=raw=>{const bytes=new Uint8Array(8);bytes.set(raw);bytes[7]=bytes.subarray(0,7).reduce((a,b)=>a^b,0);return bytes;};

test('all profile examples validate and deterministically round-trip full source, author, IDs and extensions',()=>{
  for(const profile of [native,ackermann,differential]) {
    assert.equal(validateMachineProfile(profile).valid,true);
    const bytes=compileMachineProfile(profile);
    assert.deepEqual(readMachineProfilePackage(bytes),profile);
    assert.deepEqual(bytes,compileMachineProfile(profile));
    assert.throws(()=>compileMachineProfile(profile,{deployment:true}),/尚未完成实机验证/);
    assert.equal(bytes[38],0);
  }
  assert.equal(native.identity.legacyProtocolId,0x101);
  assert.equal(native.extensions['source:protocol'].author,'MatrixCreat @WangHu');
  assert.equal(ackermann.devices.filter(item=>item.category==='light').length,4);
});

test('strict description rejects unsupported capabilities, conflicts, missing calibration and wrong units',()=>{
  const mutations=[
    p=>{p.format='FGMP/2';}, p=>{p.arbitraryCode='run';}, p=>{p.upstream.protocol='fgcp_v1';},
    p=>{p.machine.inputModel='native_percent';}, p=>{delete p.machine.geometry;},
    p=>{p.safety.requiredFeedbackIds=[];}, p=>{p.safety.commandTimeoutMs=2000;},
    p=>{p.routing.messages[1].id=p.routing.messages[0].id;},
    p=>{p.routing.messages[0].id=0x532;}, p=>{p.routing.messages[0].id=0x1000;},
    p=>{p.routing.messages[0].fields[0].source='unsupported.speed';},
    p=>{p.routing.messages[0].fields[0].startBit=1;},
    p=>{p.routing.messages[0].fields[1].startBit=0;},
    p=>{p.routing.messages[1].fields[0].startBit=48;},
    p=>{p.routing.messages[1].fields[0].startBit=56;},
    p=>{p.routing.messages[1].fields[0].safeRaw=2;},
    p=>{p.routing.messages[1].fields[0].numerator=0;},
    p=>{p.routing.messages[1].fields[0].denominator=0;},
    p=>{p.routing.messages[1].fields[0].source='work.fanGear';},
    p=>{p.devices[1].id=p.devices[0].id;},
    p=>{p.devices[0].outputMessageId=1;},
    p=>{p.devices[0].feedback='native_three_in_one';},
  ];
  for(const [index,mutate] of mutations.entries()) {const p=structuredClone(ackermann);mutate(p);assert.throws(()=>compileMachineProfile(p),/FGMP/,String(index));}
});

test('package corruption, truncation, oversized source and mismatched executable table are rejected',()=>{
  const bytes=compileMachineProfile(ackermann);
  assert.throws(()=>readMachineProfilePackage(bytes.subarray(0,bytes.length-1)),/校验|长度/);
  const corrupt=bytes.slice();corrupt[150]^=1;
  assert.throws(()=>readMachineProfilePackage(corrupt),/校验/);
  const mismatch=bytes.slice();new DataView(mismatch.buffer).setUint16(52,1300,true);
  new DataView(mismatch.buffer).setUint32(12,crc32(mismatch.subarray(16)),true);
  assert.throws(()=>readMachineProfilePackage(mismatch),/不一致/);
  const huge=structuredClone(native);huge.extensions['test:large']='a'.repeat(17000);
  assert.throws(()=>compileMachineProfile(huge),/16 KiB/);
});

test('three-in-one controls retain percent/gear semantics, reserved bytes and checksum boundaries',()=>{
  const decoded=decodeThreeInOneCommand(0x514,frame([0,206,50,1,0,0,15]));
  assert.equal(decoded.values['drive.leftPercent'],-50);
  assert.equal(decoded.counter,15);
  assert.equal(decoded.values['drive.forwardMmS'],undefined);
  assert.deepEqual(decodeThreeInOneCommand(0x580,frame([0x32,0xd2,4])).values,{'work.fanGear':2,'work.pumpGear':3,'work.shredderX100':1234});
  for(const [id,raw] of [[0x514,[0,101]],[0x580,[9]],[0x580,[0,0x11,0x27]]]) assert.throws(()=>decodeThreeInOneCommand(id,frame(raw)),/越界/);
});

test('same three-in-one input maps to straight/reverse Ackermann, linked lamps and differential yaw',()=>{
  const command=decodeThreeInOneCommand(0x514,frame([0,50,50,1]));
  const values=resolveProfileSignals(ackermann,command);
  assert.equal(values['drive.forwardMmS'],1000);assert.equal(values['drive.leftSteeringMrad'],0);
  assert.deepEqual([...encodeMappedMessage(ackermann.routing.messages[0],values)],[3,232,0,0,0,0,0,0]);
  assert.deepEqual([...encodeMappedMessage(ackermann.routing.messages[1],values,{counter:1})],[15,0,0,0,0,0,1,14]);
  const reverse=resolveProfileSignals(ackermann,decodeThreeInOneCommand(0x514,frame([0,206,206])));
  assert.equal(reverse['drive.forwardMmS'],-1000);assert.equal(reverse['drive.leftSteeringMrad'],0);
  const turn=decodeThreeInOneCommand(0x514,frame([0,40,60]));
  const ack=resolveProfileSignals(ackermann,turn),diff=resolveProfileSignals(differential,turn);
  assert.ok(ack['drive.leftSteeringMrad']>ack['drive.rightSteeringMrad']);
  assert.ok(ack['drive.leftSteeringMrad']<=600);assert.equal(diff['drive.yawMradS'],400);
  assert.throws(()=>resolveProfileSignals(ackermann,decodeThreeInOneCommand(0x514,frame([0,50,206]))),/原地/);
  const negativeTurn=resolveProfileSignals(ackermann,decodeThreeInOneCommand(0x514,frame([0,196,216])));
  assert.ok(negativeTurn['drive.leftSteeringMrad']<0);
});

test('bounded mapper handles scaling, offset, active-low safe values and overflow without wrapping',()=>{
  const message=structuredClone(ackermann.routing.messages[1]);
  message.fields=message.fields.slice(0,1);message.fields[0].numerator=-1;message.fields[0].offset=1;message.fields[0].safeRaw=1;
  assert.equal(encodeMappedMessage(message,{'lighting.headlight':1})[0],0);
  assert.equal(encodeMappedMessage(message,{}, {safe:true})[0],1);
  assert.throws(()=>encodeMappedMessage(message,{}),/未获取/);
  assert.throws(()=>encodeMappedMessage(message,{'lighting.headlight':3}),/溢出/);
});
