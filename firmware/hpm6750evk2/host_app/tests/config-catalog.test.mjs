import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

test("host catalogue and firmware IDs remain aligned with existing selections", async () => {
  const source = await readFile(new URL("../lib/config-catalog.ts", import.meta.url), "utf8");
  const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
  const { BUILTIN_MACHINES: machines, BUILTIN_PROTOCOLS: protocols, BUILTIN_COMPUTE_INTERFACES: interfaces } =
    await import(`data:text/javascript;base64,${Buffer.from(compiled).toString("base64")}`);
  const header = await readFile(new URL("../../firmware/vcu_configurator/src/vcu_machine_profile.h", import.meta.url), "utf8");
  const constants = new Map([...header.matchAll(/#define (VCU_(?:MACHINE|PROTOCOL)_\w+) (0x[0-9A-F]+)U/g)]
    .map(([, key, value]) => [key, Number(value)]));
  const expected = [
    ["hechuan_differential", "DIFFERENTIAL", "MOTION", 1000000],
    ["hechuan_four_wheel", "FOUR_WHEEL", "MOTION", 1000000],
    ["hechuan_steering", "STEERING", "STEERING", 1000000],
    ["hechuan_mecanum", "MECANUM", "MOTION", 1000000],
    ["hechuan_omni", "OMNI", "MOTION", 1000000],
    ["tracked_three_in_one", "TRACKED", "IMPLEMENT", 250000],
  ];
  assert.equal(machines.length, expected.length);
  assert.equal(new Set(machines.map(item => item.id)).size, machines.length);
  assert.equal(new Set(protocols.map(item => item.id)).size, protocols.length);
  assert.deepEqual(interfaces.map(item => item.id).sort(), [0, 1, 2]);
  for (const [key, machineConstant, protocolConstant, bitrate] of expected) {
    const machine = machines.find(item => item.key === key);
    const matching = protocols.filter(item => item.compatibleMachineKeys.includes(key));
    assert.equal(machine.id, constants.get(`VCU_MACHINE_${machineConstant}`));
    assert.equal(matching.length, 1);
    assert.equal(matching[0].id, constants.get(`VCU_PROTOCOL_${protocolConstant}`));
    assert.equal(matching[0].canBitrate, bitrate);
    assert.match(machine.label, /农机/);
    assert.doesNotMatch(machine.label, /禾川|STM32|小车|底盘|地盘/i);
  }
  const original = protocols.find(item => item.key === 'three_in_one_can_v1');
  assert.equal(original.id, 0x0101);
  assert.equal(original.label, '三合一 CAN 协议');
  assert.equal(original.version, '2026-08-26 / v1');
});
