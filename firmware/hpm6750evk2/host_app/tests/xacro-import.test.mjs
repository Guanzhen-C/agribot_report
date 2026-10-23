import test from 'node:test';
import assert from 'node:assert/strict';
import {parseXacroArguments,isXacroFile} from '../lib/xacro-import.mjs';
import {readModelFiles} from '../lib/model-import.mjs';
test('Xacro arguments accept explicit values and defaults, reject duplicates/reserved names/oversize',()=>{
 const a=parseXacroArguments('# comment\nwheel_radius=0.35\n use_camera = true\nname=field gate');
 assert.equal(a.wheel_radius,'0.35');assert.equal(a.use_camera,'true');assert.equal(a.name,'field gate');assert.equal(Object.getPrototypeOf(a),null);
 assert.deepEqual(Object.keys(parseXacroArguments('')),[]);
 for(const input of ['bad line','a=1\na=2','__proto__=x','constructor=x','x'.repeat(8193)])assert.throws(()=>parseXacroArguments(input));
 assert.equal(isXacroFile('robot.urdf.xacro'),true);assert.equal(isXacroFile('robot.xcaro'),true);assert.equal(isXacroFile('robot.glb'),false);
});
test('Xacro file sets preserve includes and require explicit entry selection, not an arbitrary file',async()=>{
 for(const name of ['robot.xacro','robot.urdf.xacro','robot.xcaro']){const b=await readModelFiles([new File(['<robot/>'],name)]);assert.equal(b.entry,name);}
 const b=await readModelFiles([new File(['<robot/>'],'main.xacro'),new File(['<robot/>'],'wheel.xacro'),new File([new Uint8Array([1])],'wheel.glb')]);
 assert.equal(b.entry,'');assert.deepEqual(b.candidates,['main.xacro','wheel.xacro']);assert.equal(b.entries.size,3);
});
