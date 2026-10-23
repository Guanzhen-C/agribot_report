import type {RobotDescription,RobotJoint,RobotPart} from './robot-description.mjs';
import type {Matrix4} from 'three';
export function frameMatrix(joint:RobotJoint):Matrix4;
export function createUrdf(draft:RobotDescription,parts:RobotPart[],options?:{emptyLinks?:Set<string>}):{xml:string;links:{id:string;name:string;matrix:Matrix4}[]};
export function createRobotPackage(buffer:ArrayBuffer,draft:RobotDescription,parts:RobotPart[],onProgress?:(percent:number)=>void):Promise<{archive:Uint8Array;xml:string;files:Record<string,Uint8Array>;links:number;mapping:Record<string,{name:string;role:string;link:string}>}>;
