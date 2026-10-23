"use client";

import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import {tuningTransforms,type ModelAsset,type ModelTuning} from '../../lib/model-tuning.mjs';

export interface ModelPart { id: string; name: string; }
interface ModelProps {
  modelFile: string;
  selected: string; explode: number; isolate: boolean; view: { name: string; revision: number };
  onParts: (parts: ModelPart[]) => void; onSelect: (id: string) => void;
  onReady: (ready: boolean) => void;
  selectableIds?: string[];
  customModel?: ModelAsset;
  tuning?: ModelTuning;
}
type Runtime = { update: (props: ModelProps) => void };

function tokenColor(name: string) {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
  const context = canvas.getContext('2d')!;
  context.fillStyle = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  context.fillRect(0,0,1,1);
  const pixel = context.getImageData(0,0,1,1).data;
  return new THREE.Color(pixel[0]/255,pixel[1]/255,pixel[2]/255).convertSRGBToLinear();
}

export function VehicleModel(props: ModelProps) {
  const container = useRef<HTMLDivElement>(null);
  const runtime = useRef<Runtime | null>(null);
  const current = useRef(props);
  const [load, setLoad] = useState({ state: 'loading', percent: 0, error: '' });
  const [retry, setRetry] = useState(0);
  useEffect(() => { current.current = props; runtime.current?.update(props); }, [props]);
  useEffect(() => {
    const host = container.current!;
    const abort = new AbortController();
    let disposed = false, frame = 0;
    let renderer: THREE.WebGLRenderer | undefined;
    let controls: OrbitControls | undefined;
    const scene = new THREE.Scene();
    const geometry = new Set<THREE.BufferGeometry>();
    const materials = new Set<THREE.Material>();
    const resize = new ResizeObserver(() => fitViewport());
    const camera = new THREE.PerspectiveCamera(35,1,.01,100);
    const stage = new THREE.Group(); scene.add(stage);
    const partObjects: { id:string; object:THREE.Object3D; origin:THREE.Vector3; scale:THREE.Vector3; delta:THREE.Vector3; meshes:THREE.Mesh[] }[] = [];
    let displayScale=1;
    const displayCenter=new THREE.Vector3(),presentation=new THREE.Matrix4().makeRotationX(-Math.PI/2);
    const accent = tokenColor('--color-accent');
    let viewKey = '';

    function render() { if (disposed || !renderer) return; frame = 0; renderer.render(scene,camera); }
    function invalidate() { if (!frame && !disposed) frame = requestAnimationFrame(render); }
    function fitViewport() {
      if (!renderer) return;
      const {width,height} = host.getBoundingClientRect();
      camera.aspect = width/Math.max(height,1); camera.updateProjectionMatrix(); renderer.setSize(width,height,false);
      viewKey = ''; update(current.current);
    }
    function update(next: ModelProps) {
      const factor = next.explode / 100;
      const transforms=next.customModel&&next.tuning?tuningTransforms(next.customModel.parts,next.tuning):null;
      for (const part of partObjects) {
        part.object.position.copy(part.origin).addScaledVector(part.delta,factor);
        part.object.scale.copy(part.scale);
        const transform=transforms?.[part.id];
        if(transform){part.object.position.fromArray(transform.position).applyMatrix4(presentation).sub(displayCenter).multiplyScalar(displayScale).addScaledVector(part.delta,factor);part.object.scale.multiply(new THREE.Vector3(...transform.scale));}
        part.object.visible = !next.isolate || !next.selected || part.id === next.selected;
        for (const mesh of part.meshes) for (const material of Array.isArray(mesh.material)?mesh.material:[mesh.material]) {
          if (material instanceof THREE.MeshStandardMaterial) {
            material.emissive.copy(accent); material.emissiveIntensity = part.id === next.selected ? .45 : 0;
            material.color.copy(material.userData.baseColor);
            if (part.id === next.selected) material.color.lerp(accent,.45);
          }
        }
      }
      const key = `${next.view.name}:${next.view.revision}:${next.customModel?JSON.stringify(next.tuning):''}`;
      if (key !== viewKey && controls) {
        viewKey = key;
        controls.target.set(0,0,0);
        const distance = Math.max(5.2,4.4/camera.aspect);
        if (next.view.name === 'top') camera.position.set(0,distance,.001);
        else if(next.view.name === 'side') camera.position.set(distance,.15,0);
        else camera.position.set(distance*.72,distance*.5,distance*.82);
        if(next.customModel){
          stage.updateMatrixWorld(true);
          const box=new THREE.Box3().setFromObject(stage),center=box.getCenter(new THREE.Vector3()),extent=box.getSize(new THREE.Vector3()).length();
          const fit=Math.max(2,extent/(2*Math.tan(THREE.MathUtils.degToRad(camera.fov)/2)*Math.min(1,camera.aspect)));
          camera.position.normalize().multiplyScalar(fit).add(center);controls.target.copy(center);controls.maxDistance=Math.max(14,fit*4);camera.far=Math.max(100,fit*10);camera.updateProjectionMatrix();
        }
        controls.update();
      }
      host.dataset.selectedPart = next.selected;
      host.dataset.explode = String(next.explode);
      invalidate();
    }
    let press = {x:0,y:0};
    const pointerDown = (e:PointerEvent) => { press={x:e.clientX,y:e.clientY}; };
    const pointerUp = (e:PointerEvent) => {
      if (Math.hypot(e.clientX-press.x,e.clientY-press.y)>7 || !renderer || e.button!==0) return;
      const rect=host.getBoundingClientRect(), ray=new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2((e.clientX-rect.left)/rect.width*2-1,-(e.clientY-rect.top)/rect.height*2+1),camera);
      const hit=ray.intersectObjects(partObjects.filter(p=>p.object.visible&&(!current.current.selectableIds||current.current.selectableIds.includes(p.id))).map(p=>p.object),true)[0];
      let target:THREE.Object3D|undefined=hit?.object;
      while(target && !target.userData.partId) target=target.parent??undefined;
      if(target?.userData.partId) current.current.onSelect(target.userData.partId);
    };
    const contextLost = (event:Event) => { event.preventDefault(); current.current.onReady(false); setLoad({state:'error',percent:0,error:'三维显示已中断。可重新载入，部件目录和配置草稿仍可使用。'}); };
    async function open() {
      setLoad({state:'loading',percent:0,error:''});
      current.current.onReady(false);
      try {
        const custom=current.current.customModel;
        let manifest={parts:custom?.parts??[]};
        if(!custom){const manifestResponse=await fetch(`/models/${current.current.modelFile}.parts.json`,{signal:abort.signal});if(!manifestResponse.ok)throw new Error('模型目录未找到，请检查本机模型资源。');manifest=await manifestResponse.json();}
        if(disposed) return;
        current.current.onParts(manifest.parts);
        renderer = new THREE.WebGLRenderer({antialias:true,alpha:false});
        renderer.setPixelRatio(Math.min(window.devicePixelRatio,1.75));
        renderer.setClearColor(tokenColor('--color-model-background'));
        renderer.outputColorSpace = THREE.SRGBColorSpace;
        renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = .85;
        renderer.domElement.setAttribute('aria-label','农机三维模型；也可使用部件目录选择');
        renderer.domElement.addEventListener('webglcontextlost',contextLost);
        host.appendChild(renderer.domElement);
        controls = new OrbitControls(camera,renderer.domElement);
        controls.enableDamping = false; controls.minDistance = 1; controls.maxDistance = 14;
        controls.maxPolarAngle = Math.PI*.92; controls.screenSpacePanning = true;
        controls.addEventListener('change',invalidate);
        host.addEventListener('pointerdown',pointerDown);host.addEventListener('pointerup',pointerUp);
        resize.observe(host); fitViewport();
        const hemisphere = new THREE.HemisphereLight(tokenColor('--color-paper-2'),tokenColor('--color-machine'),1.5);
        const key = new THREE.DirectionalLight(tokenColor('--color-paper-2'),3);key.position.set(3,5,4);
        const fill = new THREE.DirectionalLight(tokenColor('--color-paper-2'),.8);fill.position.set(-4,2,-2);
        scene.add(hemisphere,key,fill);
        let model:THREE.Object3D;
        if(custom){model=custom.scene.clone(true);model.traverse(n=>{if(n instanceof THREE.Mesh){n.geometry=n.geometry.clone();n.material=Array.isArray(n.material)?n.material.map(m=>m.clone()):n.material.clone();}});}
        else{
        const response = await fetch(`/models/${current.current.modelFile}.glb`,{signal:abort.signal});
        if(!response.ok)throw new Error('模型文件未找到，请检查本机模型资源。');
        const total=Number(response.headers.get('content-length')||0), reader=response.body?.getReader();
        let data:ArrayBuffer;
        if(reader){
          const chunks:Uint8Array[]=[];let received=0;
          while(true){const {value,done}=await reader.read();if(done)break;chunks.push(value);received+=value.length;if(!disposed)setLoad({state:'loading',percent:total?Math.min(95,Math.round(received/total*95)):0,error:''});}
          const joined=new Uint8Array(received);let offset=0;for(const chunk of chunks){joined.set(chunk,offset);offset+=chunk.length;}data=joined.buffer;
        }else data=await response.arrayBuffer();
        model=(await new GLTFLoader().parseAsync(data,'')).scene;
        }
        // Supplied CAD uses its length axis as the writer's vertical axis.
        // Presentation correction only; do not infer control geometry from CAD.
        model.rotation.x=-Math.PI/2;
        model.traverse(object=>{if(object instanceof THREE.Mesh){geometry.add(object.geometry);for(const mat of Array.isArray(object.material)?object.material:[object.material])materials.add(mat);}});
        if(disposed){geometry.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());return;}
        const bounds=new THREE.Box3().setFromObject(model),size=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3());
        if(!Number.isFinite(size.length()) || size.length()===0) throw new Error('模型未包含可显示的装配网格。');
        const scale=2.8/Math.max(size.x,size.y,size.z);
        displayScale=scale;displayCenter.copy(center);
        model.scale.setScalar(scale);model.position.copy(center).multiplyScalar(-scale);stage.add(model);stage.updateMatrixWorld(true);
        const nodes:THREE.Object3D[]=[];
        model.traverse(object=>{if(object.userData.partId)nodes.push(object);});
        for(const node of nodes){
          const box=new THREE.Box3().setFromObject(node),mid=box.getCenter(new THREE.Vector3());
          stage.attach(node);
          const meshes:THREE.Mesh[]=[];
          node.traverse(object=>{if(object instanceof THREE.Mesh){
            const copy=(mat:THREE.Material)=>{const clone=mat.clone();materials.add(clone);if(clone instanceof THREE.MeshStandardMaterial){clone.userData.baseColor=clone.color.clone();clone.metalness=Math.min(clone.metalness,.35);clone.roughness=Math.max(clone.roughness,.45);}return clone;};
            object.material=Array.isArray(object.material)?object.material.map(copy):copy(object.material);meshes.push(object);
          }});
          const delta=mid.clone().multiplyScalar(.8);
          delta.y += mid.y>=0 ? .35 : -.22;
          partObjects.push({id:node.userData.partId,object:node,origin:node.position.clone(),scale:node.scale.clone(),delta,meshes});
        }
        if(!partObjects.length)throw new Error('模型缺少部件标识，无法进行配置关联。');
        runtime.current={update};update(current.current);setLoad({state:'ready',percent:100,error:''});host.dataset.loaded='true';current.current.onReady(true);
      } catch (error) {
        if(!disposed){current.current.onReady(false);setLoad({state:'error',percent:0,error:error instanceof Error && /[\u4e00-\u9fff]/.test(error.message)?error.message:'三维模型暂时无法载入。可重试，或使用部件目录继续配置。'});}
      }
    }
    void open();
    return () => {
      disposed=true;abort.abort();runtime.current=null;cancelAnimationFrame(frame);resize.disconnect();
      host.removeEventListener('pointerdown',pointerDown);host.removeEventListener('pointerup',pointerUp);
      controls?.dispose();geometry.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());
      if(renderer){renderer.domElement.removeEventListener('webglcontextlost',contextLost);renderer.dispose();renderer.domElement.remove();}
      delete host.dataset.loaded;
    };
  },[retry,props.modelFile,props.customModel]);
  return <div className="fg-model-stage"><div ref={container} className="fg-three-canvas"/>{load.state!=='ready' && <div className="fg-model-load" role="status">{load.state==='error'?<><strong>模型暂不可用</strong><p>{load.error}</p><button className="fg-button" onClick={()=>setRetry(retry+1)}>重新载入</button></>:<><strong>正在载入装配模型</strong><progress max="100" value={load.percent}/><span>{load.percent ? `${load.percent}%` : '首次打开需要读取本机模型'}</span></>}</div>}</div>;
}
