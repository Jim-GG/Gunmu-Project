import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const base=path.dirname(fileURLToPath(import.meta.url));
const runtime='C:/Users/Sakaona/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const {chromium}=await import(pathToFileURL(path.join(runtime,'playwright/index.mjs')));
const qa=path.join(base,'qa');fs.mkdirSync(qa,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--disable-gpu']});
const page=await browser.newPage({viewport:{width:1440,height:1080},deviceScaleFactor:1});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));
await page.goto(pathToFileURL(path.join(base,'零界-世界观设定档案.html')).href,{waitUntil:'load'});
await page.evaluate(()=>document.fonts.ready);
const desktop=await page.evaluate(()=>({
  width:innerWidth,scrollWidth:document.documentElement.scrollWidth,
  images:[...document.images].map(i=>({alt:i.alt,loaded:i.complete&&i.naturalWidth>0,width:i.naturalWidth})),
  chapters:document.querySelectorAll('h2[id]').length,
  tableOverflows:[...document.querySelectorAll('.table-wrap')].filter(t=>t.scrollWidth>t.clientWidth+1).length
}));
await page.screenshot({path:path.join(qa,'desktop-cover.png')});
await page.locator('#chapter-6').scrollIntoViewIfNeeded();
await page.screenshot({path:path.join(qa,'desktop-characters.png')});
await page.setViewportSize({width:390,height:844});
await page.evaluate(()=>scrollTo(0,0));
await page.screenshot({path:path.join(qa,'mobile-cover.png')});
const mobile=await page.evaluate(()=>({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,tableWraps:[...document.querySelectorAll('.table-wrap')].length}));
await page.setViewportSize({width:1440,height:1080});
await page.emulateMedia({media:'print'});
await page.pdf({path:path.join(base,'零界-世界观设定档案.pdf'),format:'A4',printBackground:true,preferCSSPageSize:false,margin:{top:'15mm',bottom:'16mm',left:'15mm',right:'15mm'},displayHeaderFooter:true,headerTemplate:'<div></div>',footerTemplate:'<div style="font-family:Arial;font-size:8px;color:#748087;width:100%;margin:0 15mm;display:flex;justify-content:space-between;"><span>NULL BOUNDARY · V0.9 · 2026.10.29</span><span><span class="pageNumber"></span> / <span class="totalPages"></span></span></div>'});
await page.emulateMedia({media:'screen'});
const diagrams=[];
for(const name of ['relationships','timeline','facility-map']){
 const svg=fs.readFileSync(path.join(base,'images',name+'.svg'),'utf8');
 await page.setContent('<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0;background:#f5f2e9">'+svg+'</body></html>',{waitUntil:'load'});
 const metrics=await page.evaluate(()=>{const s=document.querySelector('svg');const v=s.viewBox.baseVal;return {viewBox:[v.width,v.height],outside:[...s.querySelectorAll('text')].filter(t=>{const b=t.getBBox();return b.x<0||b.y<0||b.x+b.width>v.width||b.y+b.height>v.height}).map(t=>t.textContent)};});
 diagrams.push({name,...metrics});
 await page.screenshot({path:path.join(qa,name+'.png'),fullPage:true,timeout:6000});
}
await browser.close();
const report={desktop,mobile,diagrams,errors};
fs.writeFileSync(path.join(qa,'layout-report.json'),JSON.stringify(report,null,2));
if(errors.length||desktop.images.some(i=>!i.loaded)||desktop.scrollWidth>desktop.width||mobile.scrollWidth>mobile.width||diagrams.some(d=>d.outside.length))throw Error(JSON.stringify(report));
console.log(JSON.stringify(report));

