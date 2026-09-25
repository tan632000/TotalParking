// Kiem chung task-01 — header SCADA noi dung su that.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\header-noi-that\verify-header.mjs"
//
// Hai phep kiem quan trong nhat:
//   - mat_ket_noi_khac_trang_thai_sach: ep BA dang hong, khong phai mot. Dang
//     "200 + {}" la dang duy nhat dan toi chip xanh gia, va no di qua duong
//     hoan toan khac voi 503.
//   - kich_thuoc_khong_tang_theo_so_canh_bao: bat loi "tien tay tra kem danh
//     sach" ma chin phep kiem con lai deu bo lot.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";

// Duong dan TUYET DOI CO DINH, khong glob: ton tai ban sao cu
// TotalParking/obj/Release/Package/PackageTmp/Views/Shared/_ScadaLayout.cshtml
// chua "4 Alarm". So nham vao do thi hai ban cung cu khop nhau hoan hao va
// probe PASS gia, khien moi so do sau do do tren ma cu.
const CAP_DEPLOY = [
  ["_ScadaLayout.cshtml",
   join(GOC, "TotalParking", "Views", "Shared", "_ScadaLayout.cshtml"),
   join(DEPLOY, "Views", "Shared", "_ScadaLayout.cshtml")],
  ["TotalParking.dll",
   join(GOC, "TotalParking", "bin", "TotalParking.dll"),
   join(DEPLOY, "bin", "TotalParking.dll")],
];

const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const GOC_WEB = "http://localhost:8080";
const SUMMARY = "/Alarm/Summary";
const TRANG_KHONG_POLL = GOC_WEB + "/Home/Settings";
const TRANG_ALARMS = GOC_WEB + "/Home/Alarms";
const TRANG_KHAC = [GOC_WEB + "/Home/Settings", GOC_WEB + "/Home/Backup"];

const DAU = "HDR-" + Date.now().toString(36).toUpperCase();
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
const mot = (c) => { const r = sql(c); return r.length ? r[0][0] : null; };
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "header.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

// Doc trang thai header tu DOM. Doc CA chu LAN lop CSS: chi doc chu thi mot
// cai dat quen doi mau van PASS.
const DOC_HEADER = `(() => {
  const g = (id) => document.getElementById(id);
  const tt = g('hdr-tinh-trang'), so = g('hdr-so-alarm');
  return {
    tinh_trang_chu: (g('hdr-tinh-trang-chu') || {}).textContent || "",
    tinh_trang_lop: tt ? tt.className : null,
    so_chu:         (g('hdr-so-alarm-chu') || {}).textContent || "",
    so_lop:         so ? so.className : null,
    gio:            (g('hdr-gio')  || {}).textContent || "",
    ngay:           (g('hdr-ngay') || {}).textContent || "",
    // Mau DA TINH, khong phai chuoi class: mot lop khong ton tai trong
    // scada.css van nam trong className nhung khong to duoc gi.
    tinh_trang_nen: tt ? getComputedStyle(tt).backgroundColor : null,
    so_nen:         so ? getComputedStyle(so).backgroundColor : null
  };
})()`;

const doDo   = (lop) => /(^|\s|-)red-/.test(lop || "");
const doXanh = (lop) => /green-/.test(lop || "");
const doXam  = (lop) => /slate-/.test(lop || "");

// --------------------------------------------------- 0. deploy khop source
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
{
  const lech = [];
  const dau = [];
  for (const [ten, a, b] of CAP_DEPLOY) {
    try {
      const ha = bam(a), hb = bam(b);
      dau.push({ ten, source: a, deploy: b, bam: ha.slice(0, 12) });
      if (ha !== hb) lech.push(`${ten}: ${a} (${ha.slice(0, 12)}) != ${b} (${hb.slice(0, 12)})`);
    } catch (e) { lech.push(`${ten}: ${e.message}`); }
  }
  soLieu.deploy = dau;
  if (lech.length) {
    ghi("deploy_khop_source", false, lech.join(" ; "));
    console.log("\nCHUA DEPLOY — chep bin\\ va _ScadaLayout.cshtml sang ban deploy roi chay lai.");
    luu();
    process.exit(1);
  }
  ghi("deploy_khop_source", true, dau.map((d) => `${d.ten}=${d.bam}`).join(", "));
}

