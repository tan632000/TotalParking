// Kiem chung task-02 — trang Cards sua, tat va bat lai duoc.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-trang-the.mjs"
//
// Hai phep kiem quan trong nhat:
//   - huy_thi_khong_doi: mot cai dat tat TRUOC roi moi hoi van PASS moi phep
//     khac. Chi phep nay bat duoc.
//   - sua_tu_giao_dien_ghi_duoc: doc CSDL chu khong doc lai bang. Mot cai dat
//     chi sua DOM ma khong goi API van PASS neu chi doc bang.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const CAP_DEPLOY = [
  ["Cards.cshtml",
   join(GOC, "TotalParking", "Views", "Home", "Cards.cshtml"),
   join(DEPLOY, "Views", "Home", "Cards.cshtml")],
  ["TotalParking.dll",
   join(GOC, "TotalParking", "bin", "TotalParking.dll"),
   join(DEPLOY, "bin", "TotalParking.dll")],
];

const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const TRANG = "http://localhost:8080/Home/Cards";

const DAU = "TU" + Date.now().toString(36).slice(-6).toUpperCase();
const NGUOI = "Nguoi Kiem Thu";

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
const luu = () => writeFileSync(join(ARTIFACTS, "trang-the.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

// --------------------------------------------------- 0. deploy khop source
{
  const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
  const lech = [];
  for (const [ten, a, b] of CAP_DEPLOY) {
    try { if (bam(a) !== bam(b)) lech.push(ten); }
    catch (e) { lech.push(`${ten}: ${e.message}`); }
  }
  if (lech.length) {
    ghi("deploy_khop_source", false, "chua deploy: " + lech.join(", "));
    process.exit(1);
  }
  ghi("deploy_khop_source", true,
      createHash("sha256").update(readFileSync(CAP_DEPLOY[0][1])).digest("hex").slice(0, 12));
}

const nguon = readFileSync(CAP_DEPLOY[0][1], "utf8");

// ---------------------------------- 1. khong co o nhap ma the trong modal
//
// Cat lay dung khoi modal sua. Khong cam chuoi card_code trong ca file: no xuat
// hien hop le o cot bang va o khoa du lieu.
{
  // Moc ket thuc KHONG chua ky tu xuong dong: file nay duoc git canh bao se
  // doi LF thanh CRLF o lan checkout sau, va mot moc chua "\n\n" se truot.
  // Cung khong dung cua so du phong theo offset: no lam nhanh bao loi khong bao
  // gio chay duoc, va cat lo sang modal nhap file.
  const i = nguon.indexOf('id="editModal"');
  const j = nguon.indexOf('<div id="importModal"', i);
  if (i < 0 || j < 0) {
    ghi("khong_co_o_nhap_ma_the", false,
        `khong cat duoc khoi modal (i=${i}, j=${j})`);
    process.exit(1);
  }
  const than = nguon.slice(i, j);
  const oNhap = (than.match(/<input[^>]*>/g) || []);
  const coMaThe = oNhap.some((t) => /card_?code|ma[-_]?the/i.test(t));
  soLieu.modal = { cat_duoc: than.length, so_o_nhap: oNhap.length, co_o_ma_the: coMaThe };
  ghi("khong_co_o_nhap_ma_the", !coMaThe,
      `khoi modal ${than.length} ky tu, ${oNhap.length} o nhap, khong o nao cho ma the`);
}

// ------------------------------- 2. nhan noi dung "tat", khong phai "xoa"
{
  // Cat dung vung o thao tac bang HAI MOC CO THAT trong render(), roi chay CA
  // hai khang dinh trong vung do. Quet ca file thi hai chu nam o chu thich cung
  // PASS; quet mot cua so tinh bang offset thi truot ngay khi them vai dong.
  const moc1 = '<td class="px-4 py-2.5 text-center whitespace-nowrap">';
  const a = nguon.indexOf(moc1);
  const b = a >= 0 ? nguon.indexOf("'</td></tr>'", a) : -1;
  if (a < 0 || b < 0) {
    ghi("nhan_noi_dung_tat_khong_phai_xoa", false,
        `khong cat duoc vung o thao tac (a=${a}, b=${b})`);
  } else {
    const vung = nguon.slice(a, b);
    const coNgung = /Ngừng dùng/.test(vung) && /Dùng lại/.test(vung);
    const coXoa = /Xo[áa]/.test(vung);
    soLieu.nhan = { vung_ky_tu: vung.length, co_ngung_dung: coNgung, co_chu_xoa: coXoa };
    ghi("nhan_noi_dung_tat_khong_phai_xoa", coNgung && !coXoa,
        `vung o thao tac ${vung.length} ky tu: co "Ngừng dùng"/"Dùng lại"=${coNgung}, ` +
        `co chu "Xoá"=${coXoa}`);
  }
}

const soTheTruoc = Number(mot("SELECT COUNT(*) FROM parking_card"));
const soODoTruoc = Number(mot("SELECT COUNT(*) FROM plc_slot_state"));
soLieu.truoc = { so_the: soTheTruoc, so_o_do: soODoTruoc };
luu();

let browser, idA = 0, maA = null, oMuon = null;

try {
  sql(`INSERT INTO parking_card
        (card_code, card_no, card_type, vehicle_name, customer_type_id, weight_class_id,
         weight_text, plate, customer_name, source_label, is_active, created_at)
       VALUES ('CC${Date.now().toString(16).slice(-6).toUpperCase()}', '${DAU}-A', 'OTO',
               'Xe thu giao dien', 1, 0, '1500KG', '30A-11111', 'Khach Giao Dien',
               '${DAU}', 1, NOW())`);
  idA = Number(mot(`SELECT card_id FROM parking_card WHERE card_no='${DAU}-A'`));
  maA = mot(`SELECT card_code FROM parking_card WHERE card_id=${idA}`);
  soLieu.the_thu = { idA, maA };

  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });
  const p = await browser.newPage();
  await p.setViewport({ width: 1700, height: 950 });

  // Loc bang ve dung the thu de nut nam trong tam voi.
  // Bam NUT THAT trong bang, khong goi window.* — neu goi thang qua window thi
  // day noi onclick cua cot moi chua tung chay: card_id undefined, cot lech, hay
  // </td> dong sai deu se PASS het.
  const bamNut = async (chu) => {
    const duoc = await p.evaluate((t) => {
      const b = [...document.querySelectorAll("#cardRows button")]
                  .find((x) => x.textContent.trim() === t);
      if (!b) return false;
      b.click();
      return true;
    }, chu);
    if (!duoc) throw new Error(`khong tim thay nut "${chu}" trong bang`);
  };

  const loc = async () => {
    await p.evaluate((q) => {
      const o = document.getElementById("cardSearch");
      o.value = q;
      o.dispatchEvent(new Event("input"));
    }, `${DAU}-A`);
    await ngu(400);
  };

  await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
  await p.waitForFunction(`document.querySelectorAll('#cardRows tr').length > 0`, { timeout: 20000 });
  await loc();

  // -------------------------------------- 3. o chon lay tu may chu
  {
    await bamNut("Sửa");
    await ngu(400);
    const dem = await p.evaluate(() => ({
      loai: document.querySelectorAll("#edLoaiKhach option").length,
      hang: document.querySelectorAll("#edHangTai option").length,
      ma: (document.getElementById("edMaThe") || {}).textContent || "",
    }));
    const loaiThat = Number(mot("SELECT COUNT(*) FROM customer_type"));
    const hangThat = Number(mot("SELECT COUNT(*) FROM weight_class"));
    soLieu.o_chon = { ...dem, loaiThat, hangThat };
    ghi("o_chon_lay_tu_may_chu",
        dem.loai === loaiThat && dem.hang === hangThat && dem.ma === maA,
        `loai_khach ${dem.loai}/${loaiThat}, hang_tai ${dem.hang}/${hangThat}, ` +
        `ma the trong modal="${dem.ma}"`);
  }

  // ------------------------------- 4. sua tu giao dien ghi duoc CSDL
  {
    await p.evaluate(() => {
      document.getElementById("edBienSo").value = "99Z-12345";
      document.getElementById("edTenKhach").value = "Khach Da Sua";
      document.getElementById("edNguoi").value = "Nguoi Kiem Thu";
    });
    await p.click("#edLuu");
    await ngu(2500);

    // Doc CSDL (chong cai dat chi sua DOM) VA doc lai bang (AC-11 doi ca hai).
    // Doc them hai khoa ngoai: do la hai cot ma modal thuc su TINH ra tu ma chu,
    // va khong doc lai thi mot phep doi chieu sai se im lang.
    const r = sql(`SELECT COALESCE(plate,''), COALESCE(customer_name,''),
                          customer_type_id, weight_class_id
                     FROM parking_card WHERE card_id=${idA}`)[0];
    await loc();
    const tren_bang = await p.evaluate(() =>
      ((document.querySelector("#cardRows tr") || {}).innerText || ""));
    await p.screenshot({ path: join(ARTIFACTS, "trang-the.png") }).catch(() => {});

    const idDung = r[2] === "1" && r[3] === "0";   // VANG / THUONG, giu nguyen
    soLieu.sua = { doc_csdl: r, tren_bang: tren_bang.slice(0, 120) };
    ghi("sua_tu_giao_dien_ghi_duoc",
        r[0] === "99Z-12345" && r[1] === "Khach Da Sua" && idDung
          && tren_bang.includes("99Z-12345"),
        `CSDL: bien so="${r[0]}", khach="${r[1]}", loai_khach_id=${r[2]}, hang_tai_id=${r[3]}; ` +
        `bang co hien bien so moi: ${tren_bang.includes("99Z-12345")}`);
  }

  // ------------------- 5+6. xac nhan co hien ma the, va HUY thi khong doi
  {
    let noiDungHoi = "";
    const xuLy = async (d) => {
      if (d.type() === "confirm") { noiDungHoi = d.message(); await d.dismiss(); }
      else await d.dismiss();
    };
    p.on("dialog", xuLy);

    await loc();
    await bamNut("Ngừng dùng");
    await ngu(1500);
    const sauHuy = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    p.off("dialog", xuLy);

    soLieu.xac_nhan = { noi_dung: noiDungHoi, is_active_sau_huy: sauHuy };
    ghi("xac_nhan_co_hien_ma_the", noiDungHoi.includes(maA),
        noiDungHoi ? `hop thoai: "${noiDungHoi.split("\n")[0]}"` : "KHONG co hop thoai nao");
    ghi("huy_thi_khong_doi", sauHuy === "1",
        `sau khi huy hop thoai, is_active=${sauHuy} (1 = chua doi, dung)`);
  }

  // --------------------------------------------- 7. tu choi hien ly do
  //
  // Muon mot o dang trong cua khoi con nhieu cho nhat, theo dung rang buoc task
  // 01: UPDATE chu khong chen, va tra ve NULL chi dung gia tri minh da ghi.
  {
    const chon = sql(`
      SELECT s.block_id, s.slot_index, b.block_no,
             b.slot_count - (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id=b.block_id) AS con_trong
        FROM plc_slot_state s JOIN block b ON b.block_id = s.block_id
       WHERE s.card_code IS NULL AND b.is_active = 1
       ORDER BY con_trong DESC, s.slot_index LIMIT 1`);
    if (chon.length === 0) throw new Error("khong tim duoc o dang trong de muon");
    oMuon = { block_id: chon[0][0], slot_index: chon[0][1] };

    sql(`UPDATE plc_slot_state SET card_code='${maA}'
          WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
            AND card_code IS NULL`);
    const muonDuoc = mot(`SELECT COUNT(*) FROM plc_slot_state
                           WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
                             AND card_code='${maA}'`) === "1";
    if (!muonDuoc) throw new Error("khong muon duoc o do: mot xe vua duoc xep vao");

    const xuLy2 = async (d) => {
      if (d.type() === "confirm") await d.accept();
      else if (d.type() === "prompt") await d.accept(NGUOI);
      else await d.accept();
    };
    p.on("dialog", xuLy2);
    await loc();
    await bamNut("Ngừng dùng");
    await ngu(2500);
    p.off("dialog", xuLy2);

    const bao = await p.evaluate(() =>
      (document.getElementById("cardMsg") || {}).textContent || "");
    const con = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);

    sql(`UPDATE plc_slot_state SET card_code=NULL
          WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
            AND card_code='${maA}'`);
    oMuon = null;

    soLieu.tu_choi = { thong_bao: bao, is_active: con };
    ghi("tu_choi_hien_ly_do", bao.trim().length > 0 && con === "1",
        bao.trim() ? `bao: "${bao.trim().slice(0, 80)}"; is_active=${con}`
                   : `KHONG hien gi; is_active=${con}`);
  }
} catch (e) {
  ghi("phep_thu_trang_the", false, e.message);
} finally {
  if (browser) await browser.close();
  try {
    if (oMuon) sql(`UPDATE plc_slot_state SET card_code=NULL
                     WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
                       AND card_code LIKE 'CC%'`);
    sql(`DELETE FROM parking_card WHERE source_label LIKE '${DAU}%'`);
    const sauThe = Number(mot("SELECT COUNT(*) FROM parking_card"));
    const sauODo = Number(mot("SELECT COUNT(*) FROM plc_slot_state"));
    soLieu.sau = { so_the: sauThe, so_o_do: sauODo };
    ghi("don_sach_du_lieu_thu", sauThe === soTheTruoc && sauODo === soODoTruoc,
        `the ${soTheTruoc}->${sauThe}, o do ${soODoTruoc}->${sauODo}`);
  } catch (e) {
    ghi("don_sach_du_lieu_thu", false, "KHONG DON DUOC: " + e.message +
        ` — chay tay: DELETE FROM parking_card WHERE source_label LIKE '${DAU}%';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
