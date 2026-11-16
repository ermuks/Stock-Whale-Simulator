const fs = require("fs");
const path = require("path");
const { chromium } = require("C:/Users/saerm/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const report = { status: "RUNNING", checks: [], measurements: [], pageErrors: [] };
let browser;
function check(value, label) {
  if (!value) { throw new Error(label); }
  report.checks.push(label);
}
(async () => {
  browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true,
    args: ["--disable-gpu", "--renderer-process-limit=1", "--disable-extensions", "--disable-background-networking", "--disable-component-update"] });
  const page = await browser.newPage({ viewport: { width: 1024, height: 1400 }, deviceScaleFactor: 1, reducedMotion: "reduce" });
  page.on("pageerror", error => report.pageErrors.push(error.message));
  await page.goto("file:///" + path.join(__dirname, "prototype-preview.html").replaceAll("\\", "/"));
  const frame = page.frames().find(f => f.parentFrame());
  const q = id => frame.locator("#" + id);
  await q("dondon-interactions").waitFor();
  async function capture(name) {
    await page.waitForTimeout(250);
    const bounds = await frame.evaluate(() => ({
      rootHeight: document.getElementById("dondon-interactions").getBoundingClientRect().height,
      rootBottom: document.getElementById("dondon-interactions").getBoundingClientRect().bottom,
      innerHeight, scrollHeight: document.documentElement.scrollHeight,
      shellHeight: document.querySelector(".dd-shell").getBoundingClientRect().height
    }));
    const wrapper = await page.locator("iframe").boundingBox();
    report.measurements.push({ name, ...bounds, iframeHeight: wrapper.height });
    check(bounds.innerHeight >= bounds.rootBottom, name + ": host frame contains the entire interface");
    await frame.locator(".dd-shell").screenshot({ path: path.join(__dirname, name + ".png") });
  }
  await capture("prototype-order-1024");
  await q("dd-review-button").click();
  check(await q("dd-review").isVisible(), "native focus preserves confirmation navigation");
  await q("dd-back").click();
  await q("dd-tab-events").click();
  await frame.locator('[data-event="sale"]').click();
  check((await q("dd-buy-share").textContent()).includes("43%"), "selected event uses its own buy aggressor ratio");
  const definition = frame.locator("#dd-events details").first();
  await definition.locator("summary").click();
  check(!(await definition.textContent()).includes("58%"), "ratio definition contains no stale event-specific percentage");
  check(await frame.locator('[data-event="sale"]').evaluate(el => el.classList.contains("cursor-interaction")), "new event controls use the host cursor preference");
  await q("dd-tab-order").click();
  await q("dd-split").check();
  await page.setViewportSize({ width: 375, height: 2600 });
  await capture("prototype-order-375");
  check(await q("dd-review-button").isVisible(), "narrow split order retains the confirmation action");
  check(report.pageErrors.length === 0, "no browser runtime errors after final presentation changes");
  report.status = "PASS";
})().catch(error => { report.status = "FAIL"; report.error = String(error.stack || error); process.exitCode = 1; })
  .finally(async () => {
    if (browser) { await browser.close(); }
    report.finished = new Date().toISOString();
    fs.writeFileSync(path.join(__dirname, "preview-verification.json"), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
  });
