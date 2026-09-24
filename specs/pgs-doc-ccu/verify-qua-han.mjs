// Kiem chung AC-02 va AC-03 bang cach ha nguong qua han xuong 1 giay.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-qua-han.mjs"
//
// VI SAO CAN SCRIPT RIENG
// Ngoai hien truong luc nay ca 5 ZCU deu X3 = 1 va goi deu tuoi, nen trong
// verify-ccu.mjs hai phep kiem mang ten AC-02/AC-03 chay tren tap RONG: chung
// PASS ma khong chung minh gi. Muon cham vao nhanh "qua han" thi phai tao ra
// tinh huong qua han.
//
// Cach re nhat ma khong phai them ma: ha pgs:zcuQuaHanMs xuong 1 giay. Moi ZCU
// lap tuc qua han, nhom dang_ket_noi phai rong va ca 5 phai roi vao dong_bang.
//
// Script tu khoi phuc cau hinh cu o cuoi, ke ca khi that bai giua chung.

import { execFileSync } from "node:child_process";
import { copyFileSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CONFIG = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\Web.config";
const LUU = CONFIG + ".truoc-qua-han";
const API = "http://localhost:8080/PgsStatus/Index";
const ARTIFACTS = join(__dirname, "artifacts");

const ketQua = [];
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}
const ngu = (s) =>
  execFileSync("powershell.exe", ["-NoProfile", "-Command", `Start-Sleep -Seconds ${s}`],
               { timeout: (s + 15) * 1000 });

async function docApi(thu = 8) {
  for (let i = 0; i < thu; i++) {
    try {
      const r = await fetch(API, { cache: "no-store" });
      if (r.ok) return await r.json();
    } catch { /* IIS dang nap lai */ }
    ngu(5);
  }
  throw new Error("khong goi duoc " + API);
}

mkdirSync(ARTIFACTS, { recursive: true });
copyFileSync(CONFIG, LUU);

let batDau = null;
try {
  batDau = await docApi();

  // --- ha nguong xuong 1 giay ---
  const goc = readFileSync(CONFIG, "utf8");
  const ha = goc.replace(/("pgs:zcuQuaHanMs"\s+value=")\d+(")/, "$11$2");
  if (ha === goc) throw new Error("khong tim thay khoa pgs:zcuQuaHanMs trong Web.config");
  writeFileSync(CONFIG, ha, "utf8");

  // Sua Web.config lam IIS nap lai ung dung. Cho vong doc bat duoc it nhat mot
  // vong 5 ZCU (khe giua hai goi cung mot ZCU do duoc 2,50 giay).
  ngu(30);
  const sau = await docApi();

  const nguong = sau.nguong_qua_han_giay;
  ghi("nguong_da_ha", nguong === 1, `nguong_qua_han_giay = ${nguong}`);

  const dk = sau.dang_ket_noi ?? {};
  const db = sau.dong_bang ?? {};
  const zcus = sau.zcus ?? [];

  // AC-03: moi ZCU co goi cu hon 1 giay phai bi danh dau qua han.
  const nenQuaHan = zcus.filter((z) => z.tuoi_goi_s > nguong);
  ghi("zcu_bi_danh_dau_qua_han",
      nenQuaHan.length > 0 && nenQuaHan.every((z) => z.qua_han === true),
      `${nenQuaHan.length}/${zcus.length} zcu co tuoi > ${nguong}s, tat ca deu co co qua_han`);

  // AC-02: ZCU qua han KHONG duoc nam trong nhom dang ket noi, phai nam o
  // nhom dong bang. Day la nhanh ma verify-ccu.mjs khong cham toi duoc.
  const idDangNoi = new Set(dk.zcu_ids ?? []);
  const idDongBang = new Set(db.zcu_ids ?? []);
  const xepDung = nenQuaHan.every((z) => !idDangNoi.has(z.zcu_id) && idDongBang.has(z.zcu_id));
  ghi("qua_han_roi_vao_nhom_dong_bang", xepDung,
      `dang_ket_noi=[${(dk.zcu_ids ?? []).join(",")}] dong_bang=[${(db.zcu_ids ?? []).join(",")}]`);

  // Khong ZCU nao duoc dem hai lan. Day la loi M1 ma review bat duoc.
  const trung = (dk.zcu_ids ?? []).filter((id) => idDongBang.has(id));
  const tongSo = (dk.so_zcu ?? 0) + (db.so_zcu ?? 0);
  ghi("khong_dem_trung", trung.length === 0 && tongSo === zcus.length,
      `trung=[${trung.join(",")}], ${dk.so_zcu}+${db.so_zcu}=${tongSo} vs ${zcus.length} zcu`);

  // So hoc tung nhom phai khop chinh xac.
  //
  // Ban dau phep kiem nay doi "moi ZCU deu qua han nen nhom song ve 0". Do la
  // KY VONG SAI: nguong 1 giay ma chu ky goi la 2,50 giay, nen tai mot thoi
  // diem chi MOT SO ZCU qua han. Phep kiem dung la: so cua nhom dong bang phai
  // bang tong cua dung nhung ZCU bi xep vao do, khong thua khong thieu.
  const conSong = (z) => z.dang_ket_noi && z.tuoi_goi_s <= nguong;
  const mongSong = zcus.filter(conSong);
  const mongChet = zcus.filter((z) => !conSong(z));
  const cong = (ds, k) => ds.reduce((a, z) => a + z[k], 0);

  const khopSong = dk.co_xe === cong(mongSong, "co_xe") &&
                   dk.khong_co_xe === cong(mongSong, "khong_co_xe");
  const khopChet = db.co_xe === cong(mongChet, "co_xe") &&
                   db.khong_co_xe === cong(mongChet, "khong_co_xe");
  // Va quan trong nhat: hai nhom cong lai phai bang tong toan bo, khong hut
  // khong thua — chung minh khong ZCU nao bi bo quen hay dem hai lan.
  const tronVen = dk.co_xe + db.co_xe === cong(zcus, "co_xe");

  ghi("so_hoc_tung_nhom_khop", khopSong && khopChet && tronVen,
      `song co_xe=${dk.co_xe}/${cong(mongSong, "co_xe")}` +
      `, dong_bang co_xe=${db.co_xe}/${cong(mongChet, "co_xe")}` +
      `, tong ${dk.co_xe + db.co_xe}/${cong(zcus, "co_xe")}`);

  writeFileSync(join(ARTIFACTS, "qua-han.json"),
    JSON.stringify({ truoc: batDau, sau }, null, 1), "utf8");
} catch (e) {
  ghi("phep_thu_qua_han", false, e.message);
} finally {
  // Khoi phuc cau hinh du co loi gi xay ra.
  copyFileSync(LUU, CONFIG);
  console.log("\nDa khoi phuc Web.config, cho IIS nap lai...");
  ngu(25);
  try {
    const ve = await docApi();
    console.log(`  nguong_qua_han_giay tro lai = ${ve.nguong_qua_han_giay}`);
    if (ve.nguong_qua_han_giay !== 10) {
      ghi("khoi_phuc_cau_hinh", false, `nguong = ${ve.nguong_qua_han_giay}, mong doi 10`);
    } else {
      ghi("khoi_phuc_cau_hinh", true, "nguong ve 10s");
    }
  } catch (e) {
    ghi("khoi_phuc_cau_hinh", false, e.message);
  }
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exit(rot.length === 0 ? 0 : 1);
