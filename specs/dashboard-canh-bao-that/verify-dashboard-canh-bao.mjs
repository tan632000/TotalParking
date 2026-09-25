// Kiem chung task-01 — Dashboard doc canh bao that, go sau nguon bia.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\dashboard-canh-bao-that\verify-dashboard-canh-bao.mjs"
//
// Ba phep kiem quan trong nhat:
//   - localstorage_co_rac_van_doc_may_chu: GIEO rac vao localStorage TRUOC khi
//     tai trang. Nhanh `return` som cu doc localStorage roi thoat, va no khong
//     chua chuoi cam nao — do tren ho so trinh duyet sach thi khong bao gio cham
//     toi no.
//   - khong_doc_duoc_khac_trang_thai_sach: doi KHANG DINH DUONG mot cau bao loi
//     co dinh. Chi khang dinh phu dinh thi mot cai dat giu localStorage lam cache
//     du phong van PASS trong khi man hinh hien du lieu chet.
//   - don_rac_gia_giu_lai_e_stop: don qua tay la cat dau vao cua ma tran an toan
//     ben trang Safety.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const SRC = join(GOC, "TotalParking", "Views", "Home", "Index.cshtml");
const SRC_OPC = join(GOC, "TotalParking", "Views", "Home", "OperationControl.cshtml");
const DEPLOY_INDEX = join(DEPLOY, "Views", "Home", "Index.cshtml");

const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const TRANG = "http://localhost:8080/Home/Index";
const API = "/Alarm/List";

const DAU = "DB-" + Date.now().toString(36).toUpperCase();
const CAU_LOI = "Không đọc được cảnh báo từ máy chủ";

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
const mot = (c) => { const r = sql(c); return r.length ? r[0][0] : null; };
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "dashboard-canh-bao.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

const DOC_BANG = `(() => {
  const g = (id) => document.getElementById(id);
  const tb = g('alarms-live-tbody');
  const rows = tb ? [...tb.querySelectorAll('tr')] : [];
  return {
    than: tb ? tb.innerText : "",
    so_dong: rows.filter(r => r.id && r.id.indexOf('alarm-tr-') === 0).length,
    ma_loi: rows.filter(r => r.id).map(r => (r.children[1] || {}).innerText || ""),
    nguon_phat: rows.filter(r => r.id).map(r => (r.children[3] || {}).innerText || ""),
    dem_op: (g('active-operator-count') || {}).textContent,
    dem_sys: (g('active-system-count') || {}).textContent,
    dem_mnt: (g('active-maint-count') || {}).textContent,
    bieu_ngu_an: g('global-alarm-banner') ? g('global-alarm-banner').classList.contains('hidden') : null,
    bieu_ngu_so: (g('global-alarm-banner-count') || {}).textContent || "",
    bieu_ngu_chi_tiet: (g('global-alarm-banner-detail') || {}).textContent || ""
  };
})()`;

// --------------------------------------------------- 0. deploy khop source
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
{
  let a, b;
  try { a = bam(SRC); b = bam(DEPLOY_INDEX); }
  catch (e) { ghi("deploy_khop_source", false, e.message); process.exit(1); }
  if (a !== b) {
    ghi("deploy_khop_source", false, `${a.slice(0, 12)} != ${b.slice(0, 12)}`);
    console.log("\nCHUA DEPLOY — chep Index.cshtml sang ban deploy roi chay lai.");
    process.exit(1);
  }
  ghi("deploy_khop_source", true, a.slice(0, 12));
}

