// Kiem chung task-02 — is_connected ghi an toan vao CSDL.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-is-connected.mjs"
//
// Phep kiem quan trong nhat la bat_duoc_chuyen_trang_thai: khong co no thi mot
// cai dat chi ghi MOT LAN luc khoi dong van PASS het cac phep con lai.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const DLL_DEPLOY = join(DEPLOY, "bin", "TotalParking.dll");
const LOG = join(DEPLOY, "App_Data", "plc_audit.log");
const WRITER_CS = join(GOC, "TotalParking", "Services", "Plc", "PlcTrangThaiWriter.cs");
const MGR_CS = join(GOC, "TotalParking", "Services", "Plc", "PlcConnectionManager.cs");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const API = "http://localhost:8080/PlcStatus/Index";
const RELOAD = "http://localhost:8080/PlcStatus/Reload";

// Nhip chieu cua writer la 5s, can 3 luot xac nhan -> ~15s de mot thay doi
// duoc ghi. Cho rong rai hon de khong do nham luc dang cho xac nhan.
const CHO_GHI_S = 30;

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
  ["-NoProfile", "-Command", `Start-Sleep -Seconds ${s}`], { timeout: (s + 20) * 1000 });

async function reload() {
  try { await fetch(RELOAD, { method: "POST", body: "" }); } catch { /* bo qua */ }
}

