// Kiem chung task-01 — Dashboard Tong quan dung du lieu that.
//
// Bay kiem tra co ten, in PASS/FAIL tung cai, thoat 0 chi khi ca bay PASS.
// Chay: node specs/monitoring-real-data/verify-dashboard.mjs
//
// Diem quan trong: kpi_stub_tong_bang_0 bat buoc phai co. Du lieu that hien
// tai co in_use = 0 o ca 6 zone, nen cong thuc SAI (free_mech + free_tier0 +
// free_ground) va cong thuc DUNG (total - in_use) cho ra CUNG mot ket qua.
// Chi stub moi phan biet duoc.

import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "../../.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const SOURCE = join(GOC, "TotalParking", "Views", "Home", "Index.cshtml");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\Views\\Home\\Index.cshtml";
const TRANG = "http://localhost:8080/Home/Index";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const GACH = "\u2014";

// Doc moc tu baseline-localstorage.txt — sinh tu ban TRUOC khi sua, khong phai
// chuoi doan tay. So NGUYEN VAN tung dong: dem khong doi ma shape doi van pha
// Diagnostics va trang con lai (CLAUDE.md muc 2).
function mocLocalStorage(tenMuc) {
  const txt = readFileSync(join(__dirname, "baseline-localstorage.txt"), "utf8");
  const phan = txt.split(/^## /m).find((p) => p.startsWith(tenMuc));
  if (!phan) throw new Error("khong thay muc " + tenMuc + " trong baseline");
  return phan.split(/\r?\n/).filter((d) => d.includes("localStorage.")).map((d) => d.trim());
}

const ketQua = [];
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}

function hash(p) {
  return createHash("sha256").update(readFileSync(p)).digest("hex");
}

async function json(duong) {
  const r = await fetch(duong, { cache: "no-store" });
  if (!r.ok) throw new Error(`HTTP ${r.status} tu ${duong}`);
  return r.json();
}

