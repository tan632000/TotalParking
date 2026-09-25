// Kiem chung task-01 — bang canh bao va API doc/xac nhan.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-alarm-api.mjs"
//
// Moi dong script tao deu mang ma_loi bat dau bang TEST- kem dau thoi gian, va
// duoc xoa o cuoi. Mot phep kiem khong duoc lam ban du lieu van hanh.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\bin\\TotalParking.dll";
const ALARMS_CSHTML = join(GOC, "TotalParking", "Views", "Home", "Alarms.cshtml");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const API = "http://localhost:8080/Alarm";

const DAU = "TEST-" + Date.now().toString(36).toUpperCase();

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString(), dau_nhan_dien: DAU };
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
const mot = (cau) => sql(cau)[0][0];

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "alarm-api.json"), JSON.stringify(soLieu, null, 1), "utf8");

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
if (bam(DLL_SOURCE) !== bam(DLL_DEPLOY)) {
  ghi("deploy_khop_source", false, "source != deploy");
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, bam(DLL_SOURCE).slice(0, 12));

const soDongTruoc = Number(mot("SELECT COUNT(*) FROM canh_bao"));
soLieu.so_dong_truoc = soDongTruoc;
luu();

try {
  // ------------------------------ 1. khoa chong trung co rang buoc UNIQUE
  //
  // Rang buoc nay chi duoc DUNG o task 03, nen khong kiem o day thi loi chi lo
  // ra khi bang da ngap canh bao trung.
  {
    const r = sql(`SELECT NON_UNIQUE FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA='total_parking' AND TABLE_NAME='canh_bao'
                      AND COLUMN_NAME='khoa_chong_trung'`);
    ghi("khoa_chong_trung_la_duy_nhat", r.length === 1 && r[0][0] === "0",
        r.length ? `NON_UNIQUE=${r[0][0]} (0 la duy nhat)` : "khong co index nao");
  }

  // ------------------------------------ 2. tu vung khop bo loc san co
  //
  // Doc thang Alarms.cshtml de lay tap gia tri ma bo loc dang dung, roi doi
  // chieu voi ENUM cua bang. Day la cho giu hop dong giua task 01 va task 02.
  {
    const html = readFileSync(ALARMS_CSHTML, "utf8");
    const lay = (thuoc) => [...new Set(
      [...html.matchAll(new RegExp(`${thuoc}="([a-z]+)"`, "g"))].map((m) => m[1]))].sort();
    const typeHtml = lay("data-type");
    const sevHtml = lay("data-severity");

    const enumCua = (cot) => {
      const t = mot(`SELECT COLUMN_TYPE FROM information_schema.COLUMNS
                      WHERE TABLE_SCHEMA='total_parking' AND TABLE_NAME='canh_bao'
                        AND COLUMN_NAME='${cot}'`);
      return [...t.matchAll(/'([a-z]+)'/g)].map((m) => m[1]).sort();
    };
    const nguon = enumCua("nguon");
    const mucDo = enumCua("muc_do");

    const phu = (a, b) => a.every((x) => b.includes(x));
    soLieu.tu_vung = { typeHtml, nguon, sevHtml, mucDo };
    ghi("tu_vung_khop_bo_loc", phu(typeHtml, nguon) && phu(sevHtml, mucDo),
        `data-type [${typeHtml}] vs nguon [${nguon}]; data-severity [${sevHtml}] vs muc_do [${mucDo}]`);
  }

  // --------------------------------------------- 3. ghi roi doc lai duoc
  let idThu = 0;
  {
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, block_no, zone_id, thiet_bi, mo_ta)
         VALUES ('hardware','critical','${DAU}-A', 21, 2, 'PLC 192.169.1.121',
                 'Canh bao thu cua phep kiem, se duoc xoa')`);
    idThu = Number(mot(`SELECT canh_bao_id FROM canh_bao WHERE ma_loi='${DAU}-A'`));

    const d = await (await fetch(API + "/List", { cache: "no-store" })).json();
    const tim = (d.canh_bao || []).find((c) => c.ma_loi === `${DAU}-A`);
    const duTruong = tim && tim.muc_do === "critical" && tim.nguon === "hardware" &&
                     tim.block_no === 21 && tim.chua_xac_nhan === true && !!tim.xay_ra_luc;
    ghi("ghi_roi_doc_lai_duoc", !!duTruong,
        tim ? `tim thay id=${tim.id}, muc_do=${tim.muc_do}, chua_xac_nhan=${tim.chua_xac_nhan}`
            : "API khong tra ve canh bao vua ghi");
  }

  // ------------------------------------ 4. xac nhan luu ten va IP may khach
  {
    const r = await fetch(API + "/Ack", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: `id=${idThu}&nguoi=${encodeURIComponent("Nguoi Kiem Thu")}`,
    });
    const row = sql(`SELECT COALESCE(xac_nhan_boi,'-'), COALESCE(xac_nhan_ip,'-'),
                            IF(xac_nhan_luc IS NULL,'trong','co')
                       FROM canh_bao WHERE canh_bao_id=${idThu}`)[0];
    soLieu.xac_nhan = { http: r.status, boi: row[0], ip: row[1], luc: row[2] };
    ghi("xac_nhan_luu_ten_va_ip",
        r.status === 200 && row[0] === "Nguoi Kiem Thu" && row[1] !== "-" && row[2] === "co",
        `HTTP ${r.status}, boi="${row[0]}", ip="${row[1]}", luc=${row[2]}`);
  }

  // --------------------------- 5. xac nhan lan hai khong de len lan dau
  {
    const r = await fetch(API + "/Ack", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: `id=${idThu}&nguoi=${encodeURIComponent("Nguoi Thu Hai")}`,
    });
    const boi = mot(`SELECT xac_nhan_boi FROM canh_bao WHERE canh_bao_id=${idThu}`);
    ghi("ack_lan_hai_khong_de_lan_dau", r.status === 409 && boi === "Nguoi Kiem Thu",
        `HTTP ${r.status} (mong doi 409), nguoi xac nhan van la "${boi}"`);
  }

  // ------------------------------------------- 6. danh sach co gioi han
  //
  // Chen 210 dong DA xac nhan; API chi duoc tra toi da 200 dong da xac nhan.
  {
    const nhieu = Array.from({ length: 210 }, (_, i) =>
      `('operation','low','${DAU}-B${i}','Canh bao thu hang loat', NOW(3), NOW(3), 'kiem thu', '0.0.0.0')`
    ).join(",");
    sql(`INSERT INTO canh_bao (nguon, muc_do, ma_loi, mo_ta, xay_ra_luc, xac_nhan_luc, xac_nhan_boi, xac_nhan_ip)
         VALUES ${nhieu}`);

    const d = await (await fetch(API + "/List", { cache: "no-store" })).json();
    const daXacNhan = (d.canh_bao || []).filter((c) => !c.chua_xac_nhan).length;
    soLieu.gioi_han = { tra_ve: d.tong, da_xac_nhan: daXacNhan, gioi_han: d.gioi_han_da_xac_nhan };
    ghi("danh_sach_co_gioi_han", daXacNhan <= 200,
        `API tra ${d.tong} dong, trong do ${daXacNhan} da xac nhan (gioi han ${d.gioi_han_da_xac_nhan})`);
  }
} catch (e) {
  ghi("phep_thu_alarm_api", false, e.message);
} finally {
  // ------------------------------------------- 7. don sach du lieu thu
  try {
    sql(`DELETE FROM canh_bao WHERE ma_loi LIKE '${DAU}-%'`);
    const sau = Number(mot("SELECT COUNT(*) FROM canh_bao"));
    soLieu.so_dong_sau = sau;
    ghi("don_sach_du_lieu_thu", sau === soDongTruoc,
        `so dong ${soDongTruoc} -> ${sau}`);
  } catch (e) {
    ghi("don_sach_du_lieu_thu", false, "KHONG DON DUOC: " + e.message +
        ` — chay tay: DELETE FROM canh_bao WHERE ma_loi LIKE '${DAU}-%';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
