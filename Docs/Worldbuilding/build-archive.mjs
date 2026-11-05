import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1'));
const base = decodeURIComponent(root);
const runtime = 'C:/Users/Sakaona/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const {marked} = await import(pathToFileURL(path.join(runtime,'marked/lib/marked.esm.js')));
const esc = s => String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const svgHead=(w,h,title)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(title)}"><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0L10 5L0 10Z" fill="#667a88"/></marker></defs><rect width="100%" height="100%" fill="#f5f2e9"/><g font-family="Microsoft YaHei, Noto Sans SC, sans-serif" fill="#1c2e38">`;
const t=(x,y,text,size=18,fill='#1c2e38',weight='400')=>`<text x="${x}" y="${y}" font-size="${size}" fill="${fill}" font-weight="${weight}">${esc(text)}</text>`;
const box=(x,y,w,h,title,lines,color='#53797c')=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="10" fill="#fffdf7" stroke="${color}" stroke-width="2"/><rect x="${x}" y="${y}" width="6" height="${h}" rx="3" fill="${color}"/>`+t(x+20,y+33,title,21,color,'700')+lines.map((l,i)=>t(x+20,y+64+i*26,l,16)).join('');
const edge=(d,label,x,y,dashed=false)=>`<path d="${d}" fill="none" stroke="#667a88" stroke-width="2" ${dashed?'stroke-dasharray="7 5"':''} marker-end="url(#arrow)"/><rect x="${x-8}" y="${y-18}" width="${label.length*16+16}" height="25" fill="#f5f2e9"/>`+t(x,y,label,16,'#526672');
const end='</g></svg>';
let rel=svgHead(1160,900,'零界人物与阵营关系图');
rel+=t(45,48,'人物与阵营关系',30,'#1c2e38','700')+t(45,80,'身份席位尚未命名；人员存活、领导关系与事故责任保持留白。',17,'#63747b');
rel+=box(40,130,300,120,'C04 · 苏联高层',['首次共振受影响【已定】','姓名、最终下落未知']);
rel+=box(430,130,300,120,'苏联科研体系',['不断扩建共振实验室','权力推动研究规模【已定】']);
rel+=box(820,130,300,120,'C05 · 回应者群体',['成瘾与秘密结社【已定】','组织称谓【沿用待核】']);
rel+=edge('M340 190H430','推动扩建',347,170);
rel+=edge('M730 190H820','成员转化',737,170);
rel+=box(40,400,300,120,'C06 · 普通工作人员',['不默认全员自愿参与','人员暴露与转化【已定】']);
rel+=box(430,400,300,120,'同类异常晶体',['阿比森纳斯【已定名称】','CSM-017【沿用待核】']);
rel+=box(820,400,300,120,'现代科研团队 / C03',['无需人体合成【已定】','负责人席位【本次建议】']);
rel+=edge('M970 250V400','双向发现【已定】',805,330);
rel+=edge('M820 460H730','人工合成',737,442);
rel+=edge('M340 460H430','体内生成',347,442);
rel+=edge('M190 250V400','影响人员【已定】',52,330);
rel+=box(40,675,300,120,'普通 / 高级共鸣者',['两种人形敌人【已定】','高级实验影响方式待核']);
rel+=box(430,675,300,120,'C01 · 主角',['应急成员【沿用待核】','失枪、失联；寻会合路线']);
rel+=box(820,675,300,120,'C02 · 队友 / 联络',['同队关系【沿用待核】','具体人数、存活与职位未知']);
rel+=edge('M190 520V675','暴露与晶化【已定】',52,603);
rel+=edge('M970 520V585H580V675','事故现场行动【沿用待核】',625,574);
rel+=edge('M730 735H820','同队',753,716);
rel+=t(45,855,'关系限制：C04 是否成为正式领袖、C04 与 C05 是否同一批幸存者，均未确定。',17,'#63747b');
fs.writeFileSync(path.join(base,'images/relationships.svg'),rel+end);

let time=svgHead(1160,1340,'零界历史与现代事故时间线');
time+=t(45,48,'历史与事故时间线',30,'#1c2e38','700')+t(45,80,'先后关系与证据状态并列；1984、2028 尚待作者确认。',17,'#63747b');
time+='<path d="M88 130V1230" stroke="#a4b0ad" stroke-width="4"/>';
const events=[
['冷战时期 / 年份未知','苏联建站、早期小规模共振','建站背景【沿用待核】；研究共振【已定】'],
['首次有效回应','一名设施高层受影响','推动更多、更大的共振实验室【已定】'],
['秘密扩张阶段','结晶、成瘾、聚集与传教','制造更多可回应结晶个体【已定】'],
['1984 · 待核','零号灾难与封锁','灾难及动机【已定】；年份与封锁细节待核'],
['灾后数十年','未完全晶化的成瘾群体仍在地下','幸存【已定】；生存机制与代际关系留白'],
['2028 · 待核','现代碰撞合成出同类晶体','无人体合成【已定】；年份、编号待核'],
['现代事故前后','发现旧设施，第一次冲击后救援进入','双向发现【已定】；第一次冲击顺序待核'],
['主角乘电梯时 · 待核','第二次冲击，失枪、与队友失联','第二次冲击【已定核心】；具体演出待核'],
['序章尾声 · 待核','列车逃亡，揭示巨构与祭祀痕迹','祭祀反差【已定意向】；镜头与路线待核']
];
events.forEach((e,i)=>{let y=120+i*128;time+=`<circle cx="88" cy="${y+35}" r="10" fill="${i===3||i===5?'#b88b42':'#53797c'}"/>`;time+=box(130,y,975,106,e[1],[e[0]+' · '+e[2]],i===3||i===5?'#a87a33':'#53797c');});
time+=t(45,1300,'事件顺序不等于精确时刻；第一成功回应、零号灾难与现代两次冲击是四个不同节点。',17,'#63747b');
fs.writeFileSync(path.join(base,'images/timeline.svg'),time+end);

