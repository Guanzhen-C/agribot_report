// Generated CAD measurements in metres; no visual normalization or explosion.
import fs from 'node:fs';
import {Box3,Vector3} from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
for(const file of ['ackermann','ackermann-flagship']){
  const data=fs.readFileSync(new URL(`../public/models/${file}.glb`,import.meta.url));
  const root=(await new GLTFLoader().parseAsync(data.buffer.slice(data.byteOffset,data.byteOffset+data.byteLength),'')).scene;
  const box=new Box3().setFromObject(root),center=box.getCenter(new Vector3());
  const parts={};
  root.traverse(node=>{if(node.userData.partId)parts[node.userData.partId]=new Box3().setFromObject(node).getCenter(new Vector3()).toArray();});
  const result={units:'m',origin:[center.x,center.y,box.min.z],size:box.getSize(new Vector3()).toArray(),parts};
  fs.writeFileSync(new URL(`../public/models/${file}.kinematics.json`,import.meta.url),JSON.stringify(result,null,2),{flag:'wx'});
  console.log(file,result.size,Object.keys(parts).length);
}
