// Kiem chung: bang LED cong TOTAL lay so cho do thuong tu cam bien.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-led-total.mjs"
//
// Phai chung minh CA HAI CHIEU:
//   - cong TOTAL doi sang so cam bien
//   - cong ZONES KHONG doi (chua biet cam bien nao thuoc zone nao, doan roi
//     day len mui ten chi huong la chi sai duong)

import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\bin\\TotalParking.dll";
const ARTIFACTS = join(__dirname, "artifacts");

const API_LED = "http://localhost:8080/LedStatus/Index";
const API_PGS = "http://localhost:8080/PgsStatus/Index";

const ketQua = [];
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}
function ket() {
  const rot = ketQua.filter((k) => !k.dat);
  console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
  // Dat exitCode roi de Node tu thoat khi event loop rong, thay vi process.exit()
  // ngay lap tuc: fetch giu socket keep-alive, va thoat giua luc handle dang dong
  // lam libuv bung assertion (UV_HANDLE_CLOSING) kem ma thoat rac.
  process.exitCode = rot.length === 0 ? 0 : 1;
  if (rot.length > 0) throw new Error("co phep kiem FAIL");
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
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

let led, pgs;
try {
  led = await (await fetch(API_LED, { cache: "no-store" })).json();
  pgs = await (await fetch(API_PGS, { cache: "no-store" })).json();
  writeFileSync(join(ARTIFACTS, "led-total.json"),
    JSON.stringify({ led_capacity: led.capacity, led_zones: led.zones, pgs }, null, 1), "utf8");
} catch (e) {
  ghi("doc_duoc_hai_endpoint", false, e.message);
  ket();
}

// ------------------------------- 1. so toan bai khop tong cam bien bao trong
//
// Cong het CA hai nhom (dang ket noi + dong bang): nguoi dung chot giu so cuoi
// cung doc duoc thay vi quay ve con so cua CSDL.
{
  const mongDoi = (pgs.zcus ?? []).reduce((a, z) => a + z.khong_co_xe, 0);
  const that = led.capacity?.free_standard;
  ghi("so_toan_bai_theo_cam_bien", that === mongDoi,
      `LED free_standard=${that} vs tong cam bien bao trong=${mongDoi}`);
}

// --------------------------------------- 2. KHONG phai con so cu cua CSDL
//
// Phep kiem tren mot minh chua du: neu cam bien tinh co bang dung suc chua thi
// no PASS ma khong chung minh gi. Nen doi chieu them voi total_standard.
{
  const free = led.capacity?.free_standard;
  const total = led.capacity?.total_standard;
  const coXe = (pgs.zcus ?? []).reduce((a, z) => a + z.co_xe, 0);
  ghi("khong_con_la_so_tinh_cua_csdl", free !== total && coXe > 0,
      `free=${free}, total=${total}, cam bien thay ${coXe} xe dang do`);
}

// ------------------------------------ 3. total_standard VAN la suc chua that
//
// Chi ghi de so TRONG. Tong so o do thuong van lay tu CSDL: do la suc chua
// thuc cua bai, khong phai so cam bien da lap (79 cho 80 o).
{
  const total = led.capacity?.total_standard;
  ghi("total_van_theo_csdl", total === 80, `total_standard=${total}`);
}

// ------------------------------------------ 4. cong ZONES KHONG bi dung toi
//
// Day la phep kiem quan trong nhat ve pham vi. Neu so cam bien ro ri sang
// duong zone thi mui ten chi huong se dan tai xe theo mot con so doan mo.
//
// Kiem TINH chu khong qua HTTP: /LedStatus/Index khong phoi free_standard theo
// tung zone, nen so sanh qua endpoint chi cho 0 === 0 — PASS ma khong chung
// minh gi. Doc thang ma nguon thi bat duoc dung thu can bat.
{
  const repo = readFileSync(join(GOC, "TotalParking", "Services", "LedPanelRepository.cs"), "utf8");

  // Cat lay than ham GetCapacityByZone de soi rieng.
  const iZone = repo.indexOf("public IDictionary<int, LedCapacity> GetCapacityByZone()");
  const iSum = repo.indexOf("public static LedCapacity SumZones");
  const thanZone = iZone >= 0 && iSum > iZone ? repo.slice(iZone, iSum) : "";

  const roRiVaoZone = thanZone.includes("StandardFreeSource");
  const roRiVaoSumZones = iSum >= 0 && repo.slice(iSum).includes("StandardFreeSource");

  // Va toan repo chi duoc co DUNG MOT cho goi, nam trong GetCapacity().
  const iCap = repo.indexOf("public LedCapacity GetCapacity()");
  const thanCap = iCap >= 0 && iZone > iCap ? repo.slice(iCap, iZone) : "";
  const soLanGoi = (repo.match(/StandardFreeSource\.SoOTrong/g) ?? []).length;
  const dungChoDuyNhat = soLanGoi === 1 && thanCap.includes("StandardFreeSource.SoOTrong");

  ghi("cong_zones_khong_doi",
      thanZone.length > 0 && !roRiVaoZone && !roRiVaoSumZones && dungChoDuyNhat,
      `GetCapacityByZone ${roRiVaoZone ? "CO GOI" : "khong goi"} cam bien` +
      `, SumZones ${roRiVaoSumZones ? "CO GOI" : "khong goi"}` +
      `, toan repo goi ${soLanGoi} lan${dungChoDuyNhat ? " (dung trong GetCapacity)" : " — SAI CHO"}`);
}

// -------------------------------- 5. so trong khong vuot qua suc chua that
{
  const free = led.capacity?.free_standard ?? 0;
  const total = led.capacity?.total_standard ?? 0;
  ghi("so_trong_hop_ly", free >= 0 && free <= total,
      `0 <= ${free} <= ${total}`);
}

ket();