// ------------------------------------------- 1. khong con nguon bia
{
  const src = readFileSync(SRC, "utf8");
  const CAM = ["SYS-FLT-", "OP-ERR-", "MNT-DUE-", "addRealtimeAlarm",
               "operatorErrorsPool", "systemFaultsPool", "maintenanceDuePool",
               "getRandomItem"];
  const conSot = CAM.filter((c) => src.includes(c));

  // getItem('activeAlarms') PHAI con dung mot lan — trong ham don rac. Ham do
  // bat buoc doc khoa (AC-10) nen khong the cam tuyet doi; hai lan tro len nghia
  // la duong dung bang van con doc no.
  const soLanDoc = (src.match(/getItem\('activeAlarms'\)/g) || []).length;

  // Chot chan cho viec xoa nham file ngoai pham vi.
  const opc = readFileSync(SRC_OPC, "utf8");
  const opcConNguyen = /function saveActiveAlarms/.test(opc) &&
                       (opc.match(/saveActiveAlarms\(\)/g) || []).length >= 4;

  soLieu.tinh = { con_sot: conSot, so_lan_doc_localstorage: soLanDoc, opc_con_nguyen: opcConNguyen };
  ghi("khong_con_nguon_bia", conSot.length === 0 && soLanDoc === 1 && opcConNguyen,
      (conSot.length ? "con: " + conSot.join(", ") : "sach 8 chuoi cam") +
      `; getItem('activeAlarms') ${soLanDoc} lan (mong 1)` +
      `; OperationControl con nguyen: ${opcConNguyen}`);
}

const soDongTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));
soLieu.so_dong_truoc = soDongTruoc;
luu();

