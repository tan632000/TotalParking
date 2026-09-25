// Kiem chung: trang Zone doc so lieu that, khong con du lieu bia.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\zonedetail-doc-that\verify-zonedetail.mjs"
//
// Truoc thay doi nay trang Zone gia tu tren xuong duoi: 8 dong gan cung cho
// thong ke zone, 18 khoi bia ten "Block A-01".."Block E-03" (bai that co 118
// khoi), va 9 bien so xe khong ton tai gan cho cac o do. Khong con so nao trong
// do la 0 hay dau gach — tat ca deu la nhung con so trong rat hop ly, nen mat
// thuong khong phan biet duoc.
//
// Phep kiem dat gia nhat la `loi_thi_xoa_so`: no phai chay SAU khi so that da
// duoc ve len man hinh (doi chung am). Mot phep kiem chua bao gio do khong
// chung minh duoc gi.

import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "file:///C:/Users/Admin/source/repos/TotalParking/.claude/skills/chrome-devtools/scripts/node_modules/puppeteer/lib/esm/puppeteer/puppeteer.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const SRC = join(GOC, "TotalParking", "Views", "Home", "ZoneDetail.cshtml");
const SRC_DEPLOY = join(DEPLOY, "Views", "Home", "ZoneDetail.cshtml");
const ARTIFACTS = join(__dirname, "artifacts");
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const CHU = "http://localhost:8080";
const ZONE = 2;                       // zone co xe that lam mau
const TRANG = `${CHU}/Home/Zones/${ZONE}`;
const API_KHOI = "/Monitor/BlockMap";
const API_O = "/SlotStatus/Index";

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString(), zone: ZONE };
function ghi(ten, dat, ct) {
  ketQua.push({ ten, dat });
  console.log(`  ${dat ? "PASS" : "FAIL"}  ${ten}${ct ? "  | " + ct : ""}`);
}
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));
mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "zonedetail.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

// Doc lai toan bo luoi khoi tu DOM, khong doc mot o le.
const DOC_KHOI = `(() => {
  const ra = [];
  document.querySelectorAll('#blocksGrid button').forEach(b => {
    const so = (b.innerText.match(/^Khối (\\d+)/) || [])[1];
    const lay = (nhan) => {
      const d = [...b.querySelectorAll('div.flex.justify-between')]
        .find(x => x.firstElementChild && x.firstElementChild.textContent.trim() === nhan);
      return d ? d.lastElementChild.textContent.trim() : null;
    };
    ra.push({
      block_no: so ? Number(so) : null,
      slots: lay('Sức chứa'), occupied: lay('Có xe'),
      trong: lay('Còn trống'), fresh: lay('Ô đọc mới'),
      online: b.innerText.includes('Đang đọc được'),
    });
  });
  return {
    khoi: ra,
    tom_tat: (document.getElementById('zoneTomTat')||{}).textContent,
    phan_tram: (document.getElementById('zonePhanTram')||{}).textContent,
    thanh: (document.getElementById('zoneThanh')||{style:{}}).style.width,
  };
})()`;

// -------------------------------------------- 0. deploy khop source
{
  const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
  let a, b;
  try { a = bam(SRC); b = bam(SRC_DEPLOY); }
  catch (e) { ghi("deploy_khop_source", false, e.message); luu(); process.exit(1); }
  if (a !== b) {
    ghi("deploy_khop_source", false, `${a.slice(0, 12)} != ${b.slice(0, 12)}`);
    console.log("\nCHUA DEPLOY — chep ZoneDetail.cshtml sang ban deploy roi chay lai.");
    luu(); process.exit(1);
  }
  ghi("deploy_khop_source", true, a.slice(0, 12));
}

// -------------------------------------------- 1. khong con du lieu gia
{
  const src = readFileSync(SRC, "utf8");
  // Cac moc nay la du lieu bia CU, khong phai chuoi ma JS sinh ra luc chay.
  const cam = ["Block A-01", "51G-12345", "blocks.Add",
               "@occupancyRate", "@totalSlots", "@occupiedSlots",
               "@foreach (var block in blocks)"];
  const con = cam.filter((c) => src.includes(c));
  // Bien so gia deu theo khuon "51X-12345"; quet ca file de khong bo sot cai la.
  const bienSoGia = src.match(/\b\d{2}[A-Z]-\d{3}\.?\d{2}\b/g) || [];
  soLieu.tinh = { moc_con_lai: con, bien_so_gia: bienSoGia };
  ghi("khong_con_du_lieu_gia", con.length === 0 && bienSoGia.length === 0,
      con.length || bienSoGia.length
        ? `con ${JSON.stringify(con)} + ${bienSoGia.length} bien so`
        : "sach ca 7 moc va khong con bien so bia");
}

