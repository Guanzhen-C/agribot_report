export interface RobotPart {id:string;name:string;}
export type PartRole='actuator'|'perception'|'structure'|'support';
export type Vector3Tuple=[number,number,number];
export interface RobotJoint {name:string;parent:string;type:'fixed'|'continuous'|'revolute'|'prismatic';xyz:Vector3Tuple;rpy:Vector3Tuple;axis:Vector3Tuple;lower:number|null;upper:number|null;effort:number|null;velocity:number|null;confirmed:boolean;optical:boolean;}
export interface RobotDescription {format:string;modelId:string;name:string;author:string;packageName:string;forward:string;roles:Record<string,PartRole>;joints:Record<string,RobotJoint>;}
export const ROBOT_STORAGE_KEY:string;
export const roleNames:Record<PartRole,string>;
export function isActuator(device:{category:string;control:string}):boolean;
export function defaultPartRole(part:RobotPart):PartRole;
export function createRobotDescription(modelId:string):RobotDescription;
export function partRole(part:RobotPart,draft?:RobotDescription):PartRole;
export function robotMeasurements(modelId:string):{units:string;origin:Vector3Tuple;size:Vector3Tuple;parts:Record<string,Vector3Tuple>};
export function robotYaw(draft:RobotDescription):number;
export function robotPoint(point:Vector3Tuple,draft:RobotDescription):Vector3Tuple;
export function jointSettings(part:RobotPart,draft:RobotDescription):RobotJoint;
export function robotLinks(draft:RobotDescription,parts:RobotPart[]):RobotPart[];
export function validateRobotDescription(draft:RobotDescription,parts:RobotPart[],options?:{exporting?:boolean}):RobotDescription;
export function readRobotDescription(raw:string|null,modelId:string,parts:RobotPart[]):RobotDescription;
