import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import vm from 'node:vm';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

const require = createRequire(import.meta.url);
const source = readFileSync(new URL('../app/glass-optics.tsx', import.meta.url), 'utf8');
const { outputText } = ts.transpileModule(source, { compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.CommonJS } });
function load(stored = null, blocked = false, server = false) {
  const exports = {}, events = [], listeners = new Map();
  const react = server ? React : { ...React, useSyncExternalStore: (subscribe, snapshot) => snapshot(), useState: () => [false, () => {}] };
  const vendor = () => { throw new Error('SSR must not render vendor'); };
  vm.runInNewContext(outputText, {
    exports, Event: class { constructor(type) { this.type = type; } },
    localStorage: { getItem: () => stored, setItem: (key, value) => { if (blocked) throw new Error('Storage blocked'); stored = value; } },
    navigator: { userAgent: 'Chrome/153' }, CSS: { supports: () => true },
    window: { matchMedia: () => ({ matches: false }), dispatchEvent: e => events.push(e.type), addEventListener: (name, fn) => listeners.set(name, fn), removeEventListener: name => listeners.delete(name) },
    require: name => name === 'react' ? react : name === 'simple-liquid-glass' ? { default: vendor } : require(name),
  });
  return { ...exports, stored: () => stored, events };
}

test('SSR renders an accessible switch and no browser-only glass', () => {
  const { GlassEffectsSwitch, GlassOptics } = load(null, false, true);
  assert.match(renderToStaticMarkup(React.createElement(GlassEffectsSwitch)), /role="switch"/);
  assert.doesNotMatch(renderToStaticMarkup(React.createElement(GlassOptics, { id: 'dock' })), /data-glass-provider/);
});

for (const blocked of [false, true]) test(`switch unmounts optics and works with storage ${blocked ? 'blocked' : 'available'}`, () => {
  const api = load(null, blocked);
  assert.equal(api.GlassOptics({ id: 'dock' }).props['data-glass-provider'], 'simple-liquid-glass');
  const input = api.GlassEffectsSwitch().props.children[0].props.children[0];
  input.props.onChange({ target: { checked: false } });
  assert.equal(api.GlassOptics({ id: 'dock' }).props.children, false);
  assert.equal(api.GlassEffectsSwitch().props.children[0].props.children[0].props.checked, false);
  assert.equal(api.stored(), blocked ? null : 'off');
  assert.deepEqual(api.events, ['fieldgate:glass-effects']);
});

test('saved off choice is respected on a fresh module load; cards have no optical mounts', () => {
  const api = load('off');
  assert.equal(api.GlassOptics({ id: 'dock' }).props.children, false);
  for (const file of ['launchpad/launchpad', 'overview', 'devices/devices', 'diagnostics/diagnostics', 'preview/vehicle-preview', 'preview/profile-editor', 'robot/robot-editor', 'vcu-console']) {
    assert.doesNotMatch(readFileSync(new URL(`../app/${file}.tsx`, import.meta.url), 'utf8'), /CardGlass/);
  }
});
