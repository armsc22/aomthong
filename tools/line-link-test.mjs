import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
const source = await readFile('wwwroot/js/line-link.js', 'utf8');
async function setup(loggedIn, responseOk = true) {
  const listeners = {};
  const submit = { disabled:false };
  const nodes = {
    'line-connect-btn': {dataset:{liffId:'test',bindUrl:'/Account/BindLineUserId'}, addEventListener: (type, fn) => listeners[type] = fn, focus(){}},
    'line-status': {setAttribute(){}}, 'LineUserId':{value:'stale'}, 'LineDisplayName':{value:''}, 'LinkNonce':{value:'nonce'},
    'register-form': {querySelector:()=>submit,addEventListener:(type,fn)=>listeners.submit=fn}
  };
  let posted=0, requests=0;
  const context = {document:{getElementById:id=>nodes[id],querySelector:()=>({value:'csrf'})},
    liff:{init:async()=>{},isLoggedIn:()=>loggedIn,getIDToken:()=> 'test-token',login:()=>{}},
    window:{location:{origin:'https://example.test',pathname:'/Account/Register'}},
    HTMLFormElement:{prototype:{submit(){posted++;}}}, AbortSignal,
    fetch:async()=>{requests++;return {ok:responseOk,status:400,json:async()=>responseOk?{bound:true,userId:'verified',displayName:'Member'}:{error:'เซสชันหมดอายุ'}};}
  };
  await vm.runInNewContext(source, context);
  return {nodes,submit,listeners,posted:()=>posted,requests:()=>requests};
}
const signedOut=await setup(false);
assert.equal(signedOut.submit.disabled,true);
await signedOut.listeners.submit({preventDefault(){}});
assert.equal(signedOut.posted(),0);
const success=await setup(true);
assert.equal(success.submit.disabled,false);
assert.equal(success.nodes.LineUserId.value,'verified');
await success.listeners.submit({preventDefault(){}});
assert.equal(success.requests(),2);
assert.equal(success.posted(),1);
const failure=await setup(true,false);
assert.equal(failure.submit.disabled,true);
assert.equal(failure.nodes.LineUserId.value,'');
assert.equal(failure.nodes['line-status'].textContent,'เซสชันหมดอายุ');
console.log('PASS: LINE registration UI checks (signed out, verified, refresh before submit, failure)');
