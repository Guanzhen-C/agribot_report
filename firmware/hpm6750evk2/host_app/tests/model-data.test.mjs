import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { robotMeasurements } from '../lib/robot-description.mjs';

test('SSR imports module data, while published model measurements remain byte-identical', () => {
  const source = readFileSync(new URL('../lib/robot-description.mjs', import.meta.url), 'utf8');
  assert.doesNotMatch(source, /from\s+['"][^'"]*public\//);
  for (const [file, id] of [['ackermann', 'ackermann-independent-v1'], ['ackermann-flagship', 'ackermann-flagship-v1']]) {
    const moduleData = readFileSync(new URL(`../lib/model-data/${file}.kinematics.json`, import.meta.url));
    const published = readFileSync(new URL(`../public/models/${file}.kinematics.json`, import.meta.url));
    assert.deepEqual(moduleData, published);
    assert.deepEqual(robotMeasurements(id), JSON.parse(published));
  }
  assert.throws(() => robotMeasurements('unknown'), /装配尺寸不可用/);
});
