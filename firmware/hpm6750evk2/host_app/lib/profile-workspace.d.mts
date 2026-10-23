export interface MachineProfile {
  format: string;
  identity: {id: string; name: string; author: string; revision: number; commissioned: boolean; [key: string]: unknown};
  machine: { type: string; inputModel: string; geometry?: {trackMm: number; wheelbaseMm: number}; calibration?: {wheelSpeedAt100PercentMmS: number; maxSteeringMrad: number; minimumMovingMmS: number}; };
  devices: { id: string; name: string; category: string; control: string; feedback: string; outputMessageId?: number; channel?: number; [key: string]: unknown }[];
  extensions: Record<string, unknown>;
  [key: string]: unknown;
}
export const DRAFT_STORAGE_KEY: string;
export const MODEL_ID: string;
export const modelDefinitions: { id: string; name: string; file: string }[];
export const profileTemplates: { id: string; name: string; profile: MachineProfile }[];
export function displayName(value: unknown): string;
export function createProfileDraft(templateId: string, name: string, id: string): MachineProfile;
export function modelBindings(profile: MachineProfile, modelId?: string): Record<string,string>;
export function validateWorkspaceProfile(profile: MachineProfile): MachineProfile;
export function bindModelPart(profile: MachineProfile, partId: string, deviceId: string, modelId?: string): MachineProfile;
export function decodeDrafts(raw: string|null): MachineProfile[];
export function saveDraft(records: MachineProfile[], profile: MachineProfile): MachineProfile[];
