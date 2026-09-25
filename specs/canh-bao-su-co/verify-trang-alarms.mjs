// Kiem chung task-02 — trang Alarms doc du lieu that, tu lam moi.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-trang-alarms.mjs"

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const SRC = join(GOC, "TotalParking", "Views", "Home", "Alarms.cshtml");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\Views\\Home\\Alarms.cshtml";
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const TRANG = "http://localhost:8080/Home/Alarms";
const API = "http://localhost:8080/Alarm/List";

const DAU = "TEST-" + Date.now().toString(36).toUpperCase();
const NHIP_MS = 15000;

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString(), dau: DAU };
function ghi(ten, dat, ct) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${ct ? "  | " + ct : ""}`);
}
function sql(cau) {
  const out = execFileSync(MYSQL,
    ["-u", "root", "-h", "127.0.0.1", "-D", "total_parking", "-N", "-B", "-e", cau],
    { encoding: "utf8", timeout: 60000, env: { ...process.env, MYSQL_PWD: "12345678" } });
  return out.trim().split(/\r?\n/).filter((d) => d.length).map((d) => d.split("\t"));
}
const mot = (c) => sql(c)[0][0];
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "trang-alarms.json"), JSON.stringify(soLieu, null, 1), "utf8");

// --------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
if (bam(SRC) !== bam(DEPLOY)) {
  ghi("deploy_khop_source", false, "Alarms.cshtml source != deploy");
  console.log("\nCHUA DEPLOY — chep Alarms.cshtml sang ban deploy roi chay lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, bam(SRC).slice(0, 12));

// ------------------------------- 1. khong con du lieu cung trong HTML
//
// KHONG so chuoi '<tr class="alarm-row">' — markup that co them nhieu class nen
// chuoi do khong ton tai va phep kiem se PASS gia. Va KHONG cam chuoi
// 'alarm-row', vi template JS bat buoc phai sinh lai class do cho bo loc chay.
{
  const src = readFileSync(SRC, "utf8");
  const iMo = src.indexOf('<tbody id="alarmsTableBody">');
  const iDong = src.indexOf("</tbody>", iMo);
  const trTinh = iMo >= 0 ? (src.slice(iMo, iDong).match(/<tr/g) || []).length : -1;

  const CHUOI_CUNG = ["E-0408", "Safety-Relay", "4 hoạt động", "3 sự cố", "operator01"];
  const conSot = CHUOI_CUNG.filter((c) => src.includes(c));

  soLieu.du_lieu_cung = { tr_tinh: trTinh, con_sot: conSot };
  ghi("khong_con_du_lieu_cung", trTinh === 0 && conSot.length === 0,
      `<tr tinh trong tbody: ${trTinh}` +
      (conSot.length ? `, con chuoi cung: ${conSot.join(", ")}` : ", khong con chuoi cung"));
}

const soDongTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));
soLieu.so_dong_truoc = soDongTruoc;
luu();

let browser;
try {
  // Ba canh bao thu: hai muc khac nhau de kiem duoc bo loc.
  sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, block_no, zone_id, thiet_bi, mo_ta) VALUES
       ('hardware','critical','${DAU}-1', 21, 2, 'PLC 21', 'Canh bao thu muc nghiem trong'),
       ('hardware','high','${DAU}-2', 22, 2, 'PLC 22', 'Canh bao thu muc cao'),
       ('operation','low','${DAU}-3', 23, 3, 'HMI 23', 'Canh bao thu muc thap')`);

  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ------------------------------------------- 2. so dong khop API
  const p = await browser.newPage();
  await p.setViewport({ width: 1600, height: 1000 });
  await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
  await p.waitForFunction(`document.querySelectorAll(".alarm-row").length > 0`, { timeout: 20000 });

  const api = await (await fetch(API, { cache: "no-store" })).json();
  const soTrang = await p.evaluate(() => document.querySelectorAll(".alarm-row").length);
  soLieu.so_dong = { trang: soTrang, api: api.tong };
  ghi("so_dong_khop_api", soTrang === api.tong, `trang ${soTrang} vs api ${api.tong}`);

  // ------------------------------------------------- 3. bo loc van chay
  //
  // Phep kiem nay bat duoc loi ma so_dong_khop_api khong bat: dong render thieu
  // data-severity thi so dong van dung, nhung loc xong bang TRONG.
  {
    const mucDo = [...new Set((api.canh_bao || []).map((c) => c.muc_do))];
    const sai = [];
    for (const m of mucDo) {
      await p.select("#filterSeverity", m);
      await ngu(200);
      const hien = await p.evaluate(() =>
        [...document.querySelectorAll(".alarm-row")].filter((r) => r.style.display !== "none").length);
      const mong = (api.canh_bao || []).filter((c) => c.muc_do === m).length;
      if (hien !== mong) sai.push(`${m}: trang ${hien} vs api ${mong}`);
    }
    await p.select("#filterSeverity", "all");
    soLieu.bo_loc = { da_thu: mucDo, sai };
    ghi("bo_loc_van_chay", sai.length === 0 && mucDo.length > 0,
        sai.length ? sai.join("; ") : `da thu ${mucDo.length} muc: ${mucDo.join(", ")}`);
  }

  // ------------------------- 4. tu lam moi khi co canh bao moi
  //
  // Chen canh bao SAU khi trang da tai, roi cho qua mot chu ky. Khong co phep
  // kiem nay thi man hinh tuong hien du lieu cu ca dem ma khong ai biet.
  {
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, mo_ta)
         VALUES ('maintenance','medium','${DAU}-4','Canh bao chen sau khi trang da tai')`);
    const thay = await p.waitForFunction(
      `[...document.querySelectorAll(".alarm-row")].some(r => r.innerText.includes("${DAU}-4"))`,
      { timeout: NHIP_MS + 10000 }).then(() => true).catch(() => false);

    const capNhat = await p.evaluate(() =>
      (document.getElementById("alarm-cap-nhat") || {}).textContent || "");
    ghi("tu_lam_moi_khi_co_canh_bao_moi", thay && /Cập nhật/.test(capNhat),
        `${thay ? "da tu hien" : "KHONG tu hien"}, dau hieu cap nhat: "${capNhat.trim()}"`);
  }

  // ------------------------------------- 5. nut xac nhan ghi duoc CSDL
  {
    const id = Number(mot(`SELECT canh_bao_id FROM canh_bao WHERE ma_loi='${DAU}-1'`));
    p.on("dialog", async (d) => {
      // prompt xin ten -> tra loi; alert -> dong.
      if (d.type() === "prompt") await d.accept("Nguoi Kiem Thu");
      else await d.accept();
    });
    await p.evaluate((i) => window.xacNhan({ disabled: false }, i), id);
    await ngu(2500);
    const row = sql(`SELECT COALESCE(xac_nhan_boi,'-'), IF(xac_nhan_luc IS NULL,'trong','co')
                       FROM canh_bao WHERE canh_bao_id=${id}`)[0];
    ghi("nut_xac_nhan_ghi_duoc", row[1] === "co" && row[0] === "Nguoi Kiem Thu",
        `xac_nhan_boi="${row[0]}", xac_nhan_luc=${row[1]}`);
  }

  await p.screenshot({ path: join(ARTIFACTS, "trang-alarms.png") }).catch(() => {});
  await p.close();

  // ------------------------------ 6. trang rong KHAC trang loi
  //
  // Chan loi goi API roi tai trang: phai hien thong bao loi, KHONG phai bang
  // rong im lang. Ky thuat nay da chung minh chay duoc o packet monitoring.
  {
    const p2 = await browser.newPage();
    await p2.setRequestInterception(true);
    p2.on("request", (req) => {
      if (req.url().includes("/Alarm/List")) req.respond({ status: 503, body: "loi gia lap" });
      else req.continue();
    });
    await p2.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(2000);
    const than = await p2.evaluate(() =>
      (document.getElementById("alarmsTableBody") || {}).innerText || "");
    ghi("trang_rong_khac_trang_loi", /Không đọc được/.test(than),
        than.trim() ? `than bang: "${than.trim().slice(0, 60)}"` : "than bang TRONG — nuot loi im lang");
    await p2.close();
  }
} catch (e) {
  ghi("phep_thu_trang_alarms", false, e.message);
} finally {
  if (browser) await browser.close();
  try {
    sql(`DELETE FROM canh_bao WHERE ma_loi LIKE '${DAU}-%'`);
    const sau = Number(mot("SELECT COUNT(*) FROM canh_bao"));
    soLieu.so_dong_sau = sau;
    ghi("don_sach_du_lieu_thu", sau === soDongTruoc, `so dong ${soDongTruoc} -> ${sau}`);
  } catch (e) {
    ghi("don_sach_du_lieu_thu", false, "KHONG DON DUOC: " + e.message +
        ` — chay tay: DELETE FROM canh_bao WHERE ma_loi LIKE '${DAU}-%';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
