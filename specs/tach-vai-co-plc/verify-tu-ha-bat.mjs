// Kiem chung task-03 — tu ha va tu bat cong van hanh theo ket noi.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tu-ha-bat.mjs"
//
// CANH BAO: script nay BAT tinh nang tu doi suc chua va rut nguong xuong 1 phut,
// tren he thong dang chay. No khoi phuc nguyen trang o cuoi, ke ca khi loi.
//
// Chup TOAN BO tap block dang tat TRUOC khi bat dau, va khoi phuc ve dung tap do
// o cuoi. Khong chi khoi phuc may khoi minh dung: bat tinh nang se lam dich vu ha
// MOI khoi co PLC chet qua nguong — do la hanh vi dung, nhung mot phep kiem
// khong duoc phep de lai thay doi nao.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync, copyFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const DLL_DEPLOY = join(DEPLOY, "bin", "TotalParking.dll");
const CONFIG = join(DEPLOY, "Web.config");
const LUU_CONFIG = CONFIG + ".truoc-tu-ha-bat";
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const API = "http://localhost:8080/PlcStatus/Index";
const RELOAD = "http://localhost:8080/PlcStatus/Reload";

// Nguong rut con 1 phut (nho nhat ma INTERVAL MINUTE nhan duoc). Cong nhip dich
// vu 30s va thoi gian writer xac nhan ~15s -> cho 150s moi chieu cho chac.
const CHO_S = 150;

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString() };
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}
function sql(cau) {
  const out = execFileSync(MYSQL,
    ["-u", "root", "-h", "127.0.0.1", "-D", "total_parking", "-N", "-B", "-e", cau],
    { encoding: "utf8", timeout: 60000, env: { ...process.env, MYSQL_PWD: "12345678" } });
  return out.trim().split(/\r?\n/).filter((d) => d.length).map((d) => d.split("\t"));
}
const ngu = (s) => execFileSync("powershell.exe",
  ["-NoProfile", "-Command", `Start-Sleep -Seconds ${s}`], { timeout: (s + 30) * 1000 });
async function reload() { try { await fetch(RELOAD, { method: "POST", body: "" }); } catch {} }
async function docApi(thu = 10) {
  for (let i = 0; i < thu; i++) {
    try { const r = await fetch(API, { cache: "no-store" }); if (r.ok) return await r.json(); }
    catch {}
    ngu(5);
  }
  throw new Error("khong goi duoc " + API);
}

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "tu-ha-bat.json"), JSON.stringify(soLieu, null, 1), "utf8");

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
if (bam(DLL_SOURCE) !== bam(DLL_DEPLOY)) {
  ghi("deploy_khop_source", false, "source != deploy");
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, bam(DLL_SOURCE).slice(0, 12));

// ---------------------------------------------------- chup nguyen trang TRUOC
const tatTruoc = new Set(sql("SELECT block_no FROM block WHERE is_active = 0").map((d) => d[0]));
soLieu.block_tat_truoc = [...tatTruoc];
luu();

// Chon ba khoi trong, dang ket noi, khong lien quan nhau.
const CHON = (n) => `
  SELECT b.block_no, p.port, b.slot_count, b.zone_id
    FROM plc_device p JOIN block b ON b.block_id = p.block_id
   WHERE p.is_active = 1 AND b.is_active = 1 AND p.is_connected = 1
     AND NOT EXISTS (SELECT 1 FROM plc_slot_state s
                      WHERE s.block_id = b.block_id AND s.card_code IS NOT NULL)
     AND NOT EXISTS (SELECT 1 FROM parking_session ps
                      WHERE ps.block_id = b.block_id AND ps.active_card_id IS NOT NULL)
     AND NOT EXISTS (SELECT 1 FROM vehicle_routing r
                      WHERE r.block_no = b.block_no AND r.outcome = 'ROUTED'
                        AND r.decided_at > NOW(3) - INTERVAL 90 SECOND)
   ORDER BY b.block_no LIMIT ${n}`;

let A = null, B = null, C = null, daSuaConfig = false;

