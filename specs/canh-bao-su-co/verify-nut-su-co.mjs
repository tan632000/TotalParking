// Kiem chung task-04 — nut ghi nhan su co that thay cho nut ESTOP gia.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-nut-su-co.mjs"
//
// Phep kiem quan trong nhat la mau_khong_phu_thuoc_trinh_duyet: mot cai dat van
// to o do bang localStorage se PASS het cac phep con lai. Chinh o do cuc bo la
// thu tao cam giac "da xu ly" ma task nay muon diet.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const VIEW_SRC = join(GOC, "TotalParking", "Views", "Home", "OperationControl.cshtml");
const VIEW_DEPLOY = join(DEPLOY, "Views", "Home", "OperationControl.cshtml");
const DLL_SRC = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = join(DEPLOY, "bin", "TotalParking.dll");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const TRANG = "http://localhost:8080/Home/OperationControl";
const API = "http://localhost:8080/Alarm/List";

const MA_LOI = "SU-CO-KHAN";

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString() };
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
const mot = (c) => { const r = sql(c); return r.length ? r[0][0] : null; };
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "nut-su-co.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
{
  const lech = [];
  for (const [ten, a, b] of [["OperationControl.cshtml", VIEW_SRC, VIEW_DEPLOY],
                             ["TotalParking.dll", DLL_SRC, DLL_DEPLOY]]) {
    try { if (bam(a) !== bam(b)) lech.push(ten); }
    catch (e) { lech.push(ten + " (" + e.message + ")"); }
  }
  if (lech.length) {
    ghi("deploy_khop_source", false, "chua deploy: " + lech.join(", "));
    console.log("\nCHUA DEPLOY — chep bin\\ va .cshtml sang ban deploy roi chay lai.");
    process.exit(1);
  }
  ghi("deploy_khop_source", true, bam(VIEW_SRC).slice(0, 12));
}

const nguon = readFileSync(VIEW_SRC, "utf8");

// ------------------------------------- 1. nhan khong hua dung may
//
// Doc DUNG MOT FILE. Grep ca Views/ se FAIL oan o Safety.cshtml:122
// ("E-Stop khong bi nhan") — do la muc checklist an toan phai giu.
{
  const CAM = ["ESTOP", "dừng khẩn", "emergency stop"];
  const conSot = CAM.filter((c) => nguon.toLowerCase().includes(c.toLowerCase()));
  const coGiaiThich = /ghi nhận/i.test(nguon);
  soLieu.nhan = { con_sot: conSot, co_giai_thich: coGiaiThich };
  ghi("nhan_khong_hua_dung_may", conSot.length === 0 && coGiaiThich,
      (conSot.length ? "con chuoi: " + conSot.join(", ") : "khong con chuoi hua dung may") +
      `, co chu thich "ghi nhan": ${coGiaiThich}`);
}

// ------------------------------------- 2. guard bao tri con nguyen
//
// Bo guard nay thi khoi dang khoa bao tri lai nhan duoc thao tac doi che do.
{
  const con = /status === "Disabled"[\s\S]{0,200}?khóa bảo trì/.test(nguon);
  soLieu.guard_bao_tri = con;
  ghi("guard_bao_tri_con_nguyen", con,
      con ? "nhanh chan khi status === \"Disabled\" van con"
          : "KHONG THAY nhanh chan bao tri trong setBlockMode");
}

const soDongTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));
const idTruoc = Number(mot("SELECT COALESCE(MAX(canh_bao_id),0) FROM canh_bao"));
soLieu.id_truoc = idTruoc;
luu();

