// Kiem chung task-03 — PLC mat ket noi sinh canh bao tu dong.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-canh-bao-plc.mjs"
//
// Phep kiem quan trong nhat la su_co_lan_hai_sinh_duoc_canh_bao_moi: mot cai dat
// quen xoa khoa chong trung luc dong van PASS het cac phep con lai neu chi chay
// mot chu trinh. Vi vay script chay TRON CHU TRINH HAI LAN.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const DLL_DEPLOY = join(DEPLOY, "bin", "TotalParking.dll");
const CONFIG_DEPLOY = join(DEPLOY, "Web.config");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const RELOAD = "http://localhost:8080/PlcStatus/Reload";

const MA_LOI = "PLC-CONN-01";
const TIEN_TO = "PLC_MAT_KET_NOI:";
const PORT_CHET = 65500;

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString(), moc_thoi_gian: [] };

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
const ngu = (s) => execFileSync("powershell.exe",
  ["-NoProfile", "-Command", `Start-Sleep -Seconds ${s}`], { timeout: (s + 20) * 1000 });

async function reload() {
  try { await fetch(RELOAD, { method: "POST", body: "" }); } catch { /* bo qua */ }
}

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "canh-bao-plc.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");
const moc = (nhan) => {
  soLieu.moc_thoi_gian.push({ giay: Math.round((Date.now() - T0) / 1000), nhan });
  luu();
};
const T0 = Date.now();

