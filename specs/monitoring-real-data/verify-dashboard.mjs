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

  // ---- 8: nguon khong con so khoi viet cung
  //
  // Bay con so nay tung nam trong HTML tinh cua luoi khoi va KHONG ham nao ghi
  // de — do la ly do chung nguy hiem: chung dung canh so THAT (112/112 thiet
  // bi) nen mat thuong khong phan biet duoc.
  {
    const src = readFileSync(SOURCE, "utf8");
    const bia = ["16/20", "18/20", "14/20", "10/20", "13/24", "15/24", "5/16",
                 "Block A01", "2 block l\u1ed7i/offline"];
    const con = bia.filter((c) => src.includes(c));
    ghi("nguon_khong_con_so_khoi_bia", con.length === 0,
        con.length ? `con ${JSON.stringify(con)}` : `sach ca ${bia.length} moc`);
  }

  // ---- 9: tung the khoi khop /Monitor/BlockMap
  {
    const api = await json("http://localhost:8080/Monitor/BlockMap");
    const khoiApi = (api.blocks || []).slice().sort((a, b) => a.block_no - b.block_no);

    const pK = await browser.newPage();
    await pK.setViewport({ width: 1700, height: 1200 });
    await pK.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
    await pK.waitForFunction(
      `document.querySelectorAll('#dashBlocksGrid a').length > 0`, { timeout: 20000 });
    const dom = await pK.evaluate(() =>
      [...document.querySelectorAll("#dashBlocksGrid a")].map((a) => ({
        chu: a.innerText.replace(/\n/g, " | "),
        href: a.getAttribute("href"),
      })));
    const demLoi = await pK.evaluate(() =>
      (document.getElementById("dashKhoiLoi") || {}).innerText || "");
    await pK.close();

    const sai = [];
    if (dom.length !== khoiApi.length)
      sai.push(`ve ${dom.length} the, API co ${khoiApi.length}`);
    khoiApi.forEach((b, i) => {
      const t = (dom[i] || {}).chu || "";
      const pct = b.slots > 0 ? Math.round(b.occupied / b.slots * 100) : 0;
      if (!t.startsWith("Kh\u1ed1i " + b.block_no + " |"))
        sai.push(`vi tri ${i}: "${t.slice(0, 24)}" khong phai khoi ${b.block_no}`);
      if (!t.includes(`${b.occupied}/${b.slots}`))
        sai.push(`khoi ${b.block_no}: thieu ${b.occupied}/${b.slots}`);
      if (!t.includes(pct + "%")) sai.push(`khoi ${b.block_no}: thieu ${pct}%`);
      if ((dom[i] || {}).href !== "/Home/Zones/" + b.zone_id)
        sai.push(`khoi ${b.block_no}: lien ket sai`);
    });
    // Doi chung am: bai co o co xe ma khong the nao hien so khac 0 thi phep
    // kiem nay chi dang chung minh "moi khoi deu rong", khong chung minh gi.
    const tongXe = khoiApi.reduce((n, b) => n + (b.occupied || 0), 0);
    const theCoXe = dom.filter((x) => !/ 0\/\d/.test(x.chu)).length;
    if (tongXe > 0 && theCoXe === 0)
      sai.push(`API bao ${tongXe} o co xe nhung khong the nao hien so khac 0`);

    ghi("luoi_khoi_khop_blockmap", sai.length === 0 && khoiApi.length > 0,
        sai.length ? sai.slice(0, 3).join("; ")
                   : `${khoiApi.length} khoi khop tung con so, dem="${demLoi}"`);
  }

  // ---- 10: chan /Monitor/BlockMap -> luoi phai SACH, khong giu the cu
  //
  // Tai binh thuong TRUOC roi moi chan, neu khong thi khong phan biet duoc
  // "da xoa" voi "chua kip ve".
  {
    const pB = await browser.newPage();
    await pB.setViewport({ width: 1700, height: 1200 });
    await pB.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
    await pB.waitForFunction(
      `document.querySelectorAll('#dashBlocksGrid a').length > 0`, { timeout: 20000 });
    const truoc = await pB.evaluate(() =>
      document.querySelectorAll("#dashBlocksGrid a").length);

    await pB.setRequestInterception(true);
    pB.on("request", (req) => {
      if (req.url().includes("/Monitor/BlockMap")) req.abort();
      else req.continue();
    });
    await new Promise((r) => setTimeout(r, 7000));   // qua vai nhip poll 5 giay
    const sau = await pB.evaluate(() => ({
      the: document.querySelectorAll("#dashBlocksGrid a").length,
      chu: (document.getElementById("dashBlocksGrid") || {}).innerText || "",
      dem: (document.getElementById("dashKhoiLoi") || {}).innerText || "",
    }));
    await pB.close();

    ghi("loi_thi_xoa_luoi_khoi",
        truoc > 0 && sau.the === 0 && /kh\u00f4ng \u0111\u1ecdc \u0111\u01b0\u1ee3c/i.test(sau.chu) &&
        sau.dem.trim() === GACH,
        `truoc ${truoc} the; sau khi chan ${sau.the} the, dem="${sau.dem.trim()}"`);
  }
} catch (e) {
  console.log("  LOI KHI DO: " + e.message);
  ketQua.push({ ten: "do_trinh_duyet", dat: false });
} finally {
  if (browser) await browser.close();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exit(rot.length === 0 ? 0 : 1);
