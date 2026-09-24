// Kiem chung task-04 — moi PLC da khai bao deu nam trong vong poll.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-poll-du.mjs"
//
// CANH BAO: script nay BAT poll cho cac PLC dang tat, tuc GHI D1004 = 0 xuong
// chung o nhip poll dau tien. Chi chay sau khi co nguoi xac nhan hien truong.
//
// No do moc TRUOC khi doi gi, va neu so khoi online TUT sau khi bat thi bao FAIL
// kem danh sach de khoi phuc — mo them ket noi FINS co the dung gioi han khe cua
// PLC (loi 0x20).

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\bin\\TotalParking.dll";
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const API = "http://localhost:8080/PlcStatus/Index";
const RELOAD = "http://localhost:8080/PlcStatus/Reload";

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
async function docApi(thu = 10) {
  for (let i = 0; i < thu; i++) {
    try { const r = await fetch(API, { cache: "no-store" }); if (r.ok) return await r.json(); }
    catch {}
    ngu(5);
  }
  throw new Error("khong goi duoc " + API);
}

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "poll-du.json"), JSON.stringify(soLieu, null, 1), "utf8");

// ------------------------------------------------- 0. deploy khop source chua
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
if (bam(DLL_SOURCE) !== bam(DLL_DEPLOY)) {
  ghi("deploy_khop_source", false, "source != deploy");
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, bam(DLL_SOURCE).slice(0, 12));

// ------------------------------------------------ do moc TRUOC khi doi bat cu gi
const truoc = await docApi();
const onlineTruoc = (truoc.blocks || []).filter((b) => b.online).length;
const sePhaiBat = sql(
  `SELECT b.block_no FROM plc_device d JOIN block b ON b.block_id = d.block_id
    WHERE d.is_active = 0 ORDER BY b.block_no`).map((d) => d[0]);

soLieu.truoc = {
  online: onlineTruoc,
  trong_vong_poll: (truoc.blocks || []).length,
  se_phai_bat: sePhaiBat,
  xac_nhan_hien_truong: "Nguoi dung xac nhan 24/09: khong co tho dang lam viec tren cac khoi nay",
};
luu();
console.log(`  (moc truoc: ${onlineTruoc} online / ${(truoc.blocks || []).length} trong poll` +
            `, se bat them ${sePhaiBat.length} khoi: ${sePhaiBat.join(" ") || "khong co"})`);

// ------------------------------------------------------------ bat + Reload
sql("UPDATE plc_device SET is_active = 1 WHERE is_active = 0");
try { await fetch(RELOAD, { method: "POST", body: "" }); } catch {}

// Cho qua mot chu ky poll de cac ket noi moi kip bat tay.
ngu(45);

const sau = await docApi();
const onlineSau = (sau.blocks || []).filter((b) => b.online).length;
const trongPoll = (sau.blocks || []).length;
const trongCsdl = Number(sql("SELECT COUNT(*) FROM plc_device WHERE is_active = 1")[0][0]);
const conBoQuen = Number(sql("SELECT COUNT(*) FROM plc_device WHERE is_active = 0")[0][0]);

soLieu.sau = { online: onlineSau, trong_vong_poll: trongPoll, trong_csdl: trongCsdl, con_bo_quen: conBoQuen };
luu();

ghi("khong_con_plc_bi_bo_quen", conBoQuen === 0, `so PLC is_active = 0: ${conBoQuen}`);

ghi("poll_khop_csdl", trongPoll === trongCsdl,
    `vong poll ${trongPoll} vs CSDL ${trongCsdl}` +
    (trongPoll === trongCsdl ? "" : " — co the quen goi Reload"));

// Mo them ket noi FINS co the dung gioi han khe cua PLC (loi 0x20) va lam CAC
// KHOI KHAC rot. Neu so online tut thi migration da gay hai.
ghi("khong_mat_ket_noi_hang_loat", onlineSau >= onlineTruoc,
    `online ${onlineTruoc} -> ${onlineSau}` +
    (onlineSau < onlineTruoc
      ? ` — TUT ${onlineTruoc - onlineSau}, xem artifacts/poll-du.json de khoi phuc`
      : ""));

// Cac khoi vua bat co that su ket noi duoc khong. Khong phai tieu chi dat/khong
// dat: mot khoi moi bat co the dang mat dien that. Nhung phai nhin thay duoc.
if (sePhaiBat.length) {
  const trangThai = (sau.blocks || [])
    .filter((b) => sePhaiBat.includes(String(b.block_no)))
    .map((b) => `${b.block_no}=${b.online ? "online" : "chua noi duoc"}`);
  soLieu.khoi_vua_bat = trangThai;
  console.log(`  (khoi vua bat: ${trangThai.join(", ")})`);
}

luu();
const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
