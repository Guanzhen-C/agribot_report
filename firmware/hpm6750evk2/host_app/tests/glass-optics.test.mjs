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
test('navigation delegates optics to the pinned upstream package, not a local renderer', () => {
  const pkg = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));
  assert.equal(pkg.dependencies['simple-liquid-glass'], '5.3.0');
  assert.equal(pkg.dependencies['liquid-glass-react'], undefined);
  assert.match(source, /import LiquidGlass from 'simple-liquid-glass'/);
  assert.match(source, /<LiquidGlass/);
  assert.doesNotMatch(source, /makeGlassMap|feDisplacementMap|createElement\('canvas'\)|requestAnimationFrame/);
  assert.match(source, /liquid=\{false\}/);
  assert.match(source, /renderer="svg"/);
});

test('SSR leaves the browser-only library unmounted and preference subscriptions clean up', () => {
  const exports = {};
  let vendorRendered = false;
  vm.runInNewContext(outputText, { exports, require: name => name === 'simple-liquid-glass' ? { default: () => { vendorRendered = true; throw new Error('Vendor rendered during SSR'); } } : require(name) });
  const html = renderToStaticMarkup(React.createElement(exports.GlassOptics, { id: 'fg-dock-lens' }));
  assert.equal(vendorRendered, false);
  assert.match(html, /aria-hidden="true"/);
  assert.doesNotMatch(html, /data-glass-provider/);
  assert.match(source, /removeEventListener\('change', changed\)/);
  assert.match(source, /prefers-reduced-transparency/);
  assert.match(source, /forced-colors/);
});
