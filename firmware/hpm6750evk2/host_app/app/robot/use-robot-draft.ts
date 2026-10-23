"use client";
import {useEffect,useState} from 'react';
import {createRobotDescription,readRobotDescription,ROBOT_STORAGE_KEY,validateRobotDescription,type RobotDescription,type RobotPart} from '../../lib/robot-description.mjs';

export function useRobotDraft(modelId:string,parts:RobotPart[]){
  const [draft,setDraft]=useState<RobotDescription>(()=>createRobotDescription(modelId));
  const [dirty,setDirty]=useState(false),[ready,setReady]=useState(false),[error,setError]=useState(''),[message,setMessage]=useState('');
  useEffect(()=>{
    // eslint-disable-next-line react-hooks/set-state-in-effect -- Read browser-only, model-specific metadata after hydration; never overwrite during load.
    setReady(false);setDirty(false);setMessage('');setError('');
    if(!parts.length)return;
    try{setDraft(readRobotDescription(localStorage.getItem(`${ROBOT_STORAGE_KEY}.${modelId}`),modelId,parts));setReady(true);}
    catch{setDraft(createRobotDescription(modelId));setError('机器人描述无法读取，已有内容未覆盖。可导入备份或导出当前草稿。');}
  },[modelId,parts]);
  function change(next:RobotDescription){setDraft(next);setDirty(true);setError('');setMessage('');}
  function save(){
    try{validateRobotDescription(draft,parts);localStorage.setItem(`${ROBOT_STORAGE_KEY}.${modelId}`,JSON.stringify(draft));setDirty(false);setMessage('机器人描述已保存在本机');setError('');}
    catch(cause){setError(cause instanceof Error && /[\u4e00-\u9fff]/.test(cause.message)?cause.message:'未能保存，请检查本机存储空间与权限。');}
  }
  function load(raw:string){
    try{const next=readRobotDescription(raw,modelId,parts);change(next);setReady(true);setMessage('已载入描述，请保存到本机');}
    catch(cause){setError(cause instanceof Error?cause.message:'描述无法读取。');}
  }
  return {draft,dirty,ready,error,message,change,save,load,setError,setMessage};
}