// --------------------------------------------------------- 0. deploy khop chua
// Bat buoc chay truoc. Site phuc vu tu thu muc deploy, KHONG phai source; do
// ban deploy cu ma bao PASS la proof gia.
let hSource, hDeploy;
try {
  hSource = hash(SOURCE);
  hDeploy = hash(DEPLOY);
} catch (e) {
  ghi("deploy_khop_source", false, "khong doc duoc file: " + e.message);
  console.log("\nCHUA DEPLOY — dung lai, khong chay tiep.");
  process.exit(1);
}
if (hSource !== hDeploy) {
  ghi("deploy_khop_source", false, `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
  console.log("\nCHUA DEPLOY — copy Index.cshtml sang ban deploy roi chay lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

// ------------------------------------------------------- 1. localStorage nguyen van
// So NGUYEN VAN tung dong, khong so so dem: dem khong doi ma shape doi van pha
// Diagnostics va OperationControl (CLAUDE.md muc 2).
const MOC_LOCALSTORAGE = mocLocalStorage("Index.cshtml");
const dongLS = readFileSync(SOURCE, "utf8")
  .split(/\r?\n/)
  .filter((d) => d.includes("localStorage."))
  .map((d) => d.trim());
const lsKhop =
  dongLS.length === MOC_LOCALSTORAGE.length &&
  dongLS.every((d, i) => d === MOC_LOCALSTORAGE[i]);
ghi("localstorage_nguyen_van", lsKhop, `${dongLS.length} dong`);

// ------------------------------------------------------------------ do trinh duyet
const api = {};
let browser;
try {
  // Danh thuc app truoc khi do: lan goi nguoi DeviceStatus/Summary mat ~2,5s
  // (cache 15s trong DeviceProbeService), cac lan sau chi 1,3ms.
  await fetch("http://localhost:8080/PlcStatus/Index", { signal: AbortSignal.timeout(15000) });

  api.capacity = await json("http://localhost:8080/Monitor/RoutingState");
  api.device = await json("http://localhost:8080/DeviceStatus/Summary");

  browser = await puppeteer.launch({
    headless: "new",
    executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ---- 2..4: do tren du lieu that
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  await page.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
  await page.waitForFunction(
    `document.getElementById("kpi-total") && document.getElementById("kpi-total").textContent.trim() !== "\u2014"`,
    { timeout: 15000 }
  );

  const doc = await page.evaluate(() => ({
    total: document.getElementById("kpi-total").textContent.trim(),
    inuse: document.getElementById("kpi-inuse").textContent.trim(),
    free: document.getElementById("kpi-free").textContent.trim(),
    pct: document.getElementById("kpi-pct").textContent.trim(),
    plc: document.getElementById("dev-plc-text").textContent.trim(),
    camera: document.getElementById("dev-camera-text").textContent.trim(),
    recentIds: Array.from(document.querySelectorAll("#dash-recent-tbody tr")).map((tr) =>
      tr.getAttribute("data-event-id")
    ),
    coChuOnlineTran: /\bonline\b/i.test(document.body.innerText),
  }));

  const zones = api.capacity.zones || [];
  const tong = zones.reduce((a, z) => a + (z.total || 0), 0);
  const dung = zones.reduce((a, z) => a + (z.in_use || 0), 0);
  const kpiDat =
    doc.total === String(tong) &&
    doc.inuse === String(dung) &&
    doc.free === String(tong - dung) &&
    doc.pct === Math.round((dung / tong) * 100) + "%";
  ghi(
    "kpi_khop_routingstate",
    kpiDat,
    `trang ${doc.total}/${doc.inuse}/${doc.free}/${doc.pct} vs api ${tong}/${dung}/${tong - dung}/${Math.round((dung / tong) * 100)}%`
  );

  const nhomPlc = (api.device.groups || []).find((g) => g.key === "plc");
  const nhomCam = (api.device.groups || []).find((g) => g.key === "camera");
  const devDat = doc.plc === (nhomPlc?.detail ?? "") && doc.camera === (nhomCam?.detail ?? "");
  ghi("suc_khoe_khop_devicestatus", devDat, `plc="${doc.plc}" camera="${doc.camera}"`);

  const idApi = (api.capacity.recent || []).map((r) => r.event_id);
  const recentDat =
    doc.recentIds.length === idApi.length && doc.recentIds.every((v, i) => v === idApi[i]);
  ghi("recent_khop_event_id", recentDat, `${doc.recentIds.length} dong vs ${idApi.length} tu api`);

  ghi("nhan_khong_dung_online_tran", !doc.coChuOnlineTran,
      doc.coChuOnlineTran ? "trang co chu 'online' dung mot minh" : "khong co");

  await page.screenshot({ path: join(__dirname, "artifacts", "dashboard.png") }).catch(() => {});
  await page.close();

  // ---- 5: stub Σtotal = 0 -> phai hien em dash, khong phai NaN hay 0
  const pStub = await browser.newPage();
  await pStub.setRequestInterception(true);
  pStub.on("request", (req) => {
    if (req.url().includes("/Monitor/RoutingState")) {
      req.respond({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ zones: [], recent: [], now: "00:00:00" }),
      });
    } else req.continue();
  });
  await pStub.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
  await new Promise((r) => setTimeout(r, 1500));
  const stub = await pStub.evaluate(() => ({
    total: document.getElementById("kpi-total").textContent.trim(),
    pct: document.getElementById("kpi-pct").textContent.trim(),
  }));
  ghi("kpi_stub_tong_bang_0", stub.total === GACH && stub.pct === GACH,
      `total="${stub.total}" pct="${stub.pct}"`);
  await pStub.close();

  // ---- 6: stub co in_use > 0 -> PHAN BIET cong thuc dung voi cong thuc sai
  //
  // Day la kiem tra quan trong nhat cua ca script. Du lieu that hien co in_use=0
  // o ca 6 zone nen hai cong thuc cho cung ket qua; chi stub nay moi tach duoc.
  //   dung : free = Σtotal - Σin_use                       = 200 - 30 = 170
  //   sai  : free = Σfree_mech + Σfree_tier0 + Σfree_ground = 70+20+50 = 140
  const pCt = await browser.newPage();
  await pCt.setRequestInterception(true);
  pCt.on("request", (req) => {
    if (req.url().includes("/Monitor/RoutingState")) {
      req.respond({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          now: "00:00:00",
          recent: [],
          zones: [
            { zone_id: 1, code: "Z1", total: 120, in_use: 20,
              free_mech: 40, free_tier0: 10, free_ground: 30, used_pct: 17 },
            { zone_id: 2, code: "Z2", total: 80, in_use: 10,
              free_mech: 30, free_tier0: 10, free_ground: 20, used_pct: 13 },
          ],
        }),
      });
    } else req.continue();
  });
  await pCt.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
  await new Promise((r) => setTimeout(r, 1500));
  const ct = await pCt.evaluate(() => ({
    total: document.getElementById("kpi-total").textContent.trim(),
    inuse: document.getElementById("kpi-inuse").textContent.trim(),
    free: document.getElementById("kpi-free").textContent.trim(),
    pct: document.getElementById("kpi-pct").textContent.trim(),
  }));
  const ctDat =
    ct.total === "200" && ct.inuse === "30" && ct.free === "170" && ct.pct === "15%";
  ghi(
    "kpi_stub_cong_thuc_con_trong",
    ctDat,
    `free="${ct.free}" (dung=170, cong thuc sai se ra 140)`
  );
  await pCt.close();

  // ---- 7: chan han endpoint -> o KPI phai ve em dash, khong giu so cu
  const pLoi = await browser.newPage();
  await pLoi.setRequestInterception(true);
  pLoi.on("request", (req) => {
    if (req.url().includes("/Monitor/RoutingState")) req.abort();
    else req.continue();
  });
  await pLoi.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
  await new Promise((r) => setTimeout(r, 2000));
  const loi = await pLoi.evaluate(() => ({
    total: document.getElementById("kpi-total").textContent.trim(),
    free: document.getElementById("kpi-free").textContent.trim(),
    trangThai: document.getElementById("dash-status").textContent.trim(),
  }));
  ghi(
    "loi_dat_o_ve_gach_ngang",
    loi.total === GACH && loi.free === GACH && /lỗi/i.test(loi.trangThai),
    `total="${loi.total}" free="${loi.free}" trangThai="${loi.trangThai}"`
  );
  await pLoi.close();
} catch (e) {
  console.log("  LOI KHI DO: " + e.message);
  ketQua.push({ ten: "do_trinh_duyet", dat: false });
} finally {
  if (browser) await browser.close();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exit(rot.length === 0 ? 0 : 1);
