const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { chromium } = require("C:/Users/saerm/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const out = __dirname;
const fragment = "C:/Users/saerm/.codex/visualizations/2026/10/12/01a12fe2-0d10-7a83-8454-e2f7ae0de499/dondon-device-interactions.html";
const source = fs.readFileSync(fragment,"utf8");
new vm.Script(source.match(/<script>([\s\S]*?)<\/script>/)[1]);
const report = {status:"RUNNING", started:new Date().toISOString(), checks:[], widths:[], pageErrors:[], limitation:"Browser mockup only. No Unity, real market, or World Space camera test."};
let browser, page, frame;
function check(condition, label, detail) {
  if (!condition) { throw new Error(label + (detail ? ": "+detail : "")); }
  report.checks.push(label); console.log("PASS "+label);
}
const sleep = ms => new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
  // Allow the launcher to apply a two-core affinity and BelowNormal priority before Chromium starts.
  await sleep(1300);
  browser = await chromium.launch({executablePath:"C:/Program Files/Google/Chrome/Application/chrome.exe",headless:true,
    args:["--disable-gpu","--renderer-process-limit=1","--disable-extensions","--disable-background-networking","--disable-component-update"]});
  page = await browser.newPage({viewport:{width:1280,height:1100},deviceScaleFactor:1, reducedMotion:"reduce"});
  page.on("pageerror",error=>report.pageErrors.push(error.message));
  await page.goto("file:///"+path.join(out,"prototype-preview.html").replaceAll("\\","/"),{waitUntil:"domcontentloaded"});
  await page.frameLocator("iframe").locator("#dondon-interactions").waitFor({state:"visible",timeout:15000});
  frame = page.frames().find(f=>f.parentFrame());
  await frame.evaluate(()=>{
    window.ddTestClicks=[];
    document.addEventListener("click",event=>{
      window.ddTestClicks.push(event.target.closest("button,summary,label")?.id || event.target.tagName);
      window.ddTestClicks=window.ddTestClicks.slice(-10);
    },true);
  });
  const q = id=>frame.locator("#"+id);
  const value = async id=>(await q(id).textContent()).trim();
  const cash = ()=>value("dd-cash");
  async function controls() { const d=frame.locator(".dd-controls"); if(!await d.evaluate(el=>el.open)){await d.locator("summary").click();} }
  async function press(id) {
    // Let the host's asynchronous iframe height measurement settle before mouse coordinates are chosen.
    await sleep(180);await q(id).click();await sleep(60);
  }
  async function reset() {
    await controls();await press("dd-test-reset");await sleep(100);
    const restored=await cash()==="970,000원" && await value("dd-held")==="6주" && await q("dd-draft").isVisible() && await q("dd-tab-order").getAttribute("aria-selected")==="true";
    check(restored,"reset restores draft, account, and order screen",JSON.stringify(await frame.evaluate(()=>({
      cash:document.querySelector("#dd-cash").textContent,held:document.querySelector("#dd-held").textContent,
      screen:[...document.querySelectorAll("[data-screen]")].find(e=>e.getAttribute("aria-selected")==="true")?.dataset.screen,
      draftHidden:document.querySelector("#dd-draft").hidden,receiptHidden:document.querySelector("#dd-receipt").hidden,
      validation:document.querySelector("#dd-validation").textContent,widget:window.openai?.widgetState
    }))));
  }
  async function submit() {await press("dd-review-button");await press("dd-submit");}
  async function until(id,text) { await q(id).filter({hasText:text}).waitFor({state:"visible",timeout:12000}); }
  async function screenshot(name) {await frame.locator("#dondon-interactions .dd-shell").screenshot({path:path.join(out,name+".png")});}

  check(await value("dd-total")==="16,170원" && (await value("dd-after-cash")).includes("953,830"),"initial order arithmetic and forecast");
  check(await cash()==="970,000원" && await value("dd-held")==="6주","draft does not change assets");
  await q("dd-quantity").fill("5");
  check(await q("dd-review-button").isDisabled() && (await value("dd-validation")).includes("6,950"),"budget overrun disables confirmation with exact reason");
  await screenshot("prototype-budget-error");
  await q("dd-quantity").fill("1.5");
  check(await q("dd-review-button").isDisabled(),"fractional share quantity rejected");
  await q("dd-quantity").fill("3");await q("dd-price").fill("5395");
  check(await q("dd-review-button").isDisabled() && (await value("dd-validation")).includes("10원"),"project tick size enforced");
  await q("dd-price").fill("0");
  check(await q("dd-review-button").isDisabled(),"zero-won order rejected");
  await q("dd-price").fill("5400");
  check(await value("dd-total")==="16,200원" && (await value("dd-after-cash")).includes("953,800"),"editing price recalculates consequences");
  await q("dd-price").fill("5390");
  await q("dd-split").check();await q("dd-parts").fill("4");
  check(await q("dd-review-button").isDisabled(),"split count cannot exceed total shares");
  await q("dd-parts").fill("2");
  check((await value("dd-split-description")).includes("2주 + 1주"),"uneven split distributes whole shares");
  await screenshot("prototype-order-split");
  await q("dd-split").uncheck();
  await press("dd-review-button");
  check(await q("dd-review").isVisible(),"review button opens the confirmation step");
  check(await cash()==="970,000원" && await value("dd-reserved")==="0원","review step has no reservation or trade");
  await controls();await press("dd-test-both");
  check(await q("dd-review").isVisible() && await q("dd-tab-order").getAttribute("aria-selected")==="true","simultaneous event and call preserve review and screen");
  await press("dd-back");
  check(await q("dd-quantity").inputValue()==="3" && await cash()==="970,000원","back from review preserves draft and assets");
  await submit();
  check(await value("dd-reserved")==="16,170원","accepted buy reserves only its limit cost");
  await until("dd-receipt-title","체결 완료");
  check(await cash()==="953,830원" && await value("dd-held")==="9주" && await value("dd-average")==="5,130원","full execution updates actual assets and average basis");
  check(await value("dd-reserved")==="0원" && await value("dd-left")==="3,830원","filled order releases escrow and respects plan budget");
  check(await q("dd-receipt").isVisible() && !await q("dd-draft").isVisible(),"receipt replaces draft instead of showing misleading normal-state errors");
  await screenshot("prototype-order-complete");
  await press("dd-show-fills");
  check((await value("dd-fills")).includes("#0143") && (await value("dd-fills")).includes("체결 완료"),"individual fill history linked to submitted order");
  check((await value("dd-event-list")).includes("시장 급변") && (await value("dd-buy-share")).includes("매수 주도"),"event priority and aggressor-side meaning shown");
  await screenshot("prototype-events");

  await reset();await press("dd-test-liquidity");await submit();
  await until("dd-receipt-title","부분 체결");
  check(await value("dd-held")==="7주" && await value("dd-reserved")==="10,780원","thin liquidity produces a real partial UI ledger");
  await press("dd-cancel");
  check(await cash()==="964,610원" && await value("dd-held")==="7주" && await value("dd-reserved")==="0원","cancellation releases only unfilled funds and preserves the fill");
  await sleep(1100);
  check(await value("dd-held")==="7주","canceled order does not fill again");

  await reset();await q("dd-split").check();await q("dd-parts").fill("3");
  await submit();await until("dd-receipt-title","부분 체결");
  const splitStart=Date.now();
  check(await value("dd-held")==="7주","first one-share slice executed");
  await until("dd-receipt-title","체결 완료");
  check(Date.now()-splitStart>=5000 && await value("dd-held")==="9주","three slices execute at real three-second intervals");
  await press("dd-show-fills");
  check((await value("dd-fills")).match(/#0143/g).length===3,"split executions produce three distinct fill records");

  await reset();await press("dd-review-button");await press("dd-test-reject");await press("dd-submit");
  check((await value("dd-submit-error")).includes("주문 거절") && await cash()==="970,000원" && await value("dd-reserved")==="0원","submission failure explains cause without changing assets");
  await screenshot("prototype-submission-error");
  await press("dd-submit");await until("dd-receipt-title","체결 완료");
  check(await value("dd-held")==="9주","explicit retry succeeds once without duplicate order");

  await reset();await q("dd-price").fill("5380");await submit();await sleep(650);
  check(await value("dd-held")==="6주" && await value("dd-reserved")==="16,140원","nonmarketable limit waits instead of fabricating a fill");
  await press("dd-test-quote");await until("dd-receipt-title","체결 완료");
  check(await cash()==="953,860원" && await value("dd-held")==="9주","quote change fills eligible resting order at actual example quote");

  await reset();await press("dd-sell");await q("dd-quantity").fill("7");
  check(await q("dd-review-button").isDisabled(),"sell quantity cannot exceed owned shares");
  await q("dd-quantity").fill("2");await q("dd-price").fill("5380");
  check((await value("dd-after-held")).includes("4주"),"sell preview decreases holdings");
  await submit();await until("dd-receipt-title","체결 완료");
  check(await cash()==="980,760원" && await value("dd-held")==="4주","sell settlement updates cash and holdings");

  await reset();await press("dd-tab-phone");
  check(await q("dd-incoming").isVisible(),"incoming phone has one concise visible identity warning");
  await screenshot("prototype-phone");
  const firstRemaining=parseInt(await value("dd-countdown"));
  await press("dd-lower");await sleep(1400);
  check(await q("dd-tab-order").getAttribute("aria-selected")==="true" && (await value("dd-call-hint")).includes("초"),"closing phone keeps ringing and returns to market");
  await press("dd-tab-phone");
  check(parseInt(await value("dd-countdown"))<firstRemaining,"reopening phone does not reset response deadline");
  await press("dd-decline");
  check(await value("dd-call-result-title")==="수신 거절" && await value("dd-call-hint")==="","decline ends incoming call");
  await press("dd-test-both");await press("dd-answer");
  check(await value("dd-call-result-title")==="통화 기록" && (await value("dd-call-result-text")).includes("확인된 공시"),"answer records the unverified claim");

  await reset();await q("dd-price").focus();await page.keyboard.press("3");
  check(await q("dd-tab-order").getAttribute("aria-selected")==="true","numeric input does not invoke phone shortcut");
  await reset();await press("dd-tab-phone");await q("dd-lower").focus();await page.keyboard.press("Escape");
  check(await q("dd-tab-order").getAttribute("aria-selected")==="true","Escape closes phone while preserving market navigation");

  for (const width of [1280,768,375,320]) {
    await page.setViewportSize({width,height:1100});
    await reset();await q("dd-split").check();
    for (const name of ["order","events","phone"]) {
      await press("dd-tab-"+name);
      const size=await frame.evaluate(()=>{
        const root=document.getElementById("dondon-interactions"),box=root.getBoundingClientRect();
        const shell=root.querySelector(".dd-shell");
        return {rootWidth:box.width,scrollWidth:root.scrollWidth,shellWidth:shell.clientWidth,shellScroll:shell.scrollWidth,
          tooSmall:[...root.querySelectorAll("button")].filter(e=>e.getClientRects().length).filter(e=>e.getBoundingClientRect().height<44).map(e=>e.id)};
      });
      report.widths.push({viewport:width,screen:name,...size});
      check(size.scrollWidth<=size.rootWidth+2 && size.shellScroll<=size.shellWidth+2,"no horizontal overflow at "+width+" / "+name,JSON.stringify(size));
      check(size.tooSmall.length===0,"buttons at least 44px high at "+width+" / "+name);
      if(width===375 && name==="order"){await screenshot("prototype-order-375");}
    }
  }
  const staleSnapshot=await frame.evaluate(()=>window.openai?.widgetState);
  await page.setViewportSize({width:1280,height:1100});await reset();
  await frame.evaluate(saved=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:saved}}})),staleSnapshot);
  check(await q("dd-tab-order").getAttribute("aria-selected")==="true" && !await q("dd-split").isChecked(),"delayed own state echo cannot undo reset or restore an old draft");
  await screenshot("prototype-order-default");
  check(report.pageErrors.length===0,"no browser JavaScript runtime errors",report.pageErrors.join("; "));
  report.status="PASS";
})().catch(async error=>{
  report.status="FAIL";report.error=String(error.stack||error);console.error(report.error);
  if(frame) {
    report.failureDom=await frame.evaluate(()=>({
      cash:document.querySelector("#dd-cash").textContent,held:document.querySelector("#dd-held").textContent,
      draftHidden:document.querySelector("#dd-draft").hidden,reviewHidden:document.querySelector("#dd-review").hidden,
      receiptHidden:document.querySelector("#dd-receipt").hidden,widget:window.openai?.widgetState,clicks:window.ddTestClicks
    })).catch(()=>null);
    await frame.locator("#dondon-interactions").screenshot({path:path.join(out,"prototype-failure.png")}).catch(()=>{});
  }
  process.exitCode=1;
}).finally(async()=>{
  if(browser){await browser.close();}
  report.finished=new Date().toISOString();
  fs.writeFileSync(path.join(out,"interaction-verification.json"),JSON.stringify(report,null,2));
  console.log(report.status+" "+report.checks.length+" checks");
});