try {
  const ch = sql(CHON(3));
  if (ch.length < 3) throw new Error(`chi tim duoc ${ch.length} khoi trong, can 3`);
  [A, B, C] = ch.map((d) => ({ block_no: d[0], port: d[1], slot_count: Number(d[2]), zone_id: d[3] }));
  soLieu.khoi_thu = { A, B, C };
  luu();
  console.log(`  (khoi thu: A=${A.block_no} ep mat ket noi, B=${B.block_no} dat kich tran, C=${C.block_no} nguoi ha tay)`);

  // ---- bat tinh nang + rut nguong xuong 1 phut ----
  copyFileSync(CONFIG, LUU_CONFIG);
  let cfg = readFileSync(CONFIG, "utf8");
  cfg = cfg.replace(/("plc:tuDongCongVanHanh"\s+value=")[^"]*(")/, "$1true$2")
           .replace(/("plc:haSauPhut"\s+value=")[^"]*(")/, "$11$2")
           .replace(/("plc:batSauPhut"\s+value=")[^"]*(")/, "$11$2");
  writeFileSync(CONFIG, cfg, "utf8");
  daSuaConfig = true;
  ngu(30);
  const sauBat = await docApi();
  ghi("bat_duoc_tinh_nang",
      sauBat.cong_van_hanh?.bat_tinh_nang === true && sauBat.cong_van_hanh?.ha_sau_phut === 1,
      `bat_tinh_nang=${sauBat.cong_van_hanh?.bat_tinh_nang}, ha_sau_phut=${sauBat.cong_van_hanh?.ha_sau_phut}`);

  // ---- dat lai bo dem cho khoi thu ----
  //
  // BAT BUOC, khong phai don dep cho gon. Moi lan chay lam A doi co hai lan
  // (ha roi bat), nen chay hai lan la A cham tran 4 va dich vu tu choi doi tiep.
  // Lan chay thu ba se FAIL voi ly do "khong tu ha" trong khi ma hoan toan dung —
  // do chinh la lop an toan thu ba dang lam viec.
  sql(`UPDATE block SET so_lan_doi_hom_nay = 0, ngay_dem = CURDATE()
        WHERE block_no IN (${A.block_no}, ${C.block_no})`);

  // ---- dung ba tinh huong cung luc ----
  // A: ep mat ket noi that
  sql(`UPDATE plc_device p JOIN block b ON b.block_id=p.block_id
          SET p.port = 65500 WHERE b.block_no = ${A.block_no}`);
  // B: cung ep mat ket noi, NHUNG da kich tran so lan doi -> phai KHONG bi ha
  sql(`UPDATE plc_device p JOIN block b ON b.block_id=p.block_id
          SET p.port = 65501 WHERE b.block_no = ${B.block_no}`);
  sql(`UPDATE block SET so_lan_doi_hom_nay = 99, ngay_dem = CURDATE() WHERE block_no = ${B.block_no}`);
  // C: NGUOI ha tay (khong co dau tu_dong_ha_luc), PLC van song -> phai KHONG bi tu bat lai
  sql(`UPDATE block SET is_active = 0, tu_dong_ha_luc = NULL WHERE block_no = ${C.block_no}`);
  await reload();

  const zoneTruoc = Number(sql(`SELECT total_mech FROM v_zone_capacity WHERE zone_id = ${A.zone_id}`)[0][0]);
  soLieu.zone_total_mech_truoc = zoneTruoc;

  console.log(`  (cho ${CHO_S}s de dich vu ha...)`);
  ngu(CHO_S);

  // Tra ve nhan "co"/"trong" thay vi chuoi rong: out.trim() trong ham sql xoa
  // mat tab cuoi dong, nen mot cot rong se thanh undefined chu khong phai "".
  const q = (bn) => sql(`SELECT is_active, IF(tu_dong_ha_luc IS NULL,'trong','co') FROM block WHERE block_no = ${bn}`)[0];
  const a1 = q(A.block_no), b1 = q(B.block_no), c1 = q(C.block_no);
  soLieu.sau_khi_cho_ha = { A: a1, B: b1, C: c1 };
  luu();

  ghi("mat_ket_noi_thi_tu_ha", a1[0] === "0" && a1[1] === "co",
      `block ${A.block_no}: is_active=${a1[0]}, tu_dong_ha_luc=${a1[1]}`);

  // Ban dau phep kiem nay doi "suc chua giam DUNG slot_count cua A". Do la GIA
  // DINH SAI: khi bat tinh nang, dich vu ha MOI khoi co PLC chet qua nguong, chu
  // khong rieng khoi minh ep. Lan chay dau zone 4 giam 30 chu khong phai 5.
  //
  // Thu thuc su can chung minh la: co van hanh DIEU KHIEN duoc suc chua — tuc
  // total_mech cua zone bang dung tong slot_count cua cac khoi con bat, va khoi A
  // khong con nam trong tap do.
  const zoneSau = Number(sql(`SELECT total_mech FROM v_zone_capacity WHERE zone_id = ${A.zone_id}`)[0][0]);
  const tongConBat = Number(sql(
    `SELECT COALESCE(SUM(slot_count),0) FROM block
      WHERE zone_id = ${A.zone_id} AND kind = 'Mechanical' AND is_active = 1`)[0][0]);
  const aConTinh = Number(sql(
    `SELECT COUNT(*) FROM block WHERE block_no = ${A.block_no} AND is_active = 1`)[0][0]);
  soLieu.suc_chua = { zoneTruoc, zoneSau, tongConBat, aConTinh };
  ghi("khoi_bi_ha_roi_khoi_suc_chua", zoneSau === tongConBat && aConTinh === 0 && zoneSau < zoneTruoc,
      `zone ${A.zone_id}: total_mech ${zoneTruoc} -> ${zoneSau}` +
      `, tong slot_count khoi con bat = ${tongConBat} (${zoneSau === tongConBat ? "khop" : "LECH"})` +
      `, khoi ${A.block_no} con duoc tinh: ${aConTinh}`);

  ghi("tran_doi_co_chan_dao_dong", b1[0] === "1",
      `block ${B.block_no} da kich tran (so_lan_doi=99): is_active=${b1[0]} (phai van la 1)`);

  ghi("khong_dung_khoi_nguoi_ha_tay", c1[0] === "0" && c1[1] === "trong",
      `block ${C.block_no} nguoi ha tay: is_active=${c1[0]} (phai van la 0), tu_dong_ha_luc=${c1[1]}`);

  // ---- khoi phuc ket noi cho A, cho tu bat lai ----
  sql(`UPDATE plc_device p JOIN block b ON b.block_id=p.block_id
          SET p.port = ${A.port} WHERE b.block_no = ${A.block_no}`);
  await reload();
  console.log(`  (cho ${CHO_S}s de dich vu bat lai...)`);
  ngu(CHO_S);

  const a2 = q(A.block_no);
  soLieu.sau_khi_cho_bat = { A: a2 };
  luu();
  ghi("noi_lai_thi_tu_bat", a2[0] === "1" && a2[1] === "trong",
      `block ${A.block_no}: is_active=${a2[0]}, tu_dong_ha_luc=${a2[1]}`);
} catch (e) {
  ghi("phep_thu_tu_ha_bat", false, e.message);
} finally {
  // ---------------- KHOI PHUC: cau hinh truoc, roi du lieu ----------------
  try {
    if (daSuaConfig) { copyFileSync(LUU_CONFIG, CONFIG); ngu(25); }
    for (const k of [A, B, C]) {
      if (!k) continue;
      sql(`UPDATE plc_device p JOIN block b ON b.block_id=p.block_id
              SET p.port = ${k.port} WHERE b.block_no = ${k.block_no}`);
    }
    const ds = [A, B, C].filter(Boolean).map((k) => k.block_no);
    if (ds.length) sql(`UPDATE block SET so_lan_doi_hom_nay = 0 WHERE block_no IN (${ds.join(",")})`);

    // Khoi phuc DUNG tap block dang tat luc bat dau — khong chi may khoi minh dung.
    const danhSach = [...tatTruoc];
    sql("UPDATE block SET is_active = 1, tu_dong_ha_luc = NULL WHERE is_active = 0" +
        (danhSach.length ? ` AND block_no NOT IN (${danhSach.join(",")})` : ""));
    await reload();

    const tatSau = new Set(sql("SELECT block_no FROM block WHERE is_active = 0").map((d) => d[0]));
    soLieu.block_tat_sau = [...tatSau];
    const khop = tatSau.size === tatTruoc.size && [...tatSau].every((x) => tatTruoc.has(x));
    const cfgVe = readFileSync(CONFIG, "utf8").includes('"plc:tuDongCongVanHanh" value="false"');
    ghi("khoi_phuc_nguyen_trang", khop && cfgVe,
        `block tat: ${tatTruoc.size} -> ${tatSau.size} (${khop ? "khop" : "LECH"})` +
        `, tinh nang ${cfgVe ? "da tat lai" : "VAN CON BAT"}`);
  } catch (e) {
    ghi("khoi_phuc_nguyen_trang", false, "KHONG KHOI PHUC DUOC: " + e.message +
        " — chay tay: UPDATE block SET is_active=1, tu_dong_ha_luc=NULL WHERE is_active=0;" +
        " va chep lai Web.config tu " + LUU_CONFIG);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