let map=svgHead(1160,940,'零界设施空间拓扑');
map+=t(45,48,'两代设施 · 空间拓扑',30,'#1c2e38','700')+t(45,80,'沿用末版总纲的路线提案；不表示真实层高、比例或地图坐标。',17,'#63747b');
map+=box(300,120,560,95,'地表 · 现代应急区域',['教学、领枪与下行【沿用待核】']);
map+=edge('M580 215V275','前线电梯 / 第二次冲击',600,252);
map+=box(60,285,470,120,'苏联实验室① · 冲量储存',['躲避 → 获得兵装 → 格挡与反冲','与现代区连通【沿用待核】']);
map+=box(630,285,470,120,'现代实验室② · 定向碰撞',['矢量牵引、物体碰撞','维护无人机【沿用待核】']);
map+=edge('M530 345H630','→',570,330);
map+=edge('M865 405V450H295V485','交错路线【沿用待核】',490,443);
map+=box(60,495,470,120,'苏联实验室③ · 时间散射',['碰撞回声；两代记录对照','高级共鸣者【沿用待核】']);
map+=box(630,495,470,120,'现代实验室④ · 大型合成设施',['综合能力与失稳逃亡','不要求独立大型关卡【本次建议】']);
map+=edge('M530 555H630','→',570,540);
map+=edge('M865 615V655H580V685','运输列车【沿用待核】',655,648);
map+=box(300,690,560,105,'苏联地下巨构 / 交通枢纽',['祭祀入口反差【已定意向】','巨构与深层空间的具体形态待核']);
map+=edge('M580 795V850','更深联系未知',605,834,true);
map+=t(390,890,'深渊 · 性质、尺度与回应来源留白',21,'#63747b','700');
fs.writeFileSync(path.join(base,'images/facility-map.svg'),map+end);

