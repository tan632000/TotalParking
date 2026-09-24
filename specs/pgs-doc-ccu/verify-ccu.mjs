// Kiem chung task-01 — tang cam bien do thuong doc tu CCU.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-ccu.mjs"
//
// Bay phep kiem co ten, in PASS/FAIL tung cai, thoat 0 chi khi ca bay PASS.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { connect } from "node:net";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DLL_SOURCE = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking\\bin\\TotalParking.dll";
const CSPROJ = join(GOC, "TotalParking", "TotalParking.csproj");
const DIR_PGS = join(GOC, "TotalParking", "Services", "Pgs");
const ARTIFACTS = join(__dirname, "artifacts");

const API = "http://localhost:8080/PgsStatus/Index";
const API_TUKIEM = "http://localhost:8080/PgsStatus/TuKiem";
const CCU_HOST = "192.169.1.75";
const CCU_PORT = 2000;

const ketQua = [];
function ghi(ten, dat, chiTiet) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${chiTiet ? "  | " + chiTiet : ""}`);
}

mkdirSync(ARTIFACTS, { recursive: true });

// ------------------------------------------------- 0. deploy khop source chua
//
// Chay TRUOC moi phep khac va dung han neu lech. App phuc vu tu thu muc deploy,
// khong phai cay ma nguon: sua .cs ma quen chep DLL thi moi con so do duoc deu
// la cua ban cu, va nguoi doc se di sua nham cho hang gio.
const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
let hSource, hDeploy;
try {
  hSource = bam(DLL_SOURCE);
  hDeploy = bam(DLL_DEPLOY);
} catch (e) {
  ghi("deploy_khop_source", false, "khong doc duoc DLL: " + e.message);
  console.log("\nCHUA DEPLOY — dung lai.");
  process.exit(1);
}
if (hSource !== hDeploy) {
  ghi("deploy_khop_source", false, `source ${hSource.slice(0, 12)} != deploy ${hDeploy.slice(0, 12)}`);
  console.log("\nCHUA DEPLOY — build roi chep bin\\*.dll va Web.config sang ban deploy, roi chay lai.");
  process.exit(1);
}
ghi("deploy_khop_source", true, hSource.slice(0, 12));

// ------------------------------------------------------ doc CCU doc lap voi C#
//
// Cai dat giao thuc rieng, khong dung chung dong ma nao voi ban C#. Neu hai ben
// cung sai mot kieu thi phep so sanh vo nghia.
function crc(than) {
  let c = 0;
  for (const ch of Buffer.from(than, "ascii")) c ^= ch;
  return c.toString(16).toUpperCase().padStart(2, "0");
}

function docCcu(giay) {
  return new Promise((giaiQuyet, tuChoi) => {
    const s = connect(CCU_PORT, CCU_HOST);
    const goi = [];
    let dem = "";
    let hen = null;
    const LIVE = "$CCU,01,LIVE*" + crc("CCU,01,LIVE") + "#";

    const dong = () => {
      clearInterval(hen);
      s.destroy();
      giaiQuyet(goi);
    };
    s.setTimeout(giay * 1000 + 5000, () => { s.destroy(); tuChoi(new Error("het gio noi CCU")); });
    s.on("error", (e) => { clearInterval(hen); tuChoi(e); });
    s.on("connect", () => {
      s.write(LIVE);
      hen = setInterval(() => s.write(LIVE), 4000);
      setTimeout(dong, giay * 1000);
    });
    s.on("data", (d) => {
      dem += d.toString("ascii");
      for (;;) {
        const i = dem.indexOf("$");
        const j = i >= 0 ? dem.indexOf("#", i + 1) : -1;
        if (i < 0 || j < 0) break;
        goi.push(dem.slice(i, j + 1));
        dem = dem.slice(j + 1);
      }
    });
  });
}

// Giai mot goi $CCU,02 thanh so dem. Tra null neu khong phai goi du lieu.
function giaiGoi(k) {
  if (!k.startsWith("$") || !k.endsWith("#")) return null;
  const sao = k.lastIndexOf("*");
  if (sao < 1) return null;
  const than = k.slice(1, sao);
  if (crc(than).toUpperCase() !== k.slice(sao + 1, -1).toUpperCase()) return null;
  const p = than.split(",");
  if (p[1] !== "02" || p.length < 7) return null;
  const dem = { trong: 0, coXe: 0, loi: 0, khongLap: 0 };
  for (const ch of p[5] + p[6]) {
    if (ch === "0") dem.trong++;
    else if (ch === "1") dem.coXe++;
    else if (ch === "2") dem.loi++;
    else if (ch === "3") dem.khongLap++;
  }
  return { zcuId: Number(p[3]), ketNoi: p[4] === "1", ...dem };
}

// --------------------------------------------------------------------- do that
let api, tuCcu;
try {
  // Doc CCU 12 giay: khe giua hai goi cung mot ZCU do duoc 2,50 giay, nen 12
  // giay du bat tron it nhat 4 vong cua ca 5 ZCU.
  const goi = await docCcu(12);
  writeFileSync(join(ARTIFACTS, "ccu-goi.txt"), goi.join("\n"), "utf8");

  // Giu goi MOI NHAT cua tung ZCU, giong cach ban C# lam.
  tuCcu = new Map();
  let soGoiDuLieu = 0;
  for (const k of goi) {
    const g = giaiGoi(k);
    if (!g) continue;
    soGoiDuLieu++;
    tuCcu.set(g.zcuId, g);
  }
  if (soGoiDuLieu === 0) throw new Error("CCU khong tra goi du lieu nao");

  api = await (await fetch(API, { cache: "no-store" })).json();
} catch (e) {
  ghi("do_that", false, e.message);
  ket();
}

// --------------------------------------- 1. so cua nhom dang ket noi khop CCU
{
  const nguong = api.nguong_qua_han_giay ?? 10;
  const song = [...tuCcu.values()].filter((z) => z.ketNoi);
  const mong = {
    khong_co_xe: song.reduce((a, z) => a + z.trong, 0),
    co_xe: song.reduce((a, z) => a + z.coXe, 0),
    loi: song.reduce((a, z) => a + z.loi, 0),
    khong_lap: song.reduce((a, z) => a + z.khongLap, 0),
  };
  const that = api.dang_ket_noi ?? {};
  const khop = ["khong_co_xe", "co_xe", "loi", "khong_lap"].every((k) => that[k] === mong[k]);
  ghi("so_dang_ket_noi_khop_ccu", khop,
      `api {trong:${that.khong_co_xe} xe:${that.co_xe} loi:${that.loi} chuaLap:${that.khong_lap}}` +
      ` vs ccu {trong:${mong.khong_co_xe} xe:${mong.co_xe} loi:${mong.loi} chuaLap:${mong.khong_lap}}` +
      ` (nguong qua han ${nguong}s)`);
}

// ------------------------------------------ 2. nhom dong bang tach rieng, co tuoi
{
  const db = api.dong_bang ?? {};
  const dk = api.dang_ket_noi ?? {};
  const zcuMatKetNoi = (api.zcus ?? []).filter((z) => !z.dang_ket_noi);

  // Dieu kien 1: moi ZCU mat ket noi phai nam o nhom dong bang, khong duoc
  // cong vao nhom song.
  const idDongBang = new Set(db.zcu_ids ?? []);
  const idDangNoi = new Set(dk.zcu_ids ?? []);
  const dungCho = zcuMatKetNoi.every((z) => idDongBang.has(z.zcu_id) && !idDangNoi.has(z.zcu_id));

  // Dieu kien 2: phai co tuoi ket noi rieng. Neu tuoi tinh tu goi cuoi thi moi
  // ZCU deu ~0 vi CCU van day goi cho ZCU da chet.
  const coTuoi = zcuMatKetNoi.every((z) => z.tuoi_ket_noi_s === null || z.tuoi_ket_noi_s > 0);

  // Dieu kien 3: khong duoc ton tai truong tong gop nao o cap goc.
  const khongGop = api.co_xe === undefined && api.khong_co_xe === undefined;

  ghi("nhom_dong_bang_tach_rieng", dungCho && coTuoi && khongGop,
      `${zcuMatKetNoi.length} zcu mat ket noi, dong_bang=[${(db.zcu_ids ?? []).join(",")}]` +
      `, dang_ket_noi=[${(dk.zcu_ids ?? []).join(",")}]` +
      `, tuoi rieng ${coTuoi ? "co" : "KHONG"}, so gop ${khongGop ? "da bo" : "VAN CON"}`);
}

// ----------------------------------------------- 3. zcu qua han bi danh dau
{
  const nguong = api.nguong_qua_han_giay;
  const zcus = api.zcus ?? [];
  const coTruong = zcus.length > 0 && zcus.every((z) => typeof z.qua_han === "boolean");
  const dungLuat = zcus.every((z) => z.qua_han === (z.tuoi_goi_s > nguong));
  // ZCU qua han khong duoc nam trong nhom song du X3 = 1.
  const idDangNoi = new Set((api.dang_ket_noi?.zcu_ids) ?? []);
  const khongLotVaoSong = zcus.filter((z) => z.qua_han).every((z) => !idDangNoi.has(z.zcu_id));
  ghi("zcu_qua_han_bi_danh_dau", coTruong && dungLuat && khongLotVaoSong,
      `nguong ${nguong}s, ${zcus.length} zcu, qua han ${zcus.filter((z) => z.qua_han).length}`);
}

// ------------------------------------------------------- 4. khung hong bi loai
//
// Nap thang vao bo giai ma C# qua cong tu kiem. Day la phep kiem DUY NHAT cham
// duoc vao ma C#: neu chi kiem bang ham CRC cua chinh script nay thi mot bo
// giai ma bo qua CRC hoan toan van "dat".
{
  const than = "CCU,02,5,0,1," + "0".repeat(32) + "," + "1".repeat(32);
  const tot = "$" + than + "*" + crc(than) + "#";
  const saiCrc = "$" + than + "*" + (crc(than) === "00" ? "01" : "00") + "#";
  const nhipSong = "$CCU,01,OK*" + crc("CCU,01,OK") + "#";
  const cut = "$CCU,02,5,0,1*" + crc("CCU,02,5,0,1") + "#";
  // ZCU id hai ky tu + X3 = 0. Hai thu nay khong quan sat duoc ngoai hien truong
  // luc nay (bai chi co 5 ZCU va ca 5 dang ket noi), nen phai nap thang vao.
  const than0 = "CCU,02,16,12,0," + "2".repeat(32) + "," + "3".repeat(32);
  const matKetNoi = "$" + than0 + "*" + crc(than0) + "#";

  try {
    const r = await fetch(API_TUKIEM, {
      method: "POST",
      headers: { "Content-Type": "text/plain" },
      body: [tot, saiCrc, nhipSong, cut, matKetNoi].join("\n"),
    });
    const kq = (await r.json()).ket_qua ?? [];
    const [a, b, c, d, e] = kq;
    const dat =
      kq.length === 5 &&
      a?.hop_le === true && a.co_xe === 32 && a.khong_co_xe === 32 && a.dang_ket_noi === true &&
      b?.hop_le === false && b.ly_do === "SaiCrc" &&
      c?.hop_le === false && c.ly_do === "NhipSong" &&
      d?.hop_le === false && d.ly_do === "ThieuTruong" &&
      // Doc dung X3 = 0 va ID hai ky tu (12), dem dung 32 loi + 32 khong lap.
      e?.hop_le === true && e.dang_ket_noi === false && e.zcu_id === 12 &&
      e.loi === 32 && e.khong_lap === 32;
    ghi("khung_hong_bi_loai", dat,
        kq.map((x) => (x.hop_le ? "nhan" : "loai:" + x.ly_do)).join(" / ") +
        ` | X3=0 doc thanh dang_ket_noi=${e?.dang_ket_noi}, zcu_id=${e?.zcu_id}`);
  } catch (e) {
    ghi("khung_hong_bi_loai", false, "khong goi duoc /PgsStatus/TuKiem: " + e.message);
  }
}

// --------------------------------------------- 5+6. socket: het ZCU, mot CCU
{
  const bangTcp = () => {
    try {
      return execFileSync("powershell.exe", ["-NoProfile", "-Command",
        "Get-NetTCPConnection -RemotePort 2000 -ErrorAction SilentlyContinue | " +
        "Select-Object RemoteAddress,State | Format-Table -AutoSize | Out-String -Width 200"],
        { encoding: "utf8", timeout: 30000 });
    } catch (e) {
      return "LOI: " + e.message;
    }
  };
  const locZcu = (b) => b.split(/\r?\n/).filter((d) => /192\.169\.1\.7[0-4]\b/.test(d));
  const dangSong = (ds) => ds.filter((d) => /Established|SynSent|SynReceived/i.test(d));

  // Do HAI LAN cach nhau, vi mot lan dem khong phan biet duoc hai truong hop
  // khac han nhau:
  //   - tan du FinWait2 cua ma cu dang tieu dan  -> dung, se ve 0
  //   - ma van dang mo socket moi lien tuc       -> sai, khong bao gio ve 0
  // Do duoc ngay 24/09 sau khi deploy: 21 -> 11 -> 0 trong khoang hai phut.
  const bang1 = bangTcp();
  const zcu1 = locZcu(bang1);
  execFileSync("powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 20"],
               { timeout: 40000 });
  const bang = bangTcp();
  const zcu2 = locZcu(bang);

  writeFileSync(join(ARTIFACTS, "tcp-2000.txt"),
    "--- lan 1 ---\n" + bang1 + "\n--- lan 2 (sau 20s) ---\n" + bang, "utf8");

  // Dat khi: khong con socket SONG nao toi ZCU, va tong khong tang.
  const dongZcu = dangSong(zcu2);
  const khongTang = zcu2.length <= zcu1.length;

  // Chi xet bang socket la chua du: chi can de trong pgs:hosts la Initialize()
  // thoat som va khong mo socket nao, trong khi hai file cu van con nguyen
  // trong luong chay. Nen xet them dau vet tinh.
  const csproj = readFileSync(CSPROJ, "utf8");
  const conTrongCsproj = csproj.includes("PgsFrame.cs") || csproj.includes("PgsConnection.cs");
  const conFile = existsSync(join(DIR_PGS, "PgsFrame.cs")) || existsSync(join(DIR_PGS, "PgsConnection.cs"));

  ghi("khong_con_socket_zcu",
      dongZcu.length === 0 && khongTang && !conTrongCsproj && !conFile,
      `socket song toi .70-.74: ${dongZcu.length}` +
      `, tong ${zcu1.length} -> ${zcu2.length} (${khongTang ? "khong tang" : "TANG — ma van mo moi"})` +
      `, csproj ${conTrongCsproj ? "VAN CON" : "da go"}` +
      `, file ${conFile ? "VAN CON" : "da xoa"}`);

  // Script nay cung dang giu mot socket toi CCU luc do, nhung no da dong truoc
  // khi chay phep kiem nay. Chap nhan 1-2 de tru socket dang dong dang do.
  const dongCcu = bang.split(/\r?\n/).filter((d) => /192\.169\.1\.75\b/.test(d));
  const established = dongCcu.filter((d) => /Established/i.test(d));
  ghi("mot_socket_ccu", established.length >= 1 && established.length <= 2,
      `toi .75: ${dongCcu.length} muc, Established ${established.length}`);
}

ket();

function ket() {
  const rot = ketQua.filter((k) => !k.dat);
  console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
  process.exit(rot.length === 0 ? 0 : 1);
}
