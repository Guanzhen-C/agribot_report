import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

const require = createRequire(import.meta.url);
const source = readFileSync(new URL('../app/connection-support.ts', import.meta.url), 'utf8');
const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } });
function load(secure, serial, server = false) {
  const exports = {};
  vm.runInNewContext(outputText, { exports, window: { isSecureContext: secure }, navigator: { serial },
    require: name => name === 'react' ? { useSyncExternalStore: (subscribe, snapshot, serverSnapshot) => server ? serverSnapshot() : snapshot() } : require(name),
  });
  return exports;
}

test('connection support distinguishes SSR, insecure HTTP, unsupported browser and available serial', () => {
  for (const [secure, serial, server, expected] of [[true, {}, true, 'checking'], [false, undefined, false, 'insecure'], [true, undefined, false, 'unsupported'], [true, {}, false, 'available']]) {
    const api = load(secure, serial, server);
    assert.equal(api.useConnectionSupport(), expected);
    const label = api.connectionLabel(false, false, expected);
    if (expected !== 'available') assert.doesNotMatch(label, /未连接设备/);
  }
});

test('opened serial and preview stay distinct from vehicle feedback and unavailable access', () => {
  const api = load(true, {});
  assert.equal(api.connectionLabel(false, false, 'available'), '未连接设备');
  assert.equal(api.connectionLabel(false, true, 'available'), '串口已打开');
  assert.equal(api.connectionLabel(true, true, 'available'), '预览模式');
  assert.doesNotMatch(api.connectionLabel(false, true, 'available'), /正常|已确认/);
  assert.match(api.connectionHelp('insecure'), /localhost/);
  assert.match(api.connectionHelp('insecure'), /不会共享/);
  assert.match(api.connectionHelp('unsupported'), /Chrome 或 Edge/);
});