const md=fs.readFileSync(path.join(base,'零界-世界观设定档案.md'),'utf8');
let body=marked.parse(md);
const toc=[];let idx=0;
body=body.replace(/<h2>([\s\S]*?)<\/h2>/g,(all,txt)=>{if(!/^\d\d/.test(txt))return all;const id='chapter-'+idx++;toc.push({id,txt});return `<h2 id="${id}">${txt}</h2>`;});
for(const [name,cl] of [['已定','fixed'],['沿用待核','pending'],['本次建议','new'],['留白','open']])body=body.replaceAll('【'+name+'】',`<span class="badge ${cl}">${name}</span>`);
body=body.replace(/<table>/g,'<div class="table-wrap"><table>').replace(/<\/table>/g,'</table></div>');
const dataImage=local=>{const abs=path.join(base,local);if(!fs.existsSync(abs))throw Error('Missing asset: '+abs);const mime=abs.endsWith('.svg')?'image/svg+xml':'image/png';return 'data:'+mime+';base64,'+fs.readFileSync(abs).toString('base64');};
body=body.replace(/src="(images\/[^"]+)"/g,(all,local)=>`src="${dataImage(local)}"`);
const style=`
:root{--ink:#20323b;--muted:#627477;--paper:#f5f2e9;--teal:#345e62;--gold:#ad813d}
*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:25px}body{margin:0;color:var(--ink);background:var(--paper);font:16px/1.9 "Microsoft YaHei","Noto Sans SC",sans-serif}
nav{position:fixed;inset:0 auto 0 0;width:268px;background:#182a33;color:#c5d0d2;padding:30px 20px;overflow:auto}nav .brand{font-size:32px;color:#f7f3e7;font-weight:700;letter-spacing:7px}.small{font-size:11px;letter-spacing:2px;color:#96aaa9}nav .version{margin:22px 0 26px;border-top:1px solid #38505a;padding-top:15px;font-size:12px}nav a{display:block;color:#bccdce;text-decoration:none;font-size:13px;line-height:1.55;padding:8px 10px;border-left:2px solid transparent;margin:3px 0}nav a:hover,nav a.active{color:white;background:#263e46;border-color:#cba867}.tools{margin-top:28px;display:flex;gap:8px}.tools button{background:#2f4b53;color:#e5e6de;border:1px solid #49636b;border-radius:4px;padding:8px;cursor:pointer}
main{margin-left:268px;padding:55px 65px 100px;max-width:1510px}h1{font-size:42px;letter-spacing:2px;line-height:1.35;margin:15px 0 10px}h1+h2{font:13px/1.6 sans-serif;letter-spacing:3px;color:var(--muted);margin:0 0 25px;border:0;padding:0}h2{font-size:28px;line-height:1.5;border-top:2px solid var(--teal);padding-top:28px;margin:75px 0 25px;break-after:avoid}h3{font-size:20px;margin:33px 0 13px;color:#345e62;break-after:avoid}p{margin:13px 0}strong{font-weight:700}a{color:#2f707a;text-underline-offset:4px}blockquote{margin:25px 0;border-left:4px solid #b88b42;background:#ede9de;padding:14px 24px;color:#3e5056}blockquote p{margin:3px 0}.badge{display:inline-block;font-size:11px;line-height:1.8;padding:0 6px;border-radius:3px;white-space:nowrap;vertical-align:middle;border:1px solid transparent}.fixed{background:#dce9de;color:#355e42;border-color:#c7dacb}.pending{background:#f1e5c9;color:#80612b;border-color:#e5d5ae}.new{background:#dbe8ec;color:#3a6271;border-color:#c4d9e1}.open{background:#e6e3eb;color:#655b77;border-color:#d7d1df}
img{display:block;width:100%;height:auto;margin:26px 0 9px;border:1px solid #d9d7cb;border-radius:4px}img+p:has(em){font-size:13px;line-height:1.8;color:#697978}ul,ol{padding-left:25px}li{margin:7px 0}.table-wrap{overflow-x:auto;margin:22px 0;border:1px solid #d4d8cf;border-radius:4px}table{width:100%;border-collapse:collapse;font-size:14px;line-height:1.8;background:#faf8f1}th{text-align:left;background:#e3e8e1;color:#29474a;font-size:13px}th,td{padding:12px 14px;vertical-align:top;border-bottom:1px solid #dce0d8}tr:last-child td{border-bottom:0}td:first-child{min-width:130px}code{font-family:Consolas,monospace}footer{font-size:12px;color:var(--muted);margin-top:60px;border-top:1px solid #ccd4ca;padding-top:20px}.mobile-top{display:none}
@media(max-width:1000px){nav{width:220px}main{margin-left:220px;padding:35px 25px}h1{font-size:34px}}@media(max-width:720px){nav{position:static;width:auto;max-height:300px;padding:22px}nav .links{display:grid;grid-template-columns:1fr 1fr}.tools{margin:12px 0}main{margin:0;padding:25px 18px}h1{font-size:29px}h2{font-size:24px;margin-top:52px}td,th{padding:9px 10px}table{min-width:580px}}
@media print{nav{display:none}main{margin:0;max-width:none;padding:0}body{background:white;font-size:10pt;line-height:1.7}h1{font-size:28pt}h2{font-size:19pt;break-before:page;margin-top:10px;padding-top:15px}h1+h2{break-before:auto}h3{font-size:13pt}a{color:inherit}img{max-height:235mm;object-fit:contain;border:0}.table-wrap{overflow:visible}table{font-size:8.5pt;min-width:0}th,td{padding:7px 8px}tr{break-inside:avoid}blockquote{break-inside:avoid}.badge{font-size:8pt}footer{display:none}}
`;
const nav=toc.map(x=>`<a href="#${x.id}">${x.txt}</a>`).join('');
const printRefinement = `@media print{h2{break-before:auto;margin-top:27pt;padding-top:12pt}#chapter-0{break-before:page}p{orphans:3;widows:3}h3{break-after:avoid}table{break-inside:avoid}img{max-height:190mm}img+p:has(em){break-before:avoid}blockquote{break-inside:avoid}}`;
const html=`<!doctype html><html lang="zh-CN"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="description" content="零界世界观设定档案 V0.9，含时间线、人物关系、视觉探索与来源边界。"><title>零界 · 世界观设定档案 V0.9</title><style>${style}${printRefinement}</style></head><body><nav aria-label="档案目录"><div class="brand">零界</div><div class="small">NULL BOUNDARY / WORLD BIBLE</div><div class="version">V0.9 · 2026.10.29<br>已定设定 · 工作提案 · 未解谜团</div><div class="links">${nav}</div><div class="tools"><button onclick="window.print()">打印 / 另存 PDF</button><button onclick="window.scrollTo({top:0,behavior:'smooth'})">回到顶部</button></div></nav><main>${body}<footer>零界世界观设定档案 V0.9 · 所有概念图均为探索，来源范围与待定内容见第 00、14、15 章。</footer></main><script>const links=[...document.querySelectorAll('nav a')];const observer=new IntersectionObserver(es=>{for(const e of es)if(e.isIntersecting){links.forEach(a=>a.classList.toggle('active',a.hash==='#'+e.target.id))}},{rootMargin:'-5% 0px -75% 0px'});document.querySelectorAll('h2[id]').forEach(h=>observer.observe(h));</script></body></html>`;
fs.writeFileSync(path.join(base,'零界-世界观设定档案.html'),html);
console.log(JSON.stringify({htmlBytes:Buffer.byteLength(html),chapters:toc.length,images:(body.match(/<img /g)||[]).length}));

