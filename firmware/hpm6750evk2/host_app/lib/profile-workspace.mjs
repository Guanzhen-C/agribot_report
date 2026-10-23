import original from '../../profiles/three-in-one-original.json' with { type: 'json' };
import ackermann from '../../profiles/ackermann-four-lights.example.json' with { type: 'json' };
import differential from '../../profiles/differential-work.example.json' with { type: 'json' };
import { compileMachineProfile, validateMachineProfile } from './machine-profile.mjs';

export const DRAFT_STORAGE_KEY = 'fieldgate.machine-drafts.v1';
export const MODEL_ID = 'ackermann-independent-v1';
export const modelDefinitions = [
  { id: MODEL_ID, name: '标准装配', file: 'ackermann' },
  { id: 'ackermann-flagship-v1', name: '扩展装配', file: 'ackermann-flagship' },
];
export const profileTemplates = [
  { id: 'ackermann', name: '阿克曼 · 四路照明', profile: ackermann },
  { id: 'original', name: '原三合一 · 履带差速', profile: original },
  { id: 'differential', name: '轮式差速 · 作业设备', profile: differential },
];
const forbidden = /stm32|小车|底盘|地盘|禾川/i;
export function displayName(value) {
  if (typeof value !== 'string' || !value.trim() || value.length > 64 || forbidden.test(value)) throw new Error('请填写 1–64 字的农机或部件名称，不使用旧产品命名。');
  return value.trim();
}
export function createProfileDraft(templateId, name, id) {
  const template = profileTemplates.find(item => item.id === templateId);
  if (!template) throw new Error('请选择已有的农机结构。');
  const profile = structuredClone(template.profile);
  profile.identity = { ...profile.identity, id, name: displayName(name), revision: 1, commissioned: false };
  validateWorkspaceProfile(profile);
  return profile;
}
export function modelBindings(profile, modelId = MODEL_ID) {
  const models = profile.extensions?.['fieldgate:models'];
  if (models?.[modelId]) return models[modelId];
  const extension = profile.extensions?.['fieldgate:model'];
  return extension?.modelId === modelId ? extension.bindings : {};
}
export function validateWorkspaceProfile(profile) {
  validateMachineProfile(profile);
  displayName(profile.identity.name);
  displayName(profile.identity.author);
  profile.devices.forEach(device => displayName(device.name));
  const legacy = profile.extensions?.['fieldgate:model'];
  const models = profile.extensions?.['fieldgate:models'] ?? {};
  if (!models || Array.isArray(models) || typeof models !== 'object') throw new Error('模型关联格式不正确。');
  const collections = [...Object.entries(models), ...(legacy ? [[legacy.modelId, legacy.bindings]] : [])];
  let count = 0;
  for (const [modelId, bindings] of collections) {
    if (!modelDefinitions.some(model => model.id === modelId) || !bindings || Array.isArray(bindings) || typeof bindings !== 'object') throw new Error('模型关联格式不正确。');
    count += Object.keys(bindings).length;
    if (count > 64) throw new Error('单个草稿最多关联 64 个部件。');
    for (const [partId, deviceId] of Object.entries(bindings)) {
      if (!/^part-\d{4}$/.test(partId) || !profile.devices.some(item => item.id === deviceId)) throw new Error('部件关联了不存在的配置项。请重新选择对应功能。');
    }
  }
  compileMachineProfile(profile); // Also check embedded JSON + executable table size.
  return profile;
}
export function bindModelPart(profile, partId, deviceId, modelId = MODEL_ID) {
  const next = structuredClone(profile);
  const bindings = { ...modelBindings(next, modelId) };
  if (deviceId) bindings[partId] = deviceId; else delete bindings[partId];
  const legacy = next.extensions?.['fieldgate:model'];
  next.extensions = { ...next.extensions, 'fieldgate:models': { ...(legacy ? {[legacy.modelId]:legacy.bindings} : {}), ...next.extensions?.['fieldgate:models'], [modelId]: bindings } };
  delete next.extensions['fieldgate:model'];
  next.identity.commissioned = false;
  return validateWorkspaceProfile(next);
}
export function decodeDrafts(raw) {
  if (!raw) return [];
  if (raw.length > 256 * 1024) throw new Error('本机草稿过大，请先导出备份。');
  const records = JSON.parse(raw);
  if (!Array.isArray(records) || records.length > 12) throw new Error('本机草稿格式不正确。');
  const ids = new Set();
  for (const profile of records) {
    validateWorkspaceProfile(profile);
    if (ids.has(profile.identity.id)) throw new Error('草稿编号重复。');
    ids.add(profile.identity.id);
  }
  return records;
}
export function saveDraft(records, profile) {
  const next = structuredClone(profile);
  next.identity.commissioned = false;
  const previous = records.find(item => item.identity.id === next.identity.id);
  if (previous) next.identity.revision = previous.identity.revision + 1;
  validateWorkspaceProfile(next);
  const result = [next, ...records.filter(item => item.identity.id !== next.identity.id)];
  decodeDrafts(JSON.stringify(result));
  return result;
}
