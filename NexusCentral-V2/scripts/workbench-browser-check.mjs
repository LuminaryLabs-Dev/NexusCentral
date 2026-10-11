import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { chromium } from 'playwright';

const output = resolve(process.env.NEXUS_BROWSER_EVIDENCE || 'output/workbench-browser');
mkdirSync(output,{recursive:true});
const root = resolve('.'), startedAt = new Date().toISOString(), revision=execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8'}).trim();
const workingTreeModified=Boolean(execFileSync('git',['status','--porcelain'],{encoding:'utf8'}).trim());
const requested={mode:'local',target:root,revision,workingTreeModified,expectedSurface:'dom',expectedState:'Home, action canvas, configuration, and reviewed workflow results',interaction:'Build a workflow from an empty canvas, configure, connect, save, execute, approve, reopen, and inspect mobile layout'};
const data=mkdtempSync(join(tmpdir(),'nexus-browser-'));
const decisions=[{at:startedAt,stage:'inspect',kind:'fact',statement:'Actual modified checkout, static production build. DOM/SVG app; use browser recorder rather than a synthetic Three.js scene.',basis:'source inspection',effect:'none'}, {at:startedAt,stage:'route',kind:'decision',statement:process.env.CHROMIUM_EXECUTABLE_PATH?'Using explicitly supplied Chromium. The default browser CDN returned an invalid archive; packaged Chromium was extracted locally with system fonts. Server and browser share this process network namespace.':'Using Playwright Chromium.',basis:'local browser availability',effect:'route-change'}];
writeFileSync(join(output,'target-lock.json'),JSON.stringify({runId:output.split('/').at(-1),requested},null,2));
const errors=[],consoleErrors=[],networkFailures=[],interactions=[];
const step=(name)=>{interactions.push({at:new Date().toISOString(),action:name});console.log(name);};
let browser,server,passed=false;
const screenshot=async(page,name)=>{await page.screenshot({path:join(output,name),fullPage:true});};
try {
  server=spawn(process.execPath,['services/nexus-node/src/cli.mjs'],{cwd:root,env:{...process.env,NODE_ENV:'test',NEXUS_CENTRAL_DATA:data,NEXUS_CENTRAL_PORT:'47811'},stdio:['ignore','pipe','pipe']});
  await new Promise((resolveReady,reject)=>{const timer=setTimeout(()=>reject(new Error('Host startup timeout')),10000);server.stdout.on('data',d=>{if(d.toString().includes('ready')){clearTimeout(timer);resolveReady();}});server.on('error',reject);server.stderr.on('data',d=>console.log(d.toString()));});
  browser=await chromium.launch({headless:true,...(process.env.CHROMIUM_EXECUTABLE_PATH?{executablePath:process.env.CHROMIUM_EXECUTABLE_PATH}:{}),args:['--no-sandbox','--disable-dev-shm-usage','--no-zygote','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')consoleErrors.push(m.text());});page.on('requestfailed',r=>networkFailures.push({url:r.url(),error:r.failure()}));
  await page.goto('http://127.0.0.1:47811');
  await page.getByRole('heading',{name:'Bring your work together.'}).waitFor();
  await screenshot(page,'before.png');step('Home rendered with Launch and workflow template.');
  await page.getByRole('button',{name:'Launch a workflow'}).click();
  await page.getByLabel('Workflow name').fill('Weekly business review');
  await page.getByRole('button',{name:/Read task records Bring/}).click();
  await page.getByRole('textbox',{name:'Task records'}).fill(JSON.stringify([{title:'Send project brief',owner:'Operations',status:'open'},{title:'Resolve delivery blocker',owner:'Delivery',status:'blocked'}]));
  await page.getByRole('button',{name:'Close configuration'}).click();
  // Exercise actual drag and drop from the action menu.
  await page.getByRole('button',{name:/Prepare operations report Create/}).dragTo(page.locator('.wb-canvas-viewport'),{targetPosition:{x:420,y:210}});
  await page.getByLabel('Report title').fill('Weekly operations report');
  await page.getByLabel('Input from').selectOption({label:'Read task records (1)'});
  await page.getByRole('button',{name:'Close configuration'}).click();
  await page.getByRole('button',{name:/Review result Read/}).click();
  await page.getByLabel('Input from').selectOption({label:'Prepare operations report (2)'});
  await page.getByRole('button',{name:'Close configuration'}).click();
  assert.equal(await page.locator('.wb-block').count(),3);assert.equal(await page.locator('.wb-wires path').count(),2);
  assert.equal(await page.locator('.wb-block .wb-configure').count(),3);
  await page.getByLabel('Project reference').fill('Luminary / Operations');
  await page.getByRole('button',{name:'Fit',exact:true}).click();
  await screenshot(page,'canvas.png');step('Created three blocks, used drag/drop and accessible Add, configured fields and typed connections.');
  // Reposition and undo via actual pointer input.
  const first=page.locator('.wb-block').first();const original=await first.getAttribute('style');const box=await first.boundingBox();
  await page.mouse.move(box.x+100,box.y+20);await page.mouse.down();await page.mouse.move(box.x+135,box.y+95,{steps:6});await page.mouse.up();
  assert.notEqual(await first.getAttribute('style'),original);
  await page.getByRole('button',{name:'Undo',exact:true}).click();assert.equal(await first.getAttribute('style'),original);
  await page.getByRole('button',{name:'Redo',exact:true}).click();
  await page.getByRole('button',{name:'Fit',exact:true}).click();
  // Duplicate/remove plus missing config validation.
  await first.getByRole('button',{name:/Configure/}).click();await page.getByRole('button',{name:'Duplicate',exact:true}).click();assert.equal(await page.locator('.wb-block').count(),4);
  await page.getByRole('button',{name:'Remove action'}).click();assert.equal(await page.locator('.wb-block').count(),3);
  await page.locator('.wb-block').nth(1).getByRole('button',{name:/Configure/}).click();
  await page.getByLabel('Report title').fill('');await page.getByRole('button',{name:/Run workflow/}).click();await page.getByRole('alert').filter({hasText:'Report title is required'}).waitFor();
  await page.getByRole('button',{name:'Dismiss message'}).click();await page.getByLabel('Report title').fill('Weekly operations report');
  await screenshot(page,'configuration.png');await page.getByRole('button',{name:'Close configuration'}).click();
  step('Verified move, undo/redo, duplicate/remove, and plain-language configuration errors.');
  await page.getByRole('button',{name:/Run workflow/}).click();
  await page.getByRole('button',{name:'Approve result',exact:true}).waitFor({timeout:20000});
  await page.getByText('2 tasks included.',{exact:false}).first().waitFor();
  await screenshot(page,'review.png');
  const href=await page.getByRole('link',{name:'Download report'}).getAttribute('href');
  const response=await page.request.get('http://127.0.0.1:47811'+href);assert.equal(response.status(),200);assert.match(await response.text(),/Resolve delivery blocker/);
  await page.getByRole('button',{name:'Approve result',exact:true}).click();
  await page.locator('.wb-run-toolbar .wb-status.completed').waitFor();
  await screenshot(page,'after.png');step('Ran real isolated workers, downloaded the report, and approved the review to complete the workflow.');
  await page.reload();await page.getByRole('navigation',{name:'Main navigation'}).getByRole('button',{name:/Workflows/}).click();
  await page.getByRole('button',{name:/Weekly business review.*Open workflow/}).click();assert.equal(await page.locator('.wb-block').count(),3);assert.equal(await page.getByLabel('Project reference').inputValue(),'Luminary / Operations');
  await page.getByRole('navigation',{name:'Main navigation'}).getByRole('button',{name:/Apps/}).click();await page.getByRole('heading',{name:'NexusAgent',exact:true}).waitFor();
  await screenshot(page,'apps.png');
  await page.getByRole('navigation',{name:'Main navigation'}).getByRole('button',{name:/Home/}).click();await page.setViewportSize({width:390,height:844});
  await screenshot(page,'mobile.png');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'Mobile page should not overflow horizontally');
  await page.getByRole('button',{name:'Launch a workflow'}).click();await page.getByRole('button',{name:/Prepare a brief Capture/}).click();await page.getByLabel('Title',{exact:true}).fill('Mobile brief');await page.getByLabel('Instructions',{exact:true}).fill('A mobile workflow');
  await screenshot(page,'mobile-builder.png');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'Mobile builder should not overflow horizontally');
  step('Reopened persisted workflow, inspected Apps, and configured a mobile action without horizontal overflow.');
  assert.deepEqual(errors,[]);assert.deepEqual(consoleErrors,[]);assert.deepEqual(networkFailures,[]);
  passed=true;
} catch(error) { errors.push(error.message); throw error; }
finally {
  if(browser)await browser.close();
  if(server){server.kill('SIGTERM');await new Promise(r=>{if(server.exitCode!==null)return r();server.once('exit',r);setTimeout(r,5000);});}
  rmSync(data,{recursive:true,force:true});
  for(const [file,value] of [['console.json',{errors,consoleErrors}],['network-failures.json',networkFailures],['interaction.json',interactions],['decision-log.json',decisions]])writeFileSync(join(output,file),JSON.stringify(value,null,2));
  const verdict=passed?'pass':'fail';
  writeFileSync(join(output,'validation.json'),JSON.stringify({verdict,captureMode:'timed screenshots',errors,interactions},null,2));
  const report={schemaVersion:'sandbox.web-render-validation/1',runId:output.split('/').at(-1),startedAt,completedAt:new Date().toISOString(),runDirectory:output,requested,observed:{target:root,revision,workingTreeModified,finalUrl:'http://127.0.0.1:47811/',surface:'dom'},identity:{matchesRequestedMode:true,matchesRequestedTarget:true,matchesRequestedRevision:true},freshness:{currentRunOnly:true,historicalEvidenceExcluded:true},render:{visible:passed,nonEmpty:passed,surfaceProven:passed,fatalErrors:errors},interaction:{required:true,performed:interactions.length>0,changeProven:passed},children:[{name:'sandbox-record-browser-it',verdict,evidence:join(output,'validation.json')}],artifacts:{before:join(output,'before.png'),after:join(output,'after.png'),decisionLog:join(output,'decision-log.json'),console:join(output,'console.json'),networkFailures:join(output,'network-failures.json'),interaction:join(output,'interaction.json')},supplementalRuns:[],cleanup:{required:true,completed:true},verdict,coverageBoundary:'Modified exact V2 checkout; local production DOM/SVG interface and built-in workflow. No external agent, native desktop, live cloud credential, or physical mesh proof.'};
  writeFileSync(join(output,'validation-report.json'),JSON.stringify(report,null,2));
}