// ------------------- 1b. HTML may chu tra ve khong mang so lieu san
//
// Phep kiem o tren chi quet vai chuoi literal da bi xoa nen no bao dam xanh.
// Phep kiem nay do thu khac: HTML TRUOC khi JS chay khong duoc chua san bat ky
// con so hay den trang thai nao. No tung DO that: cac gia tri "9 (75%)",
// "3,820", "1240h", "12 pallet" va hai cham xanh PLC/HMI deu nam trong HTML
// phuc vu, va vi JS ghi de chung khi mo modal nen mat thuong khong thay.
{
  const html = await (await fetch(TRANG, { cache: "no-store" })).text();
  // Bo <script> va <style>: do la ma nguon, khong phai thu nguoi dung doc.
  const than = html.replace(/<script[\s\S]*?<\/script>/gi, "")
                   .replace(/<style[\s\S]*?<\/style>/gi, "");

  // (a) So lieu nam san trong the: "9 (75%)", "3,820", "1240h", "12 pallet"
  const soSan = (than.match(/>\s*\d[\d.,]*\s*(?:\(\d+%\)|pallet|h|ô|%)\s*</g) || [])
                  .map((x) => x.trim());
  // (b) Den trang thai to mau san: mot cai den chi duoc mau sau khi co du lieu.
  const denSan = (than.match(/rounded-full[^>]*background-color:\s*rgb\(34, 197, 94\)/g) || []);

  soLieu.html_phuc_vu = { so_san: soSan, den_san: denSan.length };
  ghi("html_may_chu_khong_mang_so", soSan.length === 0 && denSan.length === 0,
      soSan.length || denSan.length
        ? `con ${soSan.length} so san ${JSON.stringify(soSan.slice(0, 4))}, ` +
          `${denSan.length} den to mau san`
        : "khong con so lieu hay den trang thai nao nam san trong HTML");
}

