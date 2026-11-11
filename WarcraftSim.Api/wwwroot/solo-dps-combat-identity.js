"use strict";
(function(root){
  // Port of the accepted Combat HTTP identity policy, applied to already parsed
  // JavaScript JSON numbers. This is NOT an arbitrary-precision decimal parser.
  const fields=new Set(['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId']);
  const guid=/^[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$/;
  const zero='00000000-0000-0000-0000-000000000000';
  const generated=path=>{
    if(path[0]==='sim')path=path.slice(1);
    return path.length===4&&path[0]==='run'&&path[1]==='timeline'&&Number.isInteger(path[2])&&fields.has(path[3]);
  };
  function signature(value){
    const literals=new Set(),identities=new Map();let next=0;
    function collect(v,path=[]){
      if(Array.isArray(v))v.forEach((child,i)=>collect(child,[...path,i]));
      else if(v!==null&&typeof v==='object')for(const [key,child] of Object.entries(v)){
        if(guid.test(key))literals.add(key);collect(child,[...path,key]);
      }
      else if(typeof v==='string'&&guid.test(v)&&(!generated(path)||v===zero))literals.add(v);
    }
    collect(value);
    function emit(v,path=[]){
      if(Array.isArray(v))return '['+v.map((child,i)=>emit(child,[...path,i])).join(',')+']';
      if(v!==null&&typeof v==='object')return '{'+Object.keys(v).sort().map(key=>JSON.stringify(key)+':'+emit(v[key],[...path,key])).join(',')+'}';
      if(typeof v==='string'&&guid.test(v)&&v!==zero&&generated(path)&&!literals.has(v)){
        if(!identities.has(v)){
          let candidate;do{candidate='00000000-0000-4000-8000-'+(next++).toString(16).padStart(12,'0');}while(literals.has(candidate));
          identities.set(v,candidate);
        }
        v=identities.get(v);
      }
      if(typeof v==='number'&&!Number.isFinite(v)||!['string','number','boolean','object'].includes(typeof v))throw Error('Combat replay requires finite parsed JSON values.');
      return JSON.stringify(v);
    }
    return emit(value);
  }
  const api={signature,policy:'Complete parsed JSON response; JavaScript number precision. Only nonzero GUIDs at (sim.)run.timeline integer-index root event identity fields may be renamed; literal anchors, joins/splits, types, missing/null, zero GUIDs, array order and all other fields remain exact.'};
  if(typeof module==='object'&&module.exports)module.exports=api;
  if(root)root.SoloDpsCombatIdentity=api;
})(typeof window!=='undefined'?window:null);
