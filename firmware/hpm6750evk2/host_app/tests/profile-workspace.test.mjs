import assert from 'node:assert/strict';
import test from 'node:test';
import { bindModelPart, createProfileDraft, decodeDrafts, modelBindings, modelDefinitions, profileTemplates, saveDraft, validateWorkspaceProfile } from '../lib/profile-workspace.mjs';
import { compileMachineProfile, readMachineProfilePackage } from '../lib/machine-profile.mjs';

test('new drafts preserve upstream, authorship and originals; saves increment revision and remain disarmed',()=>{
  for(const template of profileTemplates){
    const before=JSON.stringify(template.profile);
    const created=createProfileDraft(template.id,'联试农机',crypto.randomUUID());
    assert.equal(created.identity.author,'刘泽禹');
    assert.deepEqual(created.upstream,template.profile.upstream);
    assert.deepEqual(created.routing,template.profile.routing);
    let saved=saveDraft([],created);assert.equal(saved.length,1);assert.equal(saved[0].identity.revision,1);
    created.identity.commissioned=true;saved=saveDraft(saved,created);
    assert.equal(saved[0].identity.revision,2);assert.equal(saved[0].identity.commissioned,false);
    assert.equal(JSON.stringify(template.profile),before);
    assert.deepEqual(decodeDrafts(JSON.stringify(saved)),saved);
  }
});

test('part bindings round-trip through executable package, retain independent models, and can be removed',()=>{
  const draft=createProfileDraft('ackermann','四灯农机',crypto.randomUUID());
  let bound=bindModelPart(draft,'part-0001','light_1');
  bound=bindModelPart(bound,'part-0001','drive',modelDefinitions[1].id);
  assert.equal(modelBindings(bound)['part-0001'],'light_1');
  assert.equal(modelBindings(bound,modelDefinitions[1].id)['part-0001'],'drive');
  assert.deepEqual(readMachineProfilePackage(compileMachineProfile(bound)),bound);
  bound=bindModelPart(bound,'part-0001','');
  assert.deepEqual(modelBindings(bound),{});
  assert.equal(modelBindings(bound,modelDefinitions[1].id)['part-0001'],'drive');
  assert.deepEqual(modelBindings(draft),{});
  assert.throws(()=>bindModelPart(bound,'part-0001','missing'));
  assert.throws(()=>bindModelPart(bound,'../../bad','drive'));
  assert.throws(()=>bindModelPart(bound,'part-0001','drive','unknown-model'));
});

test('invalid local storage and invalid UI names cannot replace a good draft collection',()=>{
  const draft=createProfileDraft('ackermann','农机一',crypto.randomUUID());
  const saved=saveDraft([],draft), before=JSON.stringify(saved);
  for(const raw of ['{}','null','broken',JSON.stringify([draft,draft]),'x'.repeat(300000)])assert.throws(()=>decodeDrafts(raw));
  for(const name of ['', 'x'.repeat(65)])assert.throws(()=>createProfileDraft('ackermann',name,crypto.randomUUID()));
  const invalid=structuredClone(draft);invalid.machine.geometry.trackMm=0;
  assert.throws(()=>saveDraft(saved,invalid));assert.equal(JSON.stringify(saved),before);
  invalid.machine.geometry.trackMm=1200;invalid.extensions['fieldgate:models']={bad:{}};
  assert.throws(()=>validateWorkspaceProfile(invalid));
  assert.throws(()=>saveDraft(Array.from({length:12},()=>({...structuredClone(draft),identity:{...draft.identity,id:crypto.randomUUID()}})),{...draft,identity:{...draft.identity,id:crypto.randomUUID()}}));
});