let browser;
try {
  browser = await puppeteer.launch({
    headless: "new", executablePath: CHROME,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });

  const api = await (await fetch(CHU + API_KHOI, { cache: "no-store" })).json();
  const khoiApi = (api.blocks || []).filter((b) => b.zone_id === ZONE)
                                    .sort((a, b) => a.block_no - b.block_no);

  // ----------------------------------------- 2. luoi khoi khop API
  let mauBlockNo = null;
  let oCoXeToan = 0;   // so o co xe tren TOAN bai, dung lam doi chung am
  {
    const p = await browser.newPage();
    await p.setViewport({ width: 1700, height: 1100 });
    await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await p.waitForFunction(`document.querySelectorAll('#blocksGrid button').length > 0`,
                            { timeout: 20000 }).catch(() => {});
    await ngu(800);
    const d = await p.evaluate(DOC_KHOI);
    await p.screenshot({ path: join(ARTIFACTS, "zone.png") }).catch(() => {});
    await p.close();

    // Doi chieu TUNG khoi, khong chi dem so luong.
    const sai = [];
    if (d.khoi.length !== khoiApi.length)
      sai.push(`so khoi ${d.khoi.length} != API ${khoiApi.length}`);
    for (const a of khoiApi) {
      const t = d.khoi.find((x) => x.block_no === a.block_no);
      if (!t) { sai.push(`thieu khoi ${a.block_no}`); continue; }
      if (t.slots !== `${a.slots} ô`) sai.push(`khoi ${a.block_no}: suc chua "${t.slots}" != ${a.slots}`);
      if (t.occupied !== String(a.occupied)) sai.push(`khoi ${a.block_no}: co xe "${t.occupied}" != ${a.occupied}`);
      if (t.trong !== String(a.slots - a.occupied)) sai.push(`khoi ${a.block_no}: con trong "${t.trong}"`);
      if (t.fresh !== `${a.fresh}/${a.slots}`) sai.push(`khoi ${a.block_no}: doc moi "${t.fresh}"`);
      if (t.online !== !!a.online) sai.push(`khoi ${a.block_no}: nhan ket noi lech`);
    }
    // Tom tat zone phai la tong cong cua API, khong phai so gan cung.
    const tongO = khoiApi.reduce((s, b) => s + b.slots, 0);
    const tongXe = khoiApi.reduce((s, b) => s + b.occupied, 0);
    const pct = tongO > 0 ? Math.round(tongXe / tongO * 100) : 0;
    const mongTomTat = `${khoiApi.length} khối · ${tongXe}/${tongO} ô có xe · ${pct}%`;
    if (!(d.tom_tat || "").startsWith(mongTomTat))
      sai.push(`tom tat "${d.tom_tat}" != "${mongTomTat}"`);

    // Chon khoi mau cho phep kiem modal theo /SlotStatus, KHONG theo BlockMap.
    // Hai API dem "co xe" theo hai luat khac nhau (xem ghi chu cuoi file), va
    // neu chon theo BlockMap thi de roi vao khoi rong — luc do phep kiem modal
    // khong bao gio chay qua nhanh "o co xe" va se xanh ma khong chung minh gi.
    const oToan = await (await fetch(CHU + API_O, { cache: "no-store" })).json();
    const oCoXe = (oToan.slots || []).filter((s) => s.zone_id === ZONE &&
                                                    (s.occupied || s.suspect));
    oCoXeToan = oCoXe.length;
    mauBlockNo = oCoXe.length ? oCoXe[0].block_no
                              : (khoiApi[0] || {}).block_no;
    soLieu.luoi_khoi = { so_khoi_api: khoiApi.length, so_khoi_dom: d.khoi.length,
                         tom_tat: d.tom_tat, phan_tram: d.phan_tram, sai,
                         mau_block: mauBlockNo };
    ghi("luoi_khoi_khop_api", sai.length === 0 && khoiApi.length > 0,
        sai.length ? sai.slice(0, 3).join("; ")
                   : `${khoiApi.length} khoi khop tung con so + tom tat zone`);
  }

  // ----------------------------------------- 3. o do trong modal khop API
  {
    const p = await browser.newPage();
    await p.setViewport({ width: 1700, height: 1100 });
    await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await p.waitForFunction(`document.querySelectorAll('#blocksGrid button').length > 0`,
                            { timeout: 20000 });
    // Bam nut THAT trong DOM, khong goi ham qua window: nut phai thuc su noi
    // duoc vao ham thi nguoi dung moi mo duoc modal.
    const bamDuoc = await p.evaluate((n) => {
      const b = [...document.querySelectorAll('#blocksGrid button')]
        .find((x) => x.innerText.startsWith("Khối " + n + "\n"));
      if (!b) return false;
      b.click(); return true;
    }, mauBlockNo);
    await ngu(2000);

    const oApi = await (await fetch(`${CHU}${API_O}?block=${mauBlockNo}`,
                                    { cache: "no-store" })).json();
    const dom = await p.evaluate(() => ({
      ten: (document.getElementById("modalBlockName") || {}).innerText,
      dem: (document.getElementById("modalPalletCount") || {}).innerText,
      the: [...document.querySelectorAll("#modalPalletsGrid > div")]
             .map((d) => d.innerText.replace(/\n/g, " | ")),
    }));
    await p.screenshot({ path: join(ARTIFACTS, "zone-modal.png") }).catch(() => {});
    await p.close();

    const sai = [];
    if (!bamDuoc) sai.push(`khong tim thay nut khoi ${mauBlockNo}`);
    if (dom.ten !== `Khối ${mauBlockNo}`) sai.push(`ten modal "${dom.ten}"`);
    if (dom.the.length !== (oApi.slots || []).length)
      sai.push(`ve ${dom.the.length} o, API co ${(oApi.slots || []).length}`);
    if (!(dom.dem || "").startsWith(`${oApi.occupied}/${oApi.total_slots}`))
      sai.push(`dem o "${dom.dem}" != ${oApi.occupied}/${oApi.total_slots}`);
    // Tung o: co xe thi the phai mang dung ma dinh danh API tra ve.
    let daDoNhanhCoXe = 0;
    (oApi.slots || []).forEach((o, i) => {
      const t = dom.the[i] || "";
      if (!t.includes(`Ô ${o.slot < 10 ? "0" + o.slot : o.slot}`))
        sai.push(`o ${o.slot}: sai thu tu`);
      if (!t.includes(o.register)) sai.push(`o ${o.slot}: thieu thanh ghi ${o.register}`);
      if (o.suspect) {
        daDoNhanhCoXe++;   // the la cung la mot o "co gi do", tinh vao doi chung
        if (!t.includes("Thẻ lạ")) sai.push(`o ${o.slot}: the la khong duoc danh dau`);
      } else if (o.occupied) {
        daDoNhanhCoXe++;
        const chinh = o.plate || o.card_no || o.card_code;
        if (!t.includes(chinh)) sai.push(`o ${o.slot}: thieu "${chinh}"`);
      } else if (!t.includes("trống")) sai.push(`o ${o.slot}: khong bao trong`);
    });
    // Doi chung am: bai co o co xe ma phep kiem khong cham vao o nao co xe thi
    // ket qua xanh khong noi len dieu gi. Bat phai do it nhat mot o.
    if (oCoXeToan > 0 && daDoNhanhCoXe === 0)
      sai.push(`bai co ${oCoXeToan} o co xe nhung khoi mau khong co o nao`);

    soLieu.modal = { block: mauBlockNo, dem: dom.dem, so_the: dom.the.length,
                     o_co_xe_da_do: daDoNhanhCoXe,
                     co_xe: dom.the.filter((t) => !t.includes("trống")), sai };
    ghi("o_do_khop_api", sai.length === 0 && dom.the.length > 0,
        sai.length ? sai.slice(0, 3).join("; ")
                   : `khoi ${mauBlockNo}: ${dom.the.length} o khop tung ma the voi API ` +
                     `(${daDoNhanhCoXe} o co xe)`);
  }

  // ----------------------------------------- 3b. thu tu uu tien plate
  //
  // Chi 128/613 the co bien so, va du lieu hien truong luc viet script khong co
  // the nao co bien so — nen neu chi do du lieu that thi nhanh `plate` KHONG BAO
  // GIO chay, va mot dau xanh o day khong chung minh gi ve ly do chinh cua thay
  // doi phia may chu.
  //
  // Quan trong: phep kiem nay KHONG duoc chep lai bieu thuc `plate || card_no`
  // cua view. Neu chep, doi thu tu thanh `card_no || plate` thi ca hai cung doi
  // va phep kiem van xanh. Thay vao do no khang dinh mot dieu doc lap: khi co CA
  // HAI, thu duoc in DAM phai la bien so, con so the xuong dong phu.
  {
    const p = await browser.newPage();
    await p.setViewport({ width: 1700, height: 1100 });
    await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await p.waitForFunction(`document.querySelectorAll('#blocksGrid button').length > 0`,
                            { timeout: 20000 });

    const BIEN = "99Z-99999", SO_THE = "KIEMTHU.A", SO_THE_B = "KIEMTHU.B";
    const gia = {
      total_slots: 2, occupied: 2, suspect: 0, free: 0,
      slots: [
        { zone_id: ZONE, block_no: mauBlockNo, slot: 1, register: "D400",
          card_code: "deadbeef", card_known: true, plate: BIEN, card_no: SO_THE,
          vehicle_name: "Xe kiem thu", suspect: false, occupied: true,
          read_at: "00:00:00" },
        { zone_id: ZONE, block_no: mauBlockNo, slot: 2, register: "D202",
          card_code: "cafebabe", card_known: true, plate: null, card_no: SO_THE_B,
          vehicle_name: "Xe kiem thu B", suspect: false, occupied: true,
          read_at: "00:00:01" },
      ],
    };
    await p.setRequestInterception(true);
    p.on("request", (r) => {
      // Khop "/SlotStatus" chu KHONG phai API_O: Url.Action bo bot ten action
      // mac dinh nen trang goi "/SlotStatus?block=N", trong khi API_O mang
      // "/SlotStatus/Index". Chan sai duong dan thi trang van doc du lieu that
      // va phep kiem nay se do vi ly do khong lien quan gi den thu no do.
      if (/\/SlotStatus(\/|\?|$)/i.test(r.url())) {
        r.respond({ status: 200, contentType: "application/json",
                    body: JSON.stringify(gia) });
      } else r.continue();
    });

    await p.evaluate((n) => {
      const b = [...document.querySelectorAll("#blocksGrid button")]
        .find((x) => x.innerText.startsWith("Khối " + n + "\n"));
      if (b) b.click();
    }, mauBlockNo);
    await ngu(2000);

    // Doc theo THU BAC hien thi, khong doc ca cum chu: dong dau la dong duoc in
    // dam, cac dong sau la thong tin phu.
    const the = await p.evaluate(() =>
      [...document.querySelectorAll("#modalPalletsGrid > div")].map((d) => {
        const dong = d.innerText.split("\n").map((x) => x.trim()).filter(Boolean);
        return { dau: dong[0], sau: dong.slice(1) };
      }));

    const sai = [];
    if (the.length !== 2) sai.push(`ve ${the.length} o, mong doi 2`);
    if (the[0]) {
      // Ô 01: dong dau la "Ô 01" + thanh ghi, nen tim dong mang ma dinh danh.
      const cum = the[0].sau.length ? the[0].sau : [];
      const iBien = cum.findIndex((x) => x === BIEN);
      const iThe = cum.findIndex((x) => x === SO_THE);
      if (iBien < 0) sai.push(`o co ca hai: khong hien bien so ${BIEN}`);
      else if (iThe >= 0 && iThe < iBien)
        sai.push(`o co ca hai: so the dung TRUOC bien so (sai thu tu uu tien)`);
      if (cum.some((x) => x.includes("chưa có biển số")))
        sai.push("o co bien so van bao 'chua co bien so'");
    }
    if (the[1]) {
      const cum = the[1].sau;
      if (!cum.includes(SO_THE_B)) sai.push(`o khong bien so: khong lui ve ${SO_THE_B}`);
      if (!cum.some((x) => x.includes("chưa có biển số")))
        sai.push("o khong bien so: thieu ghi chu 'chua co bien so'");
    }
    await p.screenshot({ path: join(ARTIFACTS, "zone-modal-plate.png") }).catch(() => {});
    await p.close();

    soLieu.uu_tien_plate = { the, sai };
    ghi("bien_so_dung_truoc_so_the", sai.length === 0 && the.length === 2,
        sai.length ? sai.slice(0, 3).join("; ")
                   : "o co bien so hien bien so truoc; o khong co lui ve so the");
  }

  // ----------------------------------------- 4. loi thi XOA so
  //
  // Tai binh thuong TRUOC de so that len man hinh, ROI moi chan API. Chan ngay
  // tu dau thi khong phan biet duoc "da xoa" voi "chua kip ve".
  {
    const p = await browser.newPage();
    await p.setViewport({ width: 1700, height: 1100 });
    await p.goto(TRANG, { waitUntil: "networkidle2", timeout: 40000 });
    await p.waitForFunction(`document.querySelectorAll('#blocksGrid button').length > 0`,
                            { timeout: 20000 });
    const truoc = await p.evaluate(DOC_KHOI);
    const coSoThat = truoc.khoi.length > 0 && /\d+\/\d+/.test(truoc.tom_tat || "");

    await p.setRequestInterception(true);
    p.on("request", (r) => {
      if (r.url().includes(API_KHOI)) r.respond({ status: 503, body: "loi gia lap" });
      else r.continue();
    });
    // Nhip poll la 15 giay; doi 18 de chac chan qua it nhat mot nhip.
    await ngu(18000);
    const sau = await p.evaluate(DOC_KHOI);
    await p.close();

    const sachSo = sau.khoi.length === 0 &&
                   (sau.tom_tat || "").includes("Mất kết nối") &&
                   (sau.phan_tram || "").trim() === "—" &&
                   (sau.thanh === "0%" || sau.thanh === "0px");
    soLieu.duong_loi = { co_so_that_truoc: coSoThat, khoi_truoc: truoc.khoi.length,
                         khoi_sau: sau.khoi.length, tom_tat_sau: sau.tom_tat,
                         phan_tram_sau: sau.phan_tram, thanh_sau: sau.thanh };
    ghi("loi_thi_xoa_so", coSoThat && sachSo,
        `truoc: ${truoc.khoi.length} khoi co so that (${coSoThat}); ` +
        `sau khi chan: ${sau.khoi.length} khoi, tom tat "${sau.tom_tat}", ` +
        `phan tram "${sau.phan_tram}", thanh "${sau.thanh}"`);
  }
} catch (e) {
  ghi("phep_thu_zone", false, e.message);
} finally {
  if (browser) await browser.close();
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;

// ===================== HAI API DEM "CO XE" KHAC NHAU =====================
//
// /Monitor/BlockMap lay `occupied` tu view v_slot_taken, view nay CO Y loai bo
// mot ma the xuat hien o nhieu hon mot block (mot chiec xe khong the nam hai
// noi). /SlotStatus dem tho theo thanh ghi cua tung block nen khong loai.
//
// Do la lua chon co chu dich cua hai ben, khong phai loi cua trang Zone. Nhung
// hau qua nhin thay duoc: khi du lieu thanh ghi co ma the trung o hai block,
// o luoi khoi se hien "Co xe 0" trong khi mo modal khoi do lai thay "1/5".
//
// Trang thai luc viet script: ma the a0ace420 nam o ca block 72 va 88 (khach
// dang test, ghi thang vao thanh ghi), nen v_slot_taken rong toan bai.
// Viec chon mot dinh nghia chung cho "co xe" la quyet dinh san pham, khong
// phai viec cua script kiem chung nay.