let browser, khoiThu = null;
try {
  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ------------------------------------- 3. bam nut ghi duoc CSDL
  const p = await browser.newPage();
  await p.setViewport({ width: 1600, height: 1000 });
  p.on("dialog", async (d) => { await d.accept(); });   // confirm + alert
  await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
  await p.waitForSelector("#btn-bao-su-co", { timeout: 20000 });

  // Doc tu o chon khoi, KHONG doc window.selectedBlockId: bien do khai bao
  // bang `let` o pham vi script nen khong gan vao window va se ra undefined.
  khoiThu = await p.evaluate(() => document.getElementById("occ-block-select").value);
  if (!khoiThu) throw new Error("khong doc duoc khoi dang chon tu #occ-block-select");
  const nhanNut = await p.evaluate(() =>
    document.getElementById("btn-bao-su-co").innerText.trim());

  await p.click("#btn-bao-su-co");
  await ngu(3000);

  const dong = sql(`SELECT canh_bao_id, muc_do, nguon, COALESCE(thiet_bi,''), COALESCE(zone_id,-1)
                      FROM canh_bao
                     WHERE canh_bao_id > ${idTruoc} AND ma_loi = '${MA_LOI}'
                     ORDER BY canh_bao_id DESC LIMIT 1`);
  const coDong = dong.length === 1 && dong[0][1] === "critical" && dong[0][3] === khoiThu;
  soLieu.dong_moi = dong.length ? {
    id: dong[0][0], muc_do: dong[0][1], nguon: dong[0][2],
    thiet_bi: dong[0][3], zone_id: dong[0][4]
  } : null;
  ghi("bam_nut_ghi_duoc_csdl", coDong,
      dong.length
        ? `id=${dong[0][0]}, muc_do=${dong[0][1]}, nguon=${dong[0][2]}, thiet_bi="${dong[0][3]}"` +
          (dong[0][3] === khoiThu ? "" : ` — KHAC khoi dang chon "${khoiThu}"`)
        : `khong co dong nao moi (nhan nut truoc khi bam: "${nhanNut}")`);

  // ------------------------- 4. trinh duyet KHAC doc duoc (goi API ngoai)
  //
  // Day la diem khac biet cot loi so voi nut cu: du lieu nam o MAY CHU. Goi
  // fetch tu node, khong qua trinh duyet vua bam.
  {
    const api = await (await fetch(API, { cache: "no-store" })).json();
    const thay = (api.canh_bao || []).some(
      (c) => c.ma_loi === MA_LOI && c.thiet_bi === khoiThu && c.chua_xac_nhan);
    soLieu.api_thay = thay;
    ghi("trinh_duyet_khac_doc_duoc", thay,
        thay ? `GET /Alarm/List (ngoai trinh duyet) co su co cua "${khoiThu}"`
             : `GET /Alarm/List KHONG thay su co cua "${khoiThu}" — du lieu con o localStorage`);
  }

  await p.screenshot({ path: join(ARTIFACTS, "nut-su-co.png") }).catch(() => {});
  await p.close();

  // --------------- 5. mau khong phu thuoc trinh duyet (context thu hai)
  //
  // Context rieng => localStorage rieng. Neu dau su co van hien thi o day thi
  // no den tu may chu; neu bien mat thi no chi la o do cuc bo cua may vua bam.
  {
    const ctx = browser.createBrowserContext
      ? await browser.createBrowserContext()
      : await browser.createIncognitoBrowserContext();
    const p2 = await ctx.newPage();
    await p2.setViewport({ width: 1600, height: 1000 });
    await p2.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });

    const hien = await p2.waitForFunction(
      `(() => { const c = [...document.querySelectorAll(".occ-block-card")]
                 .find(x => x.dataset.blockId === ${JSON.stringify(khoiThu)});
                return !!(c && c.querySelector(".occ-su-co-badge")); })()`,
      { timeout: 25000 }).then(() => true).catch(() => false);

    const sachLocal = await p2.evaluate(() => {
      try { return !localStorage.getItem("activeAlarms"); } catch { return true; }
    });

    soLieu.context_hai = { thay_dau_su_co: hien, localStorage_rong: sachLocal };
    ghi("mau_khong_phu_thuoc_trinh_duyet", hien && sachLocal,
        `context thu hai: dau su co ${hien ? "co hien" : "KHONG hien"}` +
        `, localStorage ${sachLocal ? "rong (dung la context rieng)" : "CO du lieu — khong phai context rieng"}`);
    await p2.close();
    await ctx.close().catch(() => {});
  }
} catch (e) {
  ghi("phep_thu_nut_su_co", false, e.message);
} finally {
  if (browser) await browser.close();
  try {
    sql(`DELETE FROM canh_bao WHERE canh_bao_id > ${idTruoc} AND ma_loi = '${MA_LOI}'`);
    const sau = Number(mot("SELECT COUNT(*) FROM canh_bao"));
    soLieu.so_dong = { truoc: soDongTruoc, sau };
    ghi("don_sach_du_lieu_thu", sau === soDongTruoc, `so dong ${soDongTruoc} -> ${sau}`);
  } catch (e) {
    ghi("don_sach_du_lieu_thu", false, "KHONG DON DUOC: " + e.message +
        ` — chay tay: DELETE FROM canh_bao WHERE canh_bao_id > ${idTruoc} AND ma_loi='${MA_LOI}';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