// ------------------------------- 1. khong con chuoi cung TRONG KHOI <header>
//
// Cat lay dung khoi <header>. Doc ca file se FAIL oan vi "08/06/2026" con mot
// ban nua o nhan phien ban thanh ben — ngoai pham vi, khong duoc tinh.
{
  const src = readFileSync(CAP_DEPLOY[0][1], "utf8");
  const i = src.indexOf("<header");
  const j = src.indexOf("</header>", i);
  const than = i >= 0 && j > i ? src.slice(i, j) : "";
  const CAM = ["4 Alarm", "supervisor01", "16:02:42", "08/06/2026"];
  const conSot = than ? CAM.filter((c) => than.includes(c)) : CAM;
  soLieu.chuoi_cung = { cat_duoc: than.length, con_sot: conSot };
  ghi("khong_con_chuoi_cung", than.length > 0 && conSot.length === 0,
      than.length === 0 ? "KHONG cat duoc khoi <header>"
        : (conSot.length ? "con: " + conSot.join(", ")
                         : `khoi <header> ${than.length} ky tu, sach ca 4 chuoi`));
}

const soDongTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));
soLieu.so_dong_truoc = soDongTruoc;
luu();

let browser;
try {
  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ---------------------------- 2. so khop tren HAI trang khac nhau
  //
  // Chen canh bao thu de so doi that su, roi doc header tren mot trang KHONG
  // co poll canh bao rieng (Settings) va mot trang CO (Alarms). Do mot trang
  // chi chung minh mot trang, chua chung minh layout dung chung.
  {
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, mo_ta) VALUES
         ('hardware','high','${DAU}-1','Canh bao thu header 1'),
         ('hardware','low','${DAU}-2','Canh bao thu header 2'),
         ('operation','low','${DAU}-3','Canh bao thu header 3')`);

    const mongDoi = Number(mot("SELECT COUNT(*) FROM canh_bao WHERE xac_nhan_luc IS NULL"));
    const doc = [];
    for (const url of [TRANG_KHONG_POLL, TRANG_ALARMS]) {
      const p = await browser.newPage();
      await p.setViewport({ width: 1600, height: 900 });
      await p.goto(url, { waitUntil: "networkidle2", timeout: 40000 });
      await p.waitForFunction(
        `(document.getElementById('hdr-so-alarm-chu') || {}).textContent.indexOf('Alarm') >= 0`,
        { timeout: 20000 }).catch(() => {});
      const h = await p.evaluate(DOC_HEADER);
      doc.push({ url, ...h });
      if (url === TRANG_KHONG_POLL)
        await p.screenshot({ path: join(ARTIFACTS, "header.png") }).catch(() => {});
      await p.close();
    }
    const nhan = `${mongDoi} Alarm`;
    const khop = doc.every((d) => d.so_chu.trim() === nhan);
    soLieu.hai_trang = { mong_doi: nhan, doc };
    ghi("so_khop_tren_hai_trang", khop,
        `csdl ${nhan}; ` + doc.map((d) => `${d.url.split("/").pop()}="${d.so_chu.trim()}"`).join(", "));
  }

  // ----------------------------- 3. chip do khi co critical (DU LIEU THAT)
  {
    // Chen mot dong critical MANG TIEN TO RIENG thay vi trong cho CSDL san
    // xuat tinh co dang co critical. Khong co dong nay thi probe PASS nho may:
    // chay luc he thong sach se FAIL oan, va khong ai biet vi sao.
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, mo_ta)
         VALUES ('hardware','critical','${DAU}-C','Canh bao thu muc nghiem trong')`);

    // Doc thang may chu, KHONG viet lai cau SQL cua ban cai dat: mot oracle
    // sao chep chinh gia dinh cua doi tuong do thi hai ben cung sai van khop.
    const tomTat = await (await fetch(GOC_WEB + SUMMARY, { cache: "no-store" })).json();

    const p = await browser.newPage();
    await p.goto(TRANG_KHONG_POLL, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(2500);
    const h = await p.evaluate(DOC_HEADER);
    await p.close();

    const dat = tomTat.muc_cao_nhat === "critical" && doDo(h.tinh_trang_lop) && doDo(h.so_lop);
    soLieu.chip_that = { may_chu_bao: tomTat.muc_cao_nhat, ...h };
    ghi("chip_do_khi_co_critical", dat,
        `may chu bao muc_cao_nhat="${tomTat.muc_cao_nhat}"; chip="${h.tinh_trang_chu}", ` +
        `lop do: tinh_trang=${doDo(h.tinh_trang_lop)} so=${doDo(h.so_lop)}`);
  }

  // ------------------- 4. chip xanh khi sach, va KHONG do khi chi con muc thap
  //
  // Hai trang thai nay khong ep duoc bang du lieu that ma khong xoa canh bao
  // THAT cua he thong dang chay. Nen ep bang phan hoi dung khuon tra ve tu
  // may chu. Day la phep kiem PHIA TRINH DUYET: no chung minh anh xa tu so
  // lieu sang mau chip, khong chung minh phan may chu (AC-01 lo phan do).
  async function epPhanHoi(than, kieu) {
    const p = await browser.newPage();
    await p.setRequestInterception(true);
    p.on("request", (req) => {
      if (req.url().includes(SUMMARY))
        req.respond({ status: 200, contentType: kieu || "application/json", body: than });
      else req.continue();
    });
    await p.goto(TRANG_KHONG_POLL, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(2500);
    const h = await p.evaluate(DOC_HEADER);
    await p.close();
    return h;
  }

  {
    const sach = await epPhanHoi('{"now":"00:00:00","chua_xac_nhan":0,"dang_mo":0,"muc_cao_nhat":null}');
    const nhe  = await epPhanHoi('{"now":"00:00:00","chua_xac_nhan":3,"dang_mo":2,"muc_cao_nhat":"low"}');

    const sachDung = doXanh(sach.tinh_trang_lop) && /Bình thường/.test(sach.tinh_trang_chu);
    const nheDung  = !doDo(nhe.tinh_trang_lop) && !doXanh(nhe.tinh_trang_lop);

    soLieu.chip_ep = { sach, nhe };
    ghi("chip_xanh_khi_sach", sachDung && nheDung,
        `sach: chu="${sach.tinh_trang_chu}" xanh=${doXanh(sach.tinh_trang_lop)}; ` +
        `chi-con-low: chu="${nhe.tinh_trang_chu}" do=${doDo(nhe.tinh_trang_lop)} (phai la false)`);
  }

  // --------------- 5. mat ket noi KHAC trang thai sach — ep BA dang hong
  {
    const dang = [
      ["503", { status: 503, contentType: "text/plain", body: "loi gia lap" }],
      ["200 + {}", { status: 200, contentType: "application/json", body: "{}" }],
      ["200 + HTML", { status: 200, contentType: "text/html", body: "<html>Server Error</html>" }],
    ];
    const hong = [];
    const chiTiet = [];

    for (const [ten, phanHoi] of dang) {
      const p = await browser.newPage();
      await p.setRequestInterception(true);
      p.on("request", (req) => {
        if (req.url().includes(SUMMARY)) req.respond(phanHoi);
        else req.continue();
      });
      await p.goto(TRANG_KHONG_POLL, { waitUntil: "networkidle2", timeout: 40000 });
      await ngu(2500);
      const h = await p.evaluate(DOC_HEADER);
      await p.close();

      const noiSach = /Bình thường/.test(h.tinh_trang_chu) || doXanh(h.tinh_trang_lop);
      const giuSoCu = /\d/.test(h.so_chu);
      const bao = doXam(h.tinh_trang_lop) && /Mất kết nối/.test(h.tinh_trang_chu);
      chiTiet.push({ dang: ten, chu: h.tinh_trang_chu, so: h.so_chu, noiSach, giuSoCu });
      if (noiSach || giuSoCu || !bao) hong.push(ten);
    }

    soLieu.mat_ket_noi = chiTiet;
    ghi("mat_ket_noi_khac_trang_thai_sach", hong.length === 0,
        hong.length
          ? "sai o dang: " + hong.join(", ")
          : `ca 3 dang deu bao mat ket noi, khong dang nao noi "Binh thuong" hay giu so cu`);
  }

  // ------------------------------------------------------- 6. dong ho chay
  {
    const p = await browser.newPage();
    await p.goto(TRANG_KHONG_POLL, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(1200);
    const a = await p.evaluate(DOC_HEADER);
    await ngu(1600);
    const b = await p.evaluate(DOC_HEADER);
    await p.close();
    soLieu.dong_ho = { lan1: a.gio, lan2: b.gio, ngay: b.ngay };
    ghi("dong_ho_chay", a.gio !== b.gio && /\d/.test(a.gio),
        `"${a.gio}" -> "${b.gio}", ngay="${b.ngay}"`);
  }

  // ------------- 7. kich thuoc KHONG tang theo so canh bao
  //
  // Do BYTE THAN, khong doc Content-Length: neu may chu tra chunked thi header
  // do vang mat, `0 - 0 <= 50` PASS im lang, va bien minh cot loi cho viec tao
  // endpoint rieng mat bang chung.
  {
    const doByte = async () => {
      const r = await fetch(GOC_WEB + SUMMARY, { cache: "no-store" });
      return Buffer.byteLength(await r.text(), "utf8");
    };
    const truoc = await doByte();
    const demTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));

    const hang = [];
    for (let i = 0; i < 40; i++)
      hang.push(`('hardware','medium','${DAU}-B${i}','Canh bao thu do kich thuoc ${i}')`);
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, mo_ta) VALUES ${hang.join(",")}`);

    const sau = await doByte();
    const demSau = Number(mot("SELECT COUNT(*) FROM canh_bao"));
    sql(`DELETE FROM canh_bao WHERE ma_loi LIKE '${DAU}-B%'`);

    // Phep do rong phai FAIL ro rang chu khong PASS im lang qua 0 - 0 <= 50.
    const doDuoc = truoc > 0 && sau > 0;
    const chenh = Math.abs(sau - truoc);
    soLieu.kich_thuoc = { byte_truoc: truoc, byte_sau: sau, chenh, dem_truoc: demTruoc, dem_sau: demSau };
    ghi("kich_thuoc_khong_tang_theo_so_canh_bao", doDuoc && chenh <= 50,
        doDuoc
          ? `${demTruoc} -> ${demSau} canh bao: ${truoc} -> ${sau} byte (chenh ${chenh}, tran 50)`
          : `phep do rong (${truoc}/${sau} byte) — khong ket luan duoc`);
  }

  // ------------------ 8. khong loi JS tren trang khac (loc theo nguon)
  //
  // lucide nap tu CDN unpkg khong ghim va duoc goi tran o _ScadaLayout:305.
  // O mang OT co lap no nem ReferenceError tren MOI trang — do la no co san,
  // khong phai loi cua task nay. Khong loc thi phep kiem FAIL oan va day nguoi
  // thuc thi vao viec sua ngoai pham vi.
  {
    const LUCIDE = /lucide is not defined/i;
    const boQua = [], thatSu = [];
    let coLucide = null;

    for (const url of TRANG_KHAC) {
      const p = await browser.newPage();
      p.on("pageerror", (e) => {
        const m = String(e.message || e);
        (LUCIDE.test(m) ? boQua : thatSu).push(`${url.split("/").pop()}: ${m.slice(0, 90)}`);
      });
      await p.goto(url, { waitUntil: "networkidle2", timeout: 40000 });
      await ngu(3000);
      if (coLucide === null) coLucide = await p.evaluate(() => typeof lucide);
      await p.close();
    }

    soLieu.loi_js = { typeof_lucide: coLucide, bo_qua: boQua, that_su: thatSu };
    ghi("khong_loi_js_tren_trang_khac", thatSu.length === 0,
        thatSu.length ? thatSu.join(" ; ")
          : `2 trang sach; typeof lucide="${coLucide}", da bo qua ${boQua.length} loi lucide co san`);
  }

  // ----------------------------- 9. poll KHONG chong nhau sau khi an/hien
  //
  // Thieu guard `if (!timer)` thi moi lan hien lai sinh them mot interval va
  // mat handle cu. Dem trong CUA SO QUAN SAT sau khi da an/hien xong, de khong
  // dem nham cac luot goi tuc thi luc hien lai.
  {
    const p = await browser.newPage();
    // Dinh nghia lai document.hidden truoc khi trang chay: CDP
    // Emulation.setPageVisibilityOverride da bi go o Chrome moi.
    await p.evaluateOnNewDocument(() => {
      let an = false;
      Object.defineProperty(document, "hidden", { get: () => an, configurable: true });
      Object.defineProperty(document, "visibilityState", {
        get: () => (an ? "hidden" : "visible"), configurable: true,
      });
      window.__datAn = (v) => {
        an = v;
        document.dispatchEvent(new Event("visibilitychange"));
      };
    });
    await p.goto(TRANG_KHONG_POLL, { waitUntil: "networkidle2", timeout: 40000 });

    for (let i = 0; i < 5; i++) {
      await p.evaluate(() => window.__datAn(true));
      await ngu(300);
      await p.evaluate(() => window.__datAn(false));
      await ngu(300);
    }

    // BA lan bao HIEN lien tiep ma KHONG co lan an xen giua. Day moi dung la
    // kich ban gay chong interval: bfcache, doi man hinh, khoa/mo may deu ban
    // visibilitychange bao hien ma khong co lan an tuong ung.
    //
    // Chi ban cap an/hien thi dungLai() da xoa timer truoc moi lan hien, nen
    // ngay ca ban thieu guard cung chi con MOT interval va probe PASS gia.
    await p.evaluate(() => {
      window.__datAn(false);
      window.__datAn(false);
      window.__datAn(false);
    });
    await ngu(500);

    let dem = 0;
    p.on("request", (req) => { if (req.url().includes(SUMMARY)) dem++; });
    const CUA_SO_MS = 45000;
    await ngu(CUA_SO_MS);
    await p.close();

    const tran = Math.ceil(CUA_SO_MS / NHIP_MS) + 1;   // 3 nhip + 1 bien
    soLieu.poll = { so_lan_an_hien: 5, cua_so_ms: CUA_SO_MS, so_request: dem, tran };
    ghi("poll_khong_chong_nhau", dem <= tran,
        `sau 5 lan an/hien, ${CUA_SO_MS / 1000}s co ${dem} luot goi ${SUMMARY} (tran ${tran})`);
  }
} catch (e) {
  ghi("phep_thu_header", false, e.message);
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