mkdirSync(ARTIFACTS, { recursive: true });

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
let hSource, hDeploy;
try {
  hSource = bam(DLL_SOURCE); hDeploy = bam(DLL_DEPLOY);
} catch (e) {
  ghi("deploy_khop_source", false, "khong doc duoc DLL: " + e.message);
  ket();
}
if (hSource !== hDeploy) {
  ghi("deploy_khop_source", false, `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

// --------------------------- 1. lenh ghi nam NGOAI vong poll (kiem tinh)
//
// Kiem tren ma nguon vi day la rang buoc ve CAU TRUC, khong quan sat duoc tu
// ngoai khi moi thu dang chay tot. LoopAsync chi boc try quanh Task.WhenAll;
// mot MySqlException nem ra tu do se giet vong poll vinh vien.
{
  const mgr = readFileSync(MGR_CS, "utf8");
  const writer = readFileSync(WRITER_CS, "utf8");
  const mgrGoiRepo = /PlcDeviceRepository[^;]*Ghi(TrangThaiKetNoi|MocQuanSat)/.test(mgr);
  const writerCoTryCatch = /catch\s*\(Exception[\s\S]{0,400}PlcAuditLog\.Error/.test(writer);
  const writerRieng = /Task\.Run\(|private static async Task VongAsync/.test(writer);
  ghi("ghi_nam_ngoai_vong_poll", !mgrGoiRepo && writerCoTryCatch && writerRieng,
      `manager goi lenh ghi: ${mgrGoiRepo ? "CO — SAI" : "khong"}` +
      `, writer co try/catch + log: ${writerCoTryCatch}` +
      `, writer chay luong rieng: ${writerRieng}`);
}

// ------------------------------------- 2. co moc quan sat, va NULL dung cho
{
  const r = sql(`SELECT
      COUNT(*),
      SUM(is_connected IS NOT NULL AND last_probe_at IS NULL),
      SUM(is_active = 1 AND last_probe_at IS NULL),
      SUM(is_active = 0 AND is_connected IS NOT NULL)
    FROM plc_device`);
  const [tong, coTrangThaiKhongMoc, dangBatMaKhongMoc, tatMaVanCoTrangThai] = r[0].map(Number);
  soLieu.moc_quan_sat = { tong, coTrangThaiKhongMoc, dangBatMaKhongMoc, tatMaVanCoTrangThai };
  ghi("co_moc_quan_sat",
      coTrangThaiKhongMoc === 0 && dangBatMaKhongMoc === 0 && tatMaVanCoTrangThai === 0,
      `co trang thai ma thieu moc: ${coTrangThaiKhongMoc}` +
      `, dang bat ma thieu moc: ${dangBatMaKhongMoc}` +
      `, da tat ma van co trang thai: ${tatMaVanCoTrangThai}`);
}

// ------------------------------- 3. khong ghi khi trang thai khong doi
{
  const chup = () => new Map(sql(
    "SELECT plc_id, COALESCE(is_connected,-1), COALESCE(connected_changed_at,'') FROM plc_device")
    .map((d) => [d[0], d[1] + "|" + d[2]]));

  const truoc = chup();
  ngu(CHO_GHI_S);
  const sau = chup();

  let doiTrangThai = 0, doiMocMaKhongDoiTrangThai = 0;
  for (const [id, v] of sau) {
    const cu = truoc.get(id);
    if (cu === undefined || cu === v) continue;
    const [tcu, mcu] = cu.split("|");
    const [tmoi, mmoi] = v.split("|");
    if (tcu !== tmoi) doiTrangThai++;
    else if (mcu !== mmoi) doiMocMaKhongDoiTrangThai++;
  }
  soLieu.cua_so_khong_doi = { giay: CHO_GHI_S, doiTrangThai, doiMocMaKhongDoiTrangThai };

  // Tran: trong cua so nay, so thiet bi thuc su doi trang thai phai nho. Vuot
  // tran nghia la dang ghi moi nhip hoac PLC chap chon khong duoc khu rung.
  const TRAN = 5;
  ghi("khong_ghi_khi_khong_doi",
      doiMocMaKhongDoiTrangThai === 0 && doiTrangThai <= TRAN,
      `trong ${CHO_GHI_S}s: ${doiTrangThai} thiet bi doi trang thai (tran ${TRAN})` +
      `, ${doiMocMaKhongDoiTrangThai} thiet bi doi moc MA KHONG doi trang thai`);
}

// --------------------------- 4. ep mot chuyen trang thai that roi khoi phuc
//
// Doi port cua dung MOT dong plc_device sang cong khong ai nghe. Chon khoi dang
// trong, va ghi lai port goc ra artifact TRUOC khi doi bat cu thu gi.
let khoiThu = null, portGoc = null;
try {
  const chon = sql(`
    SELECT p.plc_id, b.block_no, p.port
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
  if (chon.length === 0) throw new Error("khong tim duoc khoi dang trong va dang ket noi");

  const [plcId, blockNo, port] = chon[0];
  khoiThu = { plc_id: plcId, block_no: blockNo };
  portGoc = port;
  soLieu.ep_chuyen_trang_thai = { block_no: blockNo, plc_id: plcId, port_goc: port };
  writeFileSync(join(ARTIFACTS, "is-connected.json"), JSON.stringify(soLieu, null, 1), "utf8");

  const mocTruoc = sql(`SELECT COALESCE(connected_changed_at,'') FROM plc_device WHERE plc_id=${plcId}`)[0][0];

  // --- ep rot: doi sang cong khong ai nghe ---
  sql(`UPDATE plc_device SET port = 65500 WHERE plc_id = ${plcId}`);
  await reload();
  ngu(CHO_GHI_S);
  const r1 = sql(`SELECT COALESCE(is_connected,-1), COALESCE(connected_changed_at,'') FROM plc_device WHERE plc_id=${plcId}`)[0];

  // --- khoi phuc: tra port that ---
  sql(`UPDATE plc_device SET port = ${portGoc} WHERE plc_id = ${plcId}`);
  await reload();
  ngu(CHO_GHI_S);
  const r2 = sql(`SELECT COALESCE(is_connected,-1), COALESCE(connected_changed_at,'') FROM plc_device WHERE plc_id=${plcId}`)[0];

  soLieu.ep_chuyen_trang_thai.moc_truoc = mocTruoc;
  soLieu.ep_chuyen_trang_thai.sau_khi_rot = { is_connected: r1[0], moc: r1[1] };
  soLieu.ep_chuyen_trang_thai.sau_khi_noi_lai = { is_connected: r2[0], moc: r2[1] };

  const rot = r1[0] === "0" && r1[1] !== mocTruoc;
  const noiLai = r2[0] === "1" && r2[1] !== r1[1];
  ghi("bat_duoc_chuyen_trang_thai", rot && noiLai,
      `block ${blockNo}: sau khi rot is_connected=${r1[0]} (moc ${rot ? "da tien" : "KHONG tien"})` +
      `, sau khi noi lai is_connected=${r2[0]} (moc ${noiLai ? "da tien" : "KHONG tien"})`);
} catch (e) {
  ghi("bat_duoc_chuyen_trang_thai", false, e.message);
} finally {
  // Khoi phuc du co loi gi. Port sai la khoi do mat ket noi vinh vien.
  if (khoiThu && portGoc !== null) {
    try {
      sql(`UPDATE plc_device SET port = ${portGoc} WHERE plc_id = ${khoiThu.plc_id}`);
      await reload();
      const con = sql(`SELECT port FROM plc_device WHERE plc_id=${khoiThu.plc_id}`)[0][0];
      ghi("khoi_phuc_port", con === String(portGoc), `port block ${khoiThu.block_no} = ${con} (goc ${portGoc})`);
    } catch (e) {
      ghi("khoi_phuc_port", false, "KHONG KHOI PHUC DUOC: " + e.message +
          ` — chay tay: UPDATE plc_device SET port=${portGoc} WHERE plc_id=${khoiThu.plc_id}`);
    }
  }
}

// ------------------ 5. loi ghi CSDL khong giet vong poll (phep thu that)
//
// Doi TEN cot last_probe_at trong ~25 giay. Writer se nem "Unknown column" moi
// luot chieu; vong poll phai van doc PLC binh thuong va nhat ky phai co dong loi.
//
// Chon last_probe_at chu khong phai is_connected: GhiMocQuanSat chay MOI luot
// chieu (5 giay mot lan) nen chac chan nem loi, con GhiTrangThaiKetNoi chi chay
// khi co thiet bi doi trang thai — trong 25 giay yen tinh thi no khong duoc goi
// lan nao va phep thu se khong ep duoc loi nao ca.
//
// Vi sao khong tat han MySQL: ca he thong dung chung mot CSDL — phien gui xe,
// bang LED, danh muc the. Doi ten mot cot ma chi writer dung thi pham vi anh
// huong nho hon nhieu, va van la phep thu THAT chu khong phai suy luan.
{
  let daDoiTen = false;
  try {
    const truoc = await (await fetch(API, { cache: "no-store" })).json();
    const okTruoc = (truoc.blocks || []).filter((b) => b.last_ok).length;

    sql("ALTER TABLE plc_device CHANGE COLUMN last_probe_at last_probe_at_tam DATETIME(3) NULL");
    daDoiTen = true;
    ngu(25);

    const sau = await (await fetch(API, { cache: "no-store" })).json();
    const okSau = (sau.blocks || []).filter((b) => b.last_ok).length;

    // Vong poll con song: van chay va van co thiet bi doc duoc.
    const pollSong = sau.poll_running === true && okSau > 0;

    // Loi phai duoc GHI LAI, khong nuot im lang.
    let coLog = false;
    try {
      const log = readFileSync(LOG, "utf8");
      coLog = /GHI TRANG THAI[\s\S]{0,200}(last_probe_at|Unknown column)/.test(log.slice(-200000));
    } catch { /* khong doc duoc log */ }

    soLieu.mat_cot = { ok_truoc: okTruoc, ok_sau: okSau, poll_running: sau.poll_running, co_log_loi: coLog };
    ghi("loi_csdl_khong_giet_vong_poll", pollSong && coLog,
        `poll_running=${sau.poll_running}, thiet bi doc duoc ${okTruoc} -> ${okSau}` +
        `, nhat ky co dong loi: ${coLog ? "co" : "KHONG — dang nuot im lang"}`);
  } catch (e) {
    ghi("loi_csdl_khong_giet_vong_poll", false, e.message);
  } finally {
    if (daDoiTen) {
      try {
        sql("ALTER TABLE plc_device CHANGE COLUMN last_probe_at_tam last_probe_at DATETIME(3) NULL");
        const con = sql(`SELECT COUNT(*) FROM information_schema.COLUMNS
                          WHERE TABLE_SCHEMA='total_parking' AND TABLE_NAME='plc_device'
                            AND COLUMN_NAME='last_probe_at'`)[0][0];
        ghi("khoi_phuc_ten_cot", con === "1", `cot last_probe_at ton tai: ${con}`);
      } catch (e) {
        ghi("khoi_phuc_ten_cot", false, "KHONG KHOI PHUC DUOC: " + e.message +
            " — chay tay: ALTER TABLE plc_device CHANGE COLUMN last_probe_at_tam last_probe_at DATETIME(3) NULL");
      }
    }
  }
}

writeFileSync(join(ARTIFACTS, "is-connected.json"), JSON.stringify(soLieu, null, 1), "utf8");
ket();

function ket() {
  const rot = ketQua.filter((k) => !k.dat);
  console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
  process.exitCode = rot.length === 0 ? 0 : 1;
  if (rot.length > 0) throw new Error("co phep kiem FAIL");
}
