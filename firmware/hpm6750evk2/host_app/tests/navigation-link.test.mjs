import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import vm from 'node:vm';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import ts from 'typescript';

const require = createRequire(import.meta.url);
const source = readFileSync(new URL('../app/navigation-link.tsx', import.meta.url), 'utf8');
const { outputText } = ts.transpileModule(source, { compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.CommonJS } });
function load(react, crypto) {
  const exports = {};
  const nextLink = () => { throw new Error('Next Link must not render on the server or LAN HTTP'); };
  vm.runInNewContext(outputText, { exports, crypto, require: name => name === 'react' ? react : name === 'next/link' ? { default: nextLink } : require(name) });
  return { NavigationLink: exports.default, nextLink };
}

test('SSR emits an ordinary link with no browser APIs', () => {
  const { NavigationLink } = load(React, undefined);
  assert.equal(renderToStaticMarkup(React.createElement(NavigationLink, { href: '/preview?preview=1' }, 'Preview')), '<a href="/preview?preview=1">Preview</a>');
});

for (const [name, crypto, enhanced] of [['no crypto', undefined, false], ['LAN HTTP', {}, false], ['localhost / HTTPS', { subtle: { digest() {} } }, true]]) {
  test(`${name}: navigation preserves href, children and the caller guard`, () => {
    const { NavigationLink, nextLink } = load({ ...React, useSyncExternalStore: (subscribe, snapshot) => { assert.equal(typeof subscribe(), 'function'); return snapshot(); } }, crypto);
    const onClick = event => event.preventDefault();
    const element = NavigationLink({ href: '/settings?preview=1', children: 'Settings', onClick, 'aria-disabled': true });
    assert.equal(element.type, enhanced ? nextLink : 'a');
    assert.equal(element.props.href, '/settings?preview=1');
    assert.equal(element.props.children, 'Settings');
    assert.equal(element.props['aria-disabled'], true);
    assert.equal(element.props.onClick, onClick);
    let prevented = false;
    element.props.onClick({ preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
  });
}
