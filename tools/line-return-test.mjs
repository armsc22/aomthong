import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
const source = await readFile('wwwroot/js/line-return.js','utf8');
for (const [query,loggedIn,target] of [
 ['?liff.state=%2FAccount%2FRegister&code=private',true,'/Account/Register'],
 ['?liff.state=%2FAccount%2FProfile',true,'/Account/Profile'],
 ['?liff.state=https%3A%2F%2Fevil.test',true,'/Account/Register'],
 ['?liff.state=%2FAccount%2FRegister',false,null]
]) {
 let redirected=null, initialized=false;
 const status={dataset:{liffId:'test'}}; const retry={hidden:true};
 await vm.runInNewContext(source,{
   URLSearchParams,
   document:{getElementById:id=>id==='line-return-status'?status:retry},
   window:{location:{search:query,pathname:'/',replace:value=>{assert.equal(initialized,true);redirected=value;}}},
   liff:{init:async()=>{initialized=true;},isLoggedIn:()=>loggedIn}
 });
 assert.equal(redirected,target);
 if(!loggedIn) assert.equal(retry.hidden,false);
}
console.log('PASS: LINE callback initialization, registration/profile routing, external redirect rejection, expired login');
