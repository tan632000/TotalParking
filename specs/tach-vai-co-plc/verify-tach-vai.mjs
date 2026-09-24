// Kiem chung task-01 — truy van poll thoi phu thuoc co van hanh.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tach-vai.mjs"
//
// VI SAO DUNG TRANSACTION CHU KHONG HA CO THAT
//   - Ha co that roi goi Reload se dong phien FINS cua khoi do; mo lai co the
//     vuong loi het khe 0x20 voi thoi gian cho 5 phut, co tien le ket hang gio.
//     Mot phep kiem khong duoc phep lam rot PLC that.
//   - Trong transaction, gia tri 0 khong lo ra ngoai, nen LedPublisher va tai xe
//     khong bao gio thay so sai.
//   - ROLLBACK la nguyen tu, khong phu thuoc script con song hay bi kill —
//     khac han try/finally cua Node, von khong bat duoc SIGTERM.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\bin\\TotalParking.dll";
const REPO_CS = join(GOC, "TotalParking", "Services", "PlcDeviceRepository.cs");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";

const ketQua = [];
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}
function ket() {
  const rot = ketQua.filter((k) => !k.dat);
  console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
  process.exitCode = rot.length === 0 ? 0 : 1;
  if (rot.length > 0) throw new Error("co phep kiem FAIL");
}

// Chay SQL, tra ve cac dong dang mang cot. Mat khau qua bien moi truong, khong
// bao gio nam tren dong lenh (no lo ra trong danh sach tien trinh).
function sql(cau) {
  const out = execFileSync(MYSQL,
    ["-u", "root", "-h", "127.0.0.1", "-D", "total_parking", "-N", "-B", "-e", cau],
    { encoding: "utf8", timeout: 60000, env: { ...process.env, MYSQL_PWD: "12345678" } });
  return out.trim().split(/\r?\n/).filter((d) => d.length).map((d) => d.split("\t"));
}

mkdirSync(ARTIFACTS, { recursive: true });

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
let hSource, hDeploy;
try {
  hSource = bam(DLL_SOURCE);
  hDeploy = bam(DLL_DEPLOY);
} catch (e) {
  ghi("deploy_khop_source", false, "khong doc duoc DLL: " + e.message);
  ket();
}
if (hSource !== hDeploy) {
  ghi("deploy_khop_source", false, `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
  console.log("\nCHUA DEPLOY — build roi chep bin\\*.dll sang ban deploy, roi chay lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

// ----------------------------------- 1. ma nguon khong con loc theo co van hanh
//
// Phep kiem TINH nay la BAT BUOC, khong phai bo sung. Hien khong co block nao
// is_active = 0, nen hai dang truy van tra cung so dong — mot phep do runtime
// don thuan se PASS ke ca khi chua sua gi.
{
  const src = readFileSync(REPO_CS, "utf8");
  const iGetAll = src.indexOf("public IList<PlcDevice> GetAll");
  const iMap = src.indexOf("private static PlcDevice Map");
  const than = iGetAll >= 0 && iMap > iGetAll ? src.slice(iGetAll, iMap) : "";

  // Chi soi phan SINH TRUY VAN, bo qua chu thich: chu thich co nhac ten cot de
  // giai thich, va cam ky tu do trong chu thich la sai muc tieu.
  const dongLenh = than.split("\n")
    .filter((d) => !d.trim().startsWith("//"))
    .join("\n");

  const conLoc = /WHERE[^"]*b\.is_active/.test(dongLenh);
  const conLocPlc = /WHERE[^"]*p\.is_active\s*=\s*1/.test(dongLenh);
  ghi("ma_khong_con_loc_co_van_hanh", !conLoc && conLocPlc,
      `loc b.is_active: ${conLoc ? "VAN CON" : "da go"}` +
      `, loc p.is_active: ${conLocPlc ? "con" : "BI GO NHAM"}`);
}

// --------------------- 2. transaction phan biet duoc truy van moi voi truy van cu
//
// Chon khoi dang trong bang CSDL, khong doc thanh ghi: doc truc tiep D200-D400
// can hon 400 khung FINS chen vao vong poll ma khong cho biet gi them —
// plc_slot_state chinh la thu vong poll ghi ra.
let soLieu = {};
{
  const CHON = `
    SELECT b.block_no FROM plc_device p JOIN block b ON b.block_id = p.block_id
     WHERE p.is_active = 1 AND b.is_active = 1
       AND NOT EXISTS (SELECT 1 FROM plc_slot_state s
                        WHERE s.block_id = b.block_id AND s.card_code IS NOT NULL)
       AND NOT EXISTS (SELECT 1 FROM parking_session ps
                        WHERE ps.block_id = b.block_id AND ps.active_card_id IS NOT NULL)
       AND NOT EXISTS (SELECT 1 FROM vehicle_routing r
                        WHERE r.block_no = b.block_no AND r.outcome = 'ROUTED'
                          AND r.decided_at > NOW(3) - INTERVAL 90 SECOND)
     ORDER BY b.block_no LIMIT 1`;

  const chon = sql(CHON);
  if (chon.length === 0) {
    ghi("transaction_phan_biet_hai_truy_van", false,
        "khong tim duoc khoi nao dang trong de thu — khong doan bua, dung lai");
    ket();
  }
  const blockNo = chon[0][0];

  // Hai dang truy van, dem trong CUNG mot transaction roi ROLLBACK.
  const DEM_MOI = "SELECT COUNT(*) FROM plc_device p JOIN block b ON b.block_id=p.block_id WHERE p.is_active=1";
  const DEM_CU = DEM_MOI + " AND b.is_active=1";
  const r = sql(`
    START TRANSACTION;
    UPDATE block SET is_active = 0 WHERE block_no = ${blockNo};
    ${DEM_MOI};
    ${DEM_CU};
    ROLLBACK;`);

  const moi = Number(r[0][0]);
  const cu = Number(r[1][0]);
  soLieu = { block_thu: blockNo, truy_van_moi: moi, truy_van_cu: cu };

  // Phai KHAC nhau. Bang nhau nghia la phep thu khong phan biet duoc gi.
  ghi("transaction_phan_biet_hai_truy_van", moi === cu + 1,
      `block thu ${blockNo}: truy van moi giu ${moi}, truy van cu con ${cu}` +
      (moi === cu ? "  — BANG NHAU, phep thu vo nghia" : ""));
}

// --------------------------------------- 3. rollback khong de lai co nao bi ha
{
  const r = sql("SELECT COALESCE(SUM(is_active = 0), 0) FROM block");
  const con = Number(r[0][0]);
  soLieu.block_bi_ha_sau_rollback = con;
  ghi("khong_de_lai_co_nao_bi_ha", con === 0, `so block is_active = 0: ${con}`);
}

writeFileSync(join(ARTIFACTS, "tach-vai.json"), JSON.stringify(soLieu, null, 1), "utf8");
ket();
