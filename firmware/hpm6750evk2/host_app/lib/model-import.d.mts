import type {ModelAsset,ModelTuning} from './model-tuning.mjs';
export type ModelBundle={entries:Map<string,Uint8Array>;entry:string;candidates?:string[]};
export function readModelFiles(files:File[]):Promise<ModelBundle>;
export function importModel(bundle:ModelBundle,options:{up:string;forward:string;unit:number},xacroArguments?:Record<string,string>):Promise<ModelAsset>;
export function exportTunedModel(asset:ModelAsset,tuning:ModelTuning):Promise<ArrayBuffer>;
