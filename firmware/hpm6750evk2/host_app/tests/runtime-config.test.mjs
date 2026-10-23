import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

test('local development uses Node SSR and deployment builds retain the Worker adapter', async () => {
  const source = readFileSync(new URL('../vite.config.ts', import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } });
  const exports = {}, workerConfigs = [];
  const modules = {
    vinext: { default: () => 'vinext' },
    vite: { defineConfig: value => value },
    './.openai/hosting.json': { default: { d1: 'TEST_DB', r2: 'TEST_BUCKET' } },
    './build/sites-vite-plugin': { sites: () => 'sites' },
    '@cloudflare/vite-plugin': { cloudflare: config => { workerConfigs.push(config); return 'cloudflare'; } },
  };
  vm.runInNewContext(outputText, { exports, process: { env: {} }, require: name => { assert.ok(modules[name], name); return modules[name]; } });
  const dev = await exports.default({ command: 'serve', mode: 'development' });
  assert.deepEqual(Array.from(dev.plugins), ['vinext', 'sites']);
  assert.equal(workerConfigs.length, 0);
  const build = await exports.default({ command: 'build', mode: 'production' });
  assert.deepEqual(Array.from(build.plugins), ['vinext', 'sites', 'cloudflare']);
  assert.equal(workerConfigs.length, 1);
  assert.equal(workerConfigs[0].config.main, './worker/index.ts');
  assert.equal(workerConfigs[0].config.d1_databases[0].binding, 'TEST_DB');
  assert.equal(workerConfigs[0].config.r2_buckets[0].binding, 'TEST_BUCKET');
  assert.deepEqual(Array.from(workerConfigs[0].viteEnvironment.childEnvironments), ['ssr']);
});
