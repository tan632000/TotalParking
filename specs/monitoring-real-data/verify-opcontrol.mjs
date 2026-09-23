// Kiem chung task-02 — Dieu khien van hanh hien trang thai block that.
//
// Sau kiem tra co ten, in PASS/FAIL tung cai, thoat 0 chi khi ca sau PASS.
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"

import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "../../.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const SOURCE = join(GOC, "TotalParking", "Views", "Home", "OperationControl.cshtml");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\Views\\Home\\OperationControl.cshtml";
const TRANG = "http://localhost:8080/Home/OperationControl";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";

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
const hash = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");

// --------------------------------------------------------- 0. deploy khop chua
let hSource, hDeploy;
try {
  hSource = hash(SOURCE);
  hDeploy = hash(DEPLOY);
} catch (e) {
  ghi("deploy_khop_source", false, "khong doc duoc file: " + e.message);
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
if (hSource !== hDeploy) {
  ghi("deploy_khop_source", false, `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
  console.log("\nCHUA DEPLOY — copy OperationControl.cshtml sang ban deploy roi chay lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

const src = readFileSync(SOURCE, "utf8");

// ------------------------------------------------ 1. localStorage nguyen van
// 18 dong moc, so NGUYEN VAN. Dem khong doi ma shape doi van pha Diagnostics
// va Index (CLAUDE.md muc 2), nen so chuoi chu khong so so luong.
const MOC_LS = mocLocalStorage("OperationControl.cshtml");
const dongLS = src.split(/\r?\n/).filter((d) => d.includes("localStorage.")).map((d) => d.trim());
const lsSoLuongDat = dongLS.length === MOC_LS.length;
const lsChuoiDat = dongLS.every((d, i) => d === MOC_LS[i]);
ghi("localstorage_nguyen_van", lsSoLuongDat && lsChuoiDat,
    `${dongLS.length}/${MOC_LS.length} dong`);

// ---------------------------------------- 2. nut dieu khien con nguyen
const NUT = [
  "btn-action-start", "btn-action-stop", "btn-action-pause", "btn-action-resume",
  "btn-action-bypass", "btn-action-disable", "btn-action-lock", "btn-action-override",
  "btn-action-unlock", "btn-action-trigger-fault",
  "btn-mode-auto", "btn-mode-manual", "btn-mode-maintenance", "btn-mode-emergency",
  "btn-plc-send", "btn-recovery-reset", "btn-start-recovery",
];
const thieu = NUT.filter((id) => !src.includes(`id="${id}"`));
ghi("nut_dieu_khien_con_nguyen", thieu.length === 0,
    thieu.length ? "thieu: " + thieu.join(", ") : `du ${NUT.length} nut`);

// ------------------------------------------------------------------ do trinh duyet
let browser;
try {
  await fetch("http://localhost:8080/PlcStatus/Index", { signal: AbortSignal.timeout(15000) });
  const plc = await (await fetch("http://localhost:8080/PlcStatus/Index", { cache: "no-store" })).json();

  browser = await puppeteer.launch({
    headless: "new",
    executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  await page.goto(TRANG, { waitUntil: "networkidle2", timeout: 30000 });
  await page.waitForFunction(
    `document.getElementById("occ-block-doc-duoc") && document.getElementById("occ-block-doc-duoc").textContent.trim() !== "\u2014"`,
    { timeout: 20000 }
  );

  const doc = await page.evaluate(() => ({
    docDuoc: document.getElementById("occ-block-doc-duoc").textContent.trim(),
    choKichHoat: document.getElementById("occ-block-cho-kich-hoat").textContent.trim(),
    coChuOnlineTran: /\bonline\b/i.test(document.body.innerText),
  }));

  const blocks = plc.blocks || [];
  const song = blocks.filter((b) => b.online).length;
  ghi("block_khop_plcstatus", doc.docDuoc === `${song}/${blocks.length}`,
      `trang "${doc.docDuoc}" vs api "${song}/${blocks.length}"`);

  const cho = plc.chua_dua_vao_van_hanh ? plc.chua_dua_vao_van_hanh.so_luong : 0;
  ghi("cho_kich_hoat_hien_ra", doc.choKichHoat === String(cho),
      `trang "${doc.choKichHoat}" vs api "${cho}"`);

  ghi("nhan_dung_tu_vung", !doc.coChuOnlineTran,
      doc.coChuOnlineTran ? "trang co chu 'online' dung mot minh" : "khong co");

  await page.screenshot({ path: join(__dirname, "artifacts", "opcontrol.png") }).catch(() => {});
  await page.close();
} catch (e) {
  console.log("  LOI KHI DO: " + e.message);
  ketQua.push({ ten: "do_trinh_duyet", dat: false });
} finally {
  if (browser) await browser.close();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exit(rot.length === 0 ? 0 : 1);
