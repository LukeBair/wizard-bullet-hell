// Usage: node tools/artgen/render.js  -> writes Assets/Resources/Art/<Style>/*.png
const {chromium}=require(process.env.PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{const b=await chromium.launch();const p=await b.newPage();p.on('pageerror',e=>console.log('ERR',e.message));
for(const style of ['Dusk','Vector','Crt']){await p.goto('file://'+path.join(__dirname,'gen.html')+'?style='+style);await p.waitForFunction('window.DONE===true');
 const out=await p.evaluate(()=>window.OUT);const dir=path.join(__dirname,'../../Assets/Resources/Art',style);fs.mkdirSync(dir,{recursive:true});
 for(const [n,url] of Object.entries(out))fs.writeFileSync(path.join(dir,n+'.png'),Buffer.from(url.split(',')[1],'base64'));console.log(style,Object.keys(out).length);}
await b.close();})();
