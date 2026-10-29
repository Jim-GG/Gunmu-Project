import fs from 'node:fs';import path from 'node:path';import {pathToFileURL,fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url));const out=path.join(dir,'qa');fs.mkdirSync(out,{recursive:true});
const {chromium}=await import('file:///C:/Users/Sakaona/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const page=await browser.newPage({viewport:{width:1440,height:1080}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
await page.goto(pathToFileURL(path.join(dir,'首个Demo流程与实现方案.html')).href);await page.evaluate(()=>document.fonts.ready);
const desktop=await page.evaluate(()=>({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,headings:document.querySelectorAll('h2[id]').length,tables:document.querySelectorAll('table').length,images:[...document.images].map(i=>({loaded:i.complete&&i.naturalWidth>0})),tableOverflow:[...document.querySelectorAll('.table-wrap')].filter(t=>t.scrollWidth>t.clientWidth+1).length}));
await page.screenshot({path:path.join(out,'desktop.png')});await page.locator('#s3').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'timeline.png')});
await page.setViewportSize({width:390,height:844});await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(out,'mobile.png')});
const mobile=await page.evaluate(()=>({width:innerWidth,scrollWidth:document.documentElement.scrollWidth}));
await page.setContent('<html><body style="margin:0">'+fs.readFileSync(path.join(dir,'Demo流程图.svg'),'utf8')+'</body></html>');
const flow=await page.evaluate(()=>{const s=document.querySelector('svg'),v=s.viewBox.baseVal;return {outside:[...s.querySelectorAll('text')].filter(t=>{const b=t.getBBox();return b.x<0||b.x+b.width>v.width||b.y<0||b.y+b.height>v.height}).map(t=>t.textContent)}});await browser.close();
const report={desktop,mobile,flow,errors};fs.writeFileSync(path.join(out,'report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
if(desktop.scrollWidth>desktop.width||desktop.images.some(i=>!i.loaded)||desktop.tableOverflow||mobile.scrollWidth>mobile.width||flow.outside.length||errors.length)process.exitCode=1;

