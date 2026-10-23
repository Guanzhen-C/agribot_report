import {XacroParser} from 'xacro-parser';

const forbiddenNames=new Set(['__proto__','constructor','prototype']);
export const isXacroFile=path=>/\.(xacro|xcaro)$/i.test(path);
export function parseXacroArguments(text){
  if(text.length>8192)throw new Error('Xacro 参数最多 8 KB。');
  const result=Object.create(null);
  for(const line of text.split(/\r?\n/).map(l=>l.trim()).filter(l=>l&&!l.startsWith('#'))){
    const match=/^([a-zA-Z_][\w.-]*)\s*=\s*(.*)$/.exec(line);
    if(!match||forbiddenNames.has(match[1]))throw new Error('参数格式应为 name=value，每行一项。');
    if(Object.hasOwn(result,match[1]))throw new Error('参数名称重复：'+match[1]);
    result[match[1]]=match[2];
  }
  if(Object.keys(result).length>64)throw new Error('Xacro 参数最多 64 项。');
  return result;
}

function checkedXml(text,path){
  if(text.length>2*1024*1024)throw new Error('单个 Xacro/XML 文件不能超过 2 MB。');
  if(/<!DOCTYPE|<!ENTITY/i.test(text))throw new Error('Xacro 不允许 DTD 或外部实体。');
  const doc=new DOMParser().parseFromString(text,'text/xml');
  if(doc.querySelector('parsererror')||doc.documentElement.tagName!=='robot')throw new Error('Xacro XML 格式不正确：'+path);
  const nodes=[...doc.getElementsByTagName('*')];
  if(nodes.length>12000)throw new Error('Xacro 文档节点过多，请简化后导入。');
  for(const n of nodes){
    if(n.prefix&&n.namespaceURI==='http://www.ros.org/wiki/xacro'&&n.prefix!=='xacro')throw new Error('请使用标准 xacro: 命名空间前缀。');
    if(forbiddenNames.has(n.getAttribute('name')))throw new Error('Xacro 使用了不支持的保留名称。');
    if(n.tagName==='xacro:call'||(n.tagName==='xacro:include'&&n.hasAttribute('ns'))||/(:=\^)/.test(n.getAttribute('params')||''))throw new Error('暂不支持动态宏调用、include 命名空间或参数透传，请先用 ROS 展开为 URDF。');
    // No prototype traversal through the expression evaluator's dictionary helper.
    for(const a of n.attributes)if(/__proto__|\bconstructor\b|\bprototype\b/.test(a.value))throw new Error('Xacro 表达式包含不支持的对象访问。');
  }
  return doc;
}

// Bound non-recursive macro expansion before invoking the upstream parser. Recursion
// is deliberately unsupported here rather than allowing an imported file to hang the UI.
function checkMacros(docs){
  const macros=new Map(),roots=[];
  for(const doc of docs){
    for(const n of doc.getElementsByTagName('xacro:macro')){
      const name=n.getAttribute('name');
      if(macros.has(name))throw new Error('Xacro 宏重复定义：'+name+'。请先用 ROS 展开后导入。');
      macros.set(name,n);
    }
    roots.push(...doc.documentElement.children);
  }
  const memo=new Map();
  function weight(node,stack=[]){
    if(node.tagName==='xacro:macro')return 0;
    const key=node.tagName?.replace(/^xacro:/,'');
    let cost=1;
    if(node.prefix==='xacro'&&macros.has(key)){
      if(stack.includes(key)||stack.length>=32)throw new Error('不支持递归或超过 32 层的 Xacro 宏，请先用 ROS 展开。');
      if(!memo.has(key))memo.set(key,[...macros.get(key).children].reduce((sum,n)=>sum+weight(n,[...stack,key]),0));
      cost+=memo.get(key);
    }
    for(const child of node.children??[])cost+=weight(child,stack);
    if(cost>25000)throw new Error('Xacro 宏展开超过 25000 个节点，请简化后导入。');
    return cost;
  }
  // Also check unused definitions, because includes can introduce subsequent calls.
  for(const [name,node]of macros){for(const child of node.children)weight(child,[name]);}
  if(roots.reduce((sum,n)=>sum+weight(n),0)>25000)throw new Error('Xacro 展开规模过大。');
}

export async function expandXacro({entries,entry},resolve,argumentsMap={}){
  const parser=new XacroParser(),docs=new Map(),decoder=new TextDecoder();let includeReads=0;
  const base=entry.includes('/')?entry.slice(0,entry.lastIndexOf('/')+1):'';
  // Use a local-only virtual root for $(find pkg); no ROS process or environment access.
  const virtual='/__fieldgate_local__/';
  function localPath(path){return path.startsWith(virtual)?path.slice(virtual.length):path.startsWith('package://')?path.slice(10):path;}
  function read(path){
    const key=resolve(localPath(path)),text=decoder.decode(entries.get(key));
    if(!docs.has(key)){docs.set(key,checkedXml(text,key));checkMacros(docs.values());}
    return text;
  }
  parser.workingPath=base;parser.arguments=argumentsMap;
  parser.rospackCommands=(command,...args)=>{
    if(command==='arg')return null; // Upstream applies supplied arguments and xacro:arg defaults.
    if(command==='find'&&args.length===1){
      const pkg=args[0];if(!/^[\w.-]+$/.test(pkg))throw new Error('ROS 包名称无效。');
      const dirs=new Set();
      for(const path of entries.keys()){const segments=path.split('/');for(let i=0;i<segments.length-1;i++)if(segments[i]===pkg)dirs.add(segments.slice(0,i+1).join('/'));}
      if(dirs.size!==1)throw new Error('找不到唯一的本地 ROS 包目录：'+pkg+'。请在 ZIP 中保留包名目录。');
      return virtual+[...dirs][0];
    }
    throw new Error('不支持 $('+command+')；仅支持 $(arg name) 和本地 $(find package)。');
  };
  parser.getFileContents=async path=>{
    if(++includeReads>128)throw new Error('Xacro include 超过 128 次，可能存在循环引用。');
    return read(path);
  };
  // All finite expression evaluation is handled by the library, never eval/Python/shell.
  parser.expressionParser.functions.__read_property__=(object,...keys)=>{let value=object;for(const key of keys){if(forbiddenNames.has(String(key))||value==null||!Object.hasOwn(Object(value),key))throw new Error('不支持访问对象继承属性。');value=value[key];}return value;};
  const evaluate=parser.expressionParser.evaluate.bind(parser.expressionParser);let evaluations=0;
  parser.expressionParser.evaluate=(expression,properties)=>{if(++evaluations>25000)throw new Error('Xacro 表达式计算次数超限。');return evaluate(expression,properties);};
  try{
    const result=await parser.parse(read(entry));
    for(const node of result.getElementsByTagName('*')){
      if(node.prefix==='xacro')throw new Error('展开后仍有未处理的 Xacro 标签。');
      for(const attr of node.attributes){
        if(attr.value.startsWith(virtual)){
          const target=attr.value.slice(virtual.length).split('/'),from=base.split('/').filter(Boolean);
          while(from.length&&target.length&&from[0]===target[0]){from.shift();target.shift();}
          attr.value=[...from.map(()=>'..'),...target].join('/');
        }
        if(/\$[({]/.test(attr.value))throw new Error('展开后仍有未解析的 Xacro 表达式。');
      }
    }
    const xml=new XMLSerializer().serializeToString(result);
    if(xml.length>4*1024*1024)throw new Error('展开后的 URDF 超过 4 MB。');
    return xml;
  }catch(cause){throw new Error('Xacro 展开失败：'+(cause instanceof Error?cause.message:String(cause)));}
}