// Doi mot dieu kien SQL tro thanh dung. Tra ve gia tri cuoi cung doc duoc.
// Hoi 3 giay mot lan thay vi ngu mot cuc: phep kiem hong thi biet som hon, va
// phep kiem dat thi khong phai cho het han.
function choToi(cau, dat, giayToiDa) {
  const han = Date.now() + giayToiDa * 1000;
  let v = mot(cau);
  while (!dat(v) && Date.now() < han) { ngu(3); v = mot(cau); }
  return v;
}

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
{
  let hSource, hDeploy;
  try { hSource = bam(DLL_SOURCE); hDeploy = bam(DLL_DEPLOY); }
  catch (e) {
    ghi("deploy_khop_source", false, "khong doc duoc DLL: " + e.message);
    process.exit(1);
  }
  if (hSource !== hDeploy) {
    ghi("deploy_khop_source", false,
        `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
    console.log("\nCHUA DEPLOY — chep bin\\ sang ban deploy roi chay lai.");
    process.exit(1);
  }
  ghi("deploy_khop_source", true, hSource.slice(0, 12));
}

// Nguong doc tu chinh Web.config BAN DEPLOY, khong ghim so trong script: script
// va ung dung phai dung cung mot con so, neu khong phep do se lech ma khong ai
// nhin ra.
const NGUONG_PHUT = (() => {
  const m = readFileSync(CONFIG_DEPLOY, "utf8")
    .match(/plc:canhBaoMatKetNoiSauPhut"\s+value="(\d+)"/);
  return m ? Number(m[1]) : 5;
})();
soLieu.nguong_phut = NGUONG_PHUT;

// --------------------------------------------------------- chon khoi de thu
//
// Cung tieu chi voi packet truoc: khoi dang TRONG — khong con the trong o,
// khong phien gui xe dang mo, khong lenh xep xe nao trong 90 giay.
const chon = sql(`
  SELECT p.plc_id, b.block_no, p.port, COALESCE(p.connected_changed_at,''), COALESCE(p.is_connected,-1)
    FROM plc_device p JOIN block b ON b.block_id = p.block_id
   WHERE p.is_active = 1 AND b.is_active = 1 AND p.is_connected = 1
     AND NOT EXISTS (SELECT 1 FROM plc_slot_state s
                      WHERE s.block_id = b.block_id AND s.card_code IS NOT NULL)
     AND NOT EXISTS (SELECT 1 FROM parking_session ps
                      WHERE ps.block_id = b.block_id AND ps.active_card_id IS NOT NULL)
     AND NOT EXISTS (SELECT 1 FROM vehicle_routing r
                      WHERE r.block_no = b.block_no AND r.outcome = 'ROUTED'
                        AND r.decided_at > NOW(3) - INTERVAL 90 SECOND)
   ORDER BY b.block_no LIMIT 1`);

if (chon.length === 0) {
  ghi("chon_duoc_khoi_thu", false, "khong tim duoc khoi dang trong va dang ket noi");
  console.log("\n0/1 PASS");
  process.exit(1);
}

const [PLC_ID, BLOCK_NO, PORT_GOC, MOC_GOC, KET_NOI_GOC] = chon[0];
const KHOA = TIEN_TO + BLOCK_NO;

// Ghi nguyen trang RA FILE truoc khi doi bat cu thu gi. Neu script chet giua
// chung, day la thu duy nhat cho biet phai tra lai gi.
soLieu.khoi_thu = {
  plc_id: PLC_ID, block_no: BLOCK_NO, khoa: KHOA,
  port_goc: PORT_GOC, connected_changed_at_goc: MOC_GOC, is_connected_goc: KET_NOI_GOC
};
const ID_TRUOC = Number(mot("SELECT COALESCE(MAX(canh_bao_id),0) FROM canh_bao"));
soLieu.canh_bao_id_truoc = ID_TRUOC;
luu();
console.log(`  khoi thu: block ${BLOCK_NO} (plc_id ${PLC_ID}), nguong ${NGUONG_PHUT} phut\n`);

const demCanhBaoThu = (tuId) => Number(mot(
  `SELECT COUNT(*) FROM canh_bao
    WHERE canh_bao_id > ${tuId} AND block_no = ${BLOCK_NO} AND ma_loi = '${MA_LOI}'`));

// Ep PLC rot: doi sang cong khong ai nghe roi nap lai cau hinh. Cho den khi
// CSDL thuc su ghi is_connected = 0 — writer can 3 luot xac nhan nhip 5 giay.
async function epRot() {
  sql(`UPDATE plc_device SET port = ${PORT_CHET} WHERE plc_id = ${PLC_ID}`);
  await reload();
  return choToi(`SELECT COALESCE(is_connected,-1) FROM plc_device WHERE plc_id=${PLC_ID}`,
                (v) => v === "0", 120);
}

async function epNoiLai() {
  sql(`UPDATE plc_device SET port = ${PORT_GOC} WHERE plc_id = ${PLC_ID}`);
  await reload();
  return choToi(`SELECT COALESCE(is_connected,-1) FROM plc_device WHERE plc_id=${PLC_ID}`,
                (v) => v === "1", 120);
}

// Rut ngan nguong bang cach lui moc thoi gian, KHONG sua Web.config: ghi
// Web.config ban deploy lam ung dung nap lai, tuc hai lan khoi dong lai ngay
// giua phep do.
const luiMoc = () => sql(
  `UPDATE plc_device SET connected_changed_at = NOW(3) - INTERVAL ${NGUONG_PHUT + 2} MINUTE
    WHERE plc_id = ${PLC_ID}`);

let idLan1 = 0, idLan2 = 0;

try {
  // ==================================================== CHU TRINH 1
  moc("bat dau chu trinh 1");
  const rot1 = await epRot();
  if (rot1 !== "0") throw new Error(`khong ep duoc rot, is_connected=${rot1}`);
  moc("da rot lan 1");

  luiMoc();
  const id1 = choToi(
    `SELECT COALESCE(MAX(canh_bao_id),0) FROM canh_bao WHERE khoa_chong_trung='${KHOA}'`,
    (v) => Number(v) > 0, 90);
  idLan1 = Number(id1);
  soLieu.canh_bao_lan_1 = idLan1;
  moc("co canh bao lan 1");

  const dong1 = idLan1 > 0
    ? sql(`SELECT nguon, muc_do, COALESCE(zone_id,-1), COALESCE(thiet_bi,''), mo_ta
             FROM canh_bao WHERE canh_bao_id=${idLan1}`)[0]
    : null;
  soLieu.noi_dung_lan_1 = dong1;
  ghi("mat_ket_noi_sinh_canh_bao", idLan1 > 0 && dong1 && dong1[0] === "hardware",
      idLan1 > 0
        ? `id=${idLan1}, nguon=${dong1[0]}, muc_do=${dong1[1]}, mo_ta="${dong1[4]}"`
        : `sau 90 giay van khong co canh bao nao mang khoa ${KHOA}`);

  // ------------------------------------- van loi thi KHONG sinh them
  //
  // 95 giay tuyet doi — hon ba luot cua vong 30 giay. Khong co chong trung thi
  // so dong o day la 4 chu khong phai 1.
  ngu(95);
  const soSauCho = demCanhBaoThu(ID_TRUOC);
  soLieu.so_canh_bao_sau_95_giay = soSauCho;
  moc("da cho 95 giay");
  ghi("van_loi_khong_sinh_them", soSauCho === 1,
      `sau 95 giay PLC van chet: ${soSauCho} canh bao cho block ${BLOCK_NO} (mong doi 1)`);

  // ----------------------------------------- noi lai thi danh dau het
  const noi1 = await epNoiLai();
  if (noi1 !== "1") throw new Error(`khong noi lai duoc, is_connected=${noi1}`);
  moc("da noi lai lan 1");

  const het = choToi(
    `SELECT IF(het_luc IS NULL,'trong','co') FROM canh_bao WHERE canh_bao_id=${idLan1}`,
    (v) => v === "co", 90);
  ghi("noi_lai_thi_danh_dau_het", het === "co",
      het === "co" ? `canh bao ${idLan1} da co het_luc`
                   : `sau 90 giay canh bao ${idLan1} van chua duoc dong`);

  // --------------------------- dong cu VAN CON trong bang, va khoa da xoa
  {
    const r = sql(`SELECT COUNT(*), COALESCE(MAX(khoa_chong_trung),'NULL')
                     FROM canh_bao WHERE canh_bao_id=${idLan1}`)[0];
    const conDong = r[0] === "1";
    const khoaDaXoa = r[1] === "NULL";
    soLieu.sau_khi_dong_lan_1 = { con_dong: conDong, khoa: r[1] };
    ghi("dong_cu_van_con_trong_bang", conDong && khoaDaXoa,
        `dong ${idLan1} ${conDong ? "van con" : "DA BI XOA"}` +
        `, khoa_chong_trung = ${khoaDaXoa ? "NULL (dung)" : r[1] + " — chua xoa"}`);
  }

  // ==================================================== CHU TRINH 2
  //
  // Lap lai tron ven. Neu cai dat quen xoa khoa luc dong, lan nay CSDL se chan
  // dong moi va probe duoi FAIL — loi mot chu trinh khong bao gio bat duoc.
  moc("bat dau chu trinh 2");
  const rot2 = await epRot();
  if (rot2 !== "0") throw new Error(`chu trinh 2: khong ep duoc rot, is_connected=${rot2}`);
  luiMoc();

  const id2 = choToi(
    `SELECT COALESCE(MAX(canh_bao_id),0) FROM canh_bao WHERE khoa_chong_trung='${KHOA}'`,
    (v) => Number(v) > idLan1, 90);
  idLan2 = Number(id2);
  soLieu.canh_bao_lan_2 = idLan2;
  moc("co canh bao lan 2");
  ghi("su_co_lan_hai_sinh_duoc_canh_bao_moi", idLan2 > idLan1,
      idLan2 > idLan1
        ? `lan 1 id=${idLan1}, lan 2 id=${idLan2} — khoa da duoc xoa dung luc dong`
        : `sau 90 giay khong co canh bao moi (id moi nhat ${idLan2}, lan 1 ${idLan1})`);

  const noi2 = await epNoiLai();
  if (noi2 !== "1") throw new Error(`chu trinh 2: khong noi lai duoc, is_connected=${noi2}`);
  choToi(`SELECT IF(het_luc IS NULL,'trong','co') FROM canh_bao WHERE canh_bao_id=${idLan2}`,
         (v) => v === "co", 90);
  moc("da dong canh bao lan 2");

  // ==================================================== NULL KHAC 0
  //
  // NULL nghia la "khong quan sat duoc", khac han 0 nghia la "biet chac dang
  // chet". Dat NULL kem moc lui qua nguong: mot cai dat coi is_connected <> 1
  // la mat ket noi se sinh canh bao o day.
  const idTruocNull = Number(mot("SELECT COALESCE(MAX(canh_bao_id),0) FROM canh_bao"));
  sql(`UPDATE plc_device
          SET is_connected = NULL,
              connected_changed_at = NOW(3) - INTERVAL ${NGUONG_PHUT + 2} MINUTE
        WHERE plc_id = ${PLC_ID}`);
  ngu(75);
  const conNull = mot(`SELECT COALESCE(is_connected,-1) FROM plc_device WHERE plc_id=${PLC_ID}`);
  const soMoi = demCanhBaoThu(idTruocNull);
  soLieu.null_khong_sinh = { is_connected_luc_do: conNull, so_canh_bao_moi: soMoi };
  moc("da thu NULL 75 giay");
  ghi("null_khong_sinh_canh_bao", soMoi === 0 && conNull === "-1",
      `is_connected=${conNull === "-1" ? "NULL" : conNull} suot phep thu` +
      `, canh bao moi cho block ${BLOCK_NO}: ${soMoi} (mong doi 0)`);
} catch (e) {
  ghi("phep_thu_canh_bao_plc", false, e.message);
} finally {
  // ==================================================== KHOI PHUC
  //
  // Tra lai ca ba gia tri. Port sai la khoi do mat ket noi vinh vien, nen day
  // la buoc khong duoc phep bo qua du co loi gi xay ra o tren.
  try {
    sql(`UPDATE plc_device
            SET port = ${PORT_GOC},
                is_connected = ${KET_NOI_GOC === "-1" ? "NULL" : KET_NOI_GOC},
                connected_changed_at = ${MOC_GOC ? `'${MOC_GOC}'` : "NULL"}
          WHERE plc_id = ${PLC_ID}`);
    await reload();

    // Xoa canh bao do chinh phep thu sinh ra. Chi dong cua block thu va chi
    // dong sinh SAU moc dau — canh bao that cua 7 PLC dang chet phai o lai.
    const xoa = demCanhBaoThu(ID_TRUOC);
    sql(`DELETE FROM canh_bao
          WHERE canh_bao_id > ${ID_TRUOC} AND block_no = ${BLOCK_NO} AND ma_loi = '${MA_LOI}'`);

    const con = sql(`SELECT port, COALESCE(is_connected,-1), COALESCE(connected_changed_at,'')
                       FROM plc_device WHERE plc_id=${PLC_ID}`)[0];
    const conLai = demCanhBaoThu(ID_TRUOC);
    soLieu.khoi_phuc = { port: con[0], is_connected: con[1], moc: con[2], da_xoa: xoa };
    luu();

    ghi("khoi_phuc_nguyen_trang",
        con[0] === String(PORT_GOC) && conLai === 0,
        `port ${con[0]} (goc ${PORT_GOC}), is_connected ${con[1]}, da xoa ${xoa} canh bao thu`);
  } catch (e) {
    ghi("khoi_phuc_nguyen_trang", false, "KHONG KHOI PHUC DUOC: " + e.message +
        ` — chay tay: UPDATE plc_device SET port=${PORT_GOC} WHERE plc_id=${PLC_ID};` +
        ` DELETE FROM canh_bao WHERE canh_bao_id>${ID_TRUOC} AND block_no=${BLOCK_NO}` +
        ` AND ma_loi='${MA_LOI}';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
