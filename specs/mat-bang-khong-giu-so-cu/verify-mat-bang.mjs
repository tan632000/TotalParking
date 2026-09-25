// Kiem chung: trang Mat bang khong giu so cu khi khong doc duoc may chu.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\mat-bang-khong-giu-so-cu\verify-mat-bang.mjs"
//
// Phep kiem quan trong nhat la `loi_thi_xoa_so`. Truoc thay doi nay, nhanh catch
// chi doi mot dong chu nho o goc trang, con sau callout van giu nguyen bo so
// demo viet cung trong HTML ("Mat do: 23/34 (68%)") nhu the do la hien trang bai
// xe. So do khong phai 0 hay dau gach — no la nhung con so trong rat hop ly.

import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const SRC = join(GOC, "TotalParking", "Views", "Home", "FloorPlan.cshtml");
const DEPLOY_FP = join(DEPLOY, "Views", "Home", "FloorPlan.cshtml");
const ARTIFACTS = join(__dirname, "artifacts");
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const TRANG = "http://localhost:8080/Home/FloorPlan";
const API = "/Monitor/RoutingState";

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString() };
function ghi(ten, dat, ct) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${ct ? "  | " + ct : ""}`);
}
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "mat-bang.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

const DOC = `(() => {
  const out = [];
  document.querySelectorAll('.zone-callout').forEach(c => out.push({
    zone: c.dataset.zone,
    dong: [...c.querySelectorAll('.callout-detail, .callout-density')]
            .map(x => x.textContent.trim())
  }));
  const feed = document.getElementById('routing-feed');
  return { callouts: out, feed: feed ? feed.innerText.trim() : "" };
})()`;

// --------------------------------------------------- 0. deploy khop source
{
  const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
  let a, b;
  try { a = bam(SRC); b = bam(DEPLOY_FP); }
  catch (e) { ghi("deploy_khop_source", false, e.message); process.exit(1); }
  if (a !== b) {
    ghi("deploy_khop_source", false, `${a.slice(0, 12)} != ${b.slice(0, 12)}`);
    console.log("\nCHUA DEPLOY — chep FloorPlan.cshtml sang ban deploy roi chay lai.");
    process.exit(1);
  }
  ghi("deploy_khop_source", true, a.slice(0, 12));
}

// ------------------------------- 1. HTML tinh khong con so demo
{
  const src = readFileSync(SRC, "utf8");
  // Khong cam chuoi "Mat do" — JS that su ghi ra chuoi do. Cam dung DANG
  // "Mat do: <so>/<so> (<so>%)" nam trong HTML tinh.
  const soDemo = src.match(/Mật độ: \d+\/\d+ \(\d+%\)/g) || [];
  const nhanCu = (src.match(/\+ (SUV|SEDAN):/g) || []);
  soLieu.tinh = { so_demo: soDemo, nhan_cu: nhanCu.length };
  ghi("html_tinh_khong_con_so_demo", soDemo.length === 0 && nhanCu.length === 0,
      soDemo.length || nhanCu.length
        ? `con ${soDemo.length} so demo, ${nhanCu.length} nhan SUV/SEDAN`
        : "sach so demo va nhan cu");
}

let browser;
try {
  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  // ------------------------------- 2. duong binh thuong: so khop API
  {
    const p = await browser.newPage();
    await p.setViewport({ width: 1700, height: 1000 });
    await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await p.waitForFunction(
      `![...document.querySelectorAll('.callout-density')].some(e => e.textContent.includes('—'))`,
      { timeout: 20000 }).catch(() => { });
    await ngu(1000);

    const api = await (await fetch("http://localhost:8080" + API, { cache: "no-store" })).json();
    const d = await p.evaluate(DOC);
    await p.screenshot({ path: join(ARTIFACTS, "mat-bang.png") }).catch(() => {});
    await p.close();

    // Doi chieu TUNG zone voi API, khong chi dem so callout.
    const sai = [];
    for (const z of (api.zones || [])) {
      const c = d.callouts.find((x) => String(x.zone) === String(z.zone_id));
      if (!c) { sai.push(`zone ${z.zone_id}: khong co callout`); continue; }
      const mongTrong = "+ Pallet trống: " + z.free_mech;
      const mongMatDo = `Mật độ: ${z.in_use}/${z.total} (${z.used_pct}%)`;
      if (z.total === 0) continue;   // nhanh "chua khai bao block", khong do o day
      if (!c.dong.includes(mongTrong)) sai.push(`zone ${z.zone_id}: thieu "${mongTrong}"`);
      if (!c.dong.includes(mongMatDo)) sai.push(`zone ${z.zone_id}: thieu "${mongMatDo}"`);
    }
    soLieu.binh_thuong = { so_zone_api: (api.zones || []).length, sai, mau: d.callouts[0] };
    ghi("so_khop_api", sai.length === 0 && (api.zones || []).length > 0,
        sai.length ? sai.slice(0, 3).join("; ")
                   : `${(api.zones || []).length} zone khop tung con so voi API`);
  }

  // ------------------------- 3. LOI thi XOA so, khong giu so cu
  //
  // Tai trang BINH THUONG truoc de so that duoc ve len, ROI moi chan API. Neu
  // chan ngay tu dau thi khong phan biet duoc "xoa so" voi "chua kip ve".
  {
    const p2 = await browser.newPage();
    await p2.setViewport({ width: 1700, height: 1000 });
    await p2.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await ngu(3000);
    const truoc = await p2.evaluate(DOC);

    // Co so that tren man hinh chua? Khong co thi phep kiem nay vo nghia.
    const coSoThat = truoc.callouts.some((c) => /\d+\/\d+/.test(c.dong.join(" ")));

    await p2.setRequestInterception(true);
    p2.on("request", (req) => {
      if (req.url().includes(API)) req.respond({ status: 503, body: "loi gia lap" });
      else req.continue();
    });
    await ngu(5000);   // qua vai nhip poll 2 giay
    const sau = await p2.evaluate(DOC);
    await p2.close();

    const conSo = sau.callouts.filter((c) => /\d+\/\d+/.test(c.dong.join(" ")));
    const coBaoLoi = sau.callouts.every((c) => c.dong.join(" ").includes("Mất kết nối"));
    soLieu.duong_loi = { co_so_that_truoc: coSoThat, con_so_sau_khi_loi: conSo.length,
                         mau_sau: sau.callouts[0], feed: sau.feed.slice(0, 60) };
    ghi("loi_thi_xoa_so", coSoThat && conSo.length === 0 && coBaoLoi,
        `truoc khi chan co so that: ${coSoThat}; sau khi chan con ${conSo.length} callout mang so; ` +
        `moi callout bao mat ket noi: ${coBaoLoi}`);
  }
} catch (e) {
  ghi("phep_thu_mat_bang", false, e.message);
} finally {
  if (browser) await browser.close();
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