let browser;
try {
  // Ba canh bao thu, ba nguon khac nhau. Dong thu ba KHONG co block_no — dung de
  // bat loi ghep thang thanh "Block null".
  sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, block_no, zone_id, thiet_bi, mo_ta) VALUES
       ('hardware','critical','${DAU}-1', 21, 2, 'PLC 21', 'Canh bao thu thiet bi'),
       ('operation','high','${DAU}-2', 22, 3, 'HMI 22', 'Canh bao thu thao tac'),
       ('maintenance','low','${DAU}-3', NULL, NULL, 'Bo phan thu', 'Canh bao thu bao tri')`);

  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ---- 2+3+4+5. gieo rac TRUOC khi tai, roi do bang / cot / bo dem / bieu ngu
  // Bon phan tu de tach duoc HAI luat giu doc lap. Neu chi gieo mot phan tu
  // mang ca `code` lan chuoi "E-Stop" thi mot cai dat bo han luat theo chuoi van
  // PASS — ma Safety.cshtml:256 do theo CHUOI, khong theo code.
  const RAC = [
    { id: "ALM-999001", time: "22:50:15", code: "SYS-FLT-11", type: 2,
      typeName: "Lỗi Hệ Thống", source: "Z3 · Block E-02", msg: "Rac gia con ket lai" },
    { id: "ALM-999002", time: "22:51:15", code: "SU-CO-KHAN", type: 1,
      typeName: "Lỗi Thao Tác", source: "Z1 · Block A-01", msg: "Ghi nhan su co khan" },
    { id: "ALM-999003", time: "22:52:15", type: 1,
      typeName: "Lỗi Thao Tác", source: "Z1 · Block A-02",
      msg: "Ghi nhan su co khan (E-Stop) tai Block A-02" },
    { id: "ALM-999004", time: "22:53:15", type: 3,
      typeName: "Khac", source: "Z4", msg: "Phan tu khong co truong code" },
  ];

  const p = await browser.newPage();
  await p.setViewport({ width: 1700, height: 1000 });
  await p.evaluateOnNewDocument((rac) => {
    try { localStorage.setItem("activeAlarms", JSON.stringify(rac)); } catch (e) { }
  }, RAC);

  let demGoiApi = 0;
  p.on("request", (r) => { if (r.url().includes(API)) demGoiApi++; });

  await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
  await p.waitForFunction(
    `[...document.querySelectorAll('#alarms-live-tbody tr')].some(r => (r.innerText||'').indexOf('${DAU}-1') >= 0)`,
    { timeout: 25000 }).catch(() => { });
  await ngu(1500);

  const api = await (await fetch("http://localhost:8080" + API, { cache: "no-store" })).json();
  const mongDoi = (api.canh_bao || []).filter((c) => c.chua_xac_nhan);
  const h = await p.evaluate(DOC_BANG);
  await p.screenshot({ path: join(ARTIFACTS, "dashboard-canh-bao.png") }).catch(() => {});

  // -------------------- 2. localStorage co rac van doc may chu
  {
    const coRac = h.than.includes("Rac gia con ket lai");
    soLieu.gieo_rac = { thay_rac_tren_bang: coRac, so_dong: h.so_dong };
    ghi("localstorage_co_rac_van_doc_may_chu", !coRac && h.so_dong === mongDoi.length,
        `rac gia hien tren bang: ${coRac}; so dong ${h.so_dong} vs api ${mongDoi.length}`);
  }

  // ------------------------------------------ 3. so dong khop CSDL
  {
    const tren = new Set(h.ma_loi.map((s) => s.trim()).filter((s) => s.length));
    const duoi = new Set(mongDoi.map((c) => c.ma_loi));
    const thieu = [...duoi].filter((m) => !tren.has(m));
    const thua = [...tren].filter((m) => !duoi.has(m));
    soLieu.ma_loi = { tren: [...tren].slice(0, 8), thieu, thua };
    // So CA so dong: 16 canh bao PLC deu mang cung mot ma, nen so tap ma loi
    // thoi thi mot cai dat khu trung lap chi ve 4 dong van PASS.
    ghi("so_dong_khop_csdl",
        thieu.length === 0 && thua.length === 0 && h.so_dong === mongDoi.length,
        `${h.so_dong} dong vs api ${mongDoi.length}; ${tren.size} ma tren bang vs ${duoi.size} tu api` +
        (thieu.length ? `; THIEU ${thieu.join(",")}` : "") +
        (thua.length ? `; THUA ${thua.join(",")}` : ""));
  }

  // ------------------------------------- 4. cot Nguon phat dung
  {
    const i3 = h.ma_loi.findIndex((m) => m.trim() === `${DAU}-3`);
    const i1 = h.ma_loi.findIndex((m) => m.trim() === `${DAU}-1`);
    const o3 = i3 >= 0 ? h.nguon_phat[i3].trim() : "";
    const o1 = i1 >= 0 ? h.nguon_phat[i1].trim() : "";
    const khongNull = i3 >= 0 && !/null/i.test(o3);
    const duBa = i1 >= 0 && o1.includes("Z2") && o1.includes("Block 21") && o1.includes("PLC 21");
    soLieu.nguon_phat = { thieu_block: o3, du_ba_phan: o1 };
    ghi("cot_nguon_phat_dung", khongNull && duBa,
        `thieu block_no -> "${o3}" (khong chua null: ${khongNull}); du ba phan -> "${o1}"`);
  }

  // ------------------------------------------- 5. ba bo dem khop
  {
    const csdl = Object.fromEntries(
      sql("SELECT nguon, COUNT(*) FROM canh_bao WHERE xac_nhan_luc IS NULL GROUP BY nguon"));
    const mong = { op: Number(csdl.operation || 0), sys: Number(csdl.hardware || 0),
                   mnt: Number(csdl.maintenance || 0) };
    const thay = { op: Number(h.dem_op), sys: Number(h.dem_sys), mnt: Number(h.dem_mnt) };
    const tong = thay.op + thay.sys + thay.mnt;
    soLieu.bo_dem = { mong, thay, tong, so_dong: h.so_dong };
    ghi("ba_bo_dem_khop",
        thay.op === mong.op && thay.sys === mong.sys && thay.mnt === mong.mnt &&
        tong === h.so_dong,
        `op ${thay.op}/${mong.op}, sys ${thay.sys}/${mong.sys}, mnt ${thay.mnt}/${mong.mnt}; ` +
        `tong ba bo dem ${tong} vs so dong ${h.so_dong}`);
  }

  // ------------------------------------------------ 6. bieu ngu khop
  {
    // So SO, khong so chuoi con: "13 canh bao".includes("3") la true, nen mot
    // bieu ngu dem nham ca tap tra ve van PASS.
    const khop = parseInt(h.bieu_ngu_so, 10) === mongDoi.length;
    const anDung = mongDoi.length === 0 ? h.bieu_ngu_an === true : h.bieu_ngu_an === false;
    soLieu.bieu_ngu = { so: h.bieu_ngu_so.trim(), chi_tiet: h.bieu_ngu_chi_tiet.trim(),
                        an: h.bieu_ngu_an, mong_doi: mongDoi.length };
    ghi("bieu_ngu_khop", khop && anDung,
        `"${h.bieu_ngu_so.trim()}" (mong ${mongDoi.length}), an=${h.bieu_ngu_an}` +
        (mongDoi.length === 0 ? " — da do nhanh an" : " — CHUA do duoc nhanh an (CSDL dang co canh bao)"));
  }

  // ------------------------------- 7. don rac gia, GIU LAI E-Stop
  {
    const con = await p.evaluate(() => {
      try {
        const t = localStorage.getItem("activeAlarms");
        const ds = t ? JSON.parse(t) : null;
        return { co_khoa: t !== null, so: ds ? ds.length : 0,
                 ds: ds ? ds.map((a) => a.code || "(khong co code)") : null };
      } catch (e) { return { co_khoa: null, so: -1, ds: null }; }
    });
    const daLoai = con.ds && !con.ds.includes("SYS-FLT-11");
    const daGiu = con.ds && con.ds.includes("SU-CO-KHAN");
    // Ba phan tu phai con: giu theo code, giu theo chuoi "E-Stop", va giu vi
    // khong co truong code (khong ro thi khong xoa).
    const duBa = con.so === 3;
    soLieu.don_rac = con;
    ghi("don_rac_gia_giu_lai_e_stop",
        con.co_khoa === true && daLoai && daGiu && duBa,
        `khoa con: ${con.co_khoa}; con ${con.so} phan tu (mong 3); ma: ${JSON.stringify(con.ds)}`);
  }

  // ------------------------------------- 8. tu lam moi that su chay
  {
    // Cua so 42 giay voi nhip 15 giay cho dung 2 luot — bien bang 0: mot tick bi
    // Chrome throttle la FAIL gia, con nhip 20 giay cung cho 2 luot nen PASS gia.
    // Noi len 65 giay (mong 4 luot, doi >= 3 de con bien) VA them khang dinh tinh
    // ve chinh con so nhip.
    const nhipTinh = /NHIP_CANH_BAO_MS\s*=\s*15000/.test(readFileSync(SRC, "utf8"));
    const truoc = demGoiApi;
    await ngu(65000);
    const sau = demGoiApi;
    soLieu.tu_lam_moi = { goi_truoc: truoc, goi_sau: sau, trong_65s: sau - truoc,
                          nhip_15s_trong_ma_nguon: nhipTinh };
    ghi("tu_lam_moi_that_su_chay", sau - truoc >= 3 && nhipTinh,
        `${sau - truoc} luot goi ${API} trong 65 giay (mong >= 3); ` +
        `NHIP_CANH_BAO_MS = 15000 trong ma nguon: ${nhipTinh}`);
  }

  // ----------------------------------------- 9. nut xac nhan ghi duoc
  {
    const idThu = Number(mot(`SELECT canh_bao_id FROM canh_bao WHERE ma_loi='${DAU}-1'`));
    p.on("dialog", async (d) => {
      if (d.type() === "prompt") await d.accept("Nguoi Kiem Thu");
      else await d.accept();
    });
    const bam = await p.evaluate((id) => {
      const tr = document.getElementById("alarm-tr-" + id);
      if (!tr) return false;
      const b = tr.querySelector("button");
      if (!b) return false;
      b.click();
      return true;
    }, idThu);
    if (!bam) throw new Error("khong tim thay nut xac nhan cua dong thu");
    await ngu(3000);
    const r = sql(`SELECT COALESCE(xac_nhan_boi,'-'), IF(xac_nhan_luc IS NULL,'trong','co')
                     FROM canh_bao WHERE canh_bao_id=${idThu}`)[0];
    soLieu.xac_nhan = { boi: r[0], luc: r[1] };
    ghi("nut_xac_nhan_ghi_duoc", r[1] === "co" && r[0] === "Nguoi Kiem Thu",
        `xac_nhan_boi="${r[0]}", xac_nhan_luc=${r[1]}`);
  }
  await p.close();

  // --------------- 9b. bieu ngu AN khi khong con canh bao nao
  //
  // Nhanh nay khong the do bang du lieu that: script tu chen ba canh bao truoc
  // khi mo trinh duyet, va CSDL that dang co 16 dong chua xac nhan. Ep bang mot
  // phan hoi rong — day la phep kiem PHIA TRINH DUYET cho anh xa "0 canh bao ->
  // an bieu ngu", khong phai bang chung ve may chu.
  {
    const p3 = await browser.newPage();
    await p3.setRequestInterception(true);
    p3.on("request", (req) => {
      if (req.url().includes(API))
        req.respond({ status: 200, contentType: "application/json",
                      body: JSON.stringify({ now: "00:00:00", canh_bao: [] }) });
      else req.continue();
    });
    await p3.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(2500);
    const g = await p3.evaluate(DOC_BANG);
    await p3.close();

    soLieu.bieu_ngu_an = { an: g.bieu_ngu_an, than: g.than.slice(0, 60), so_dong: g.so_dong };
    ghi("bieu_ngu_an_khi_khong_con_canh_bao",
        g.bieu_ngu_an === true && g.so_dong === 0 && g.than.includes("vận hành an toàn"),
        `bieu ngu an: ${g.bieu_ngu_an}, so dong: ${g.so_dong}, ` +
        `than bang: "${g.than.trim().slice(0, 50)}"`);
  }

  // ------------------- 10. khong doc duoc KHAC trang thai sach
  {
    const dang = [
      ["503", { status: 503, contentType: "text/plain", body: "loi gia lap" }],
      ["200 + HTML", { status: 200, contentType: "text/html", body: "<html>Server Error</html>" }],
      ["200 + {}", { status: 200, contentType: "application/json", body: "{}" }],
    ];
    const hong = [], chiTiet = [];
    for (const [ten, phanHoi] of dang) {
      const p2 = await browser.newPage();
      await p2.setRequestInterception(true);
      p2.on("request", (req) => {
        if (req.url().includes(API)) req.respond(phanHoi);
        else req.continue();
      });
      await p2.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
      await ngu(2500);
      const g = await p2.evaluate(DOC_BANG);
      await p2.close();

      const coCauLoi = g.than.includes(CAU_LOI);          // khang dinh DUONG
      const noiSach = g.than.includes("vận hành an toàn");
      const bieuNguAn = g.bieu_ngu_an === true;
      chiTiet.push({ dang: ten, co_cau_loi: coCauLoi, noi_sach: noiSach, bieu_ngu_an: bieuNguAn });
      if (!coCauLoi || noiSach || bieuNguAn) hong.push(ten);
    }
    soLieu.mat_ket_noi = chiTiet;
    ghi("khong_doc_duoc_khac_trang_thai_sach", hong.length === 0,
        hong.length ? "sai o dang: " + hong.join(", ")
                    : "ca 3 dang deu hien cau bao loi co dinh, khong dang nao noi an toan");
  }
} catch (e) {
  ghi("phep_thu_dashboard", false, e.message);
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
