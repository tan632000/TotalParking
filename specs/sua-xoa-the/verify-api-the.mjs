// Kiem chung task-01 — API sua / tat / bat the, ba lop chan, nhat ky rieng.
//
// Chay: & "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-api-the.mjs"
//
// Ba phep kiem quan trong nhat:
//   - khong_the_nao_khac_bi_dung: bat loi dinh danh dong bang card_code nhan tu
//     POST. Khong co no, mot cai dat ghi de len the cua khach khac van PASS het.
//   - quet_cu_hon_24h_khong_chan: bat loi bo quen dieu kien thoi gian o lop
//     plc_request — loi khoa vinh vien 13 the that.
//   - khong_o_do_nao_bien_mat: chot chan cho hop dong don dep. plc_slot_state
//     seed san theo khoa (block_id, slot_index); DELETE mot dong la mat vinh vien
//     mot o that khoi vong quet.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const GOC = join(__dirname, "..", "..");
const DEPLOY = "C:\\Users\\Admin\\Documents\\Web\\totalParking";
const DLL_SRC = join(GOC, "TotalParking", "bin", "TotalParking.dll");
const DLL_DEPLOY = join(DEPLOY, "bin", "TotalParking.dll");
const NHAT_KY = join(DEPLOY, "App_Data", "card_admin.log");
const ARTIFACTS = join(__dirname, "artifacts");
const MYSQL = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\mysql.exe";
const WEB = "http://localhost:8080";

// card_no chi 16 ky tu (varchar(16)), va the thu dung `${DAU}-A` lam so the,
// nen tien to phai ngan. Dai qua thi chinh buoc chen du lieu thu ném 1406.
const DAU = "TC" + Date.now().toString(36).slice(-6).toUpperCase();
const NGUOI = "Nguoi Kiem Thu";

const ketQua = [];
const soLieu = { thoi_diem: new Date().toISOString(), dau: DAU, buoc: [] };
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

async function post(duong, truong) {
  const than = new URLSearchParams(truong);
  const r = await fetch(WEB + duong, { method: "POST", body: than });
  let d = null;
  try { d = await r.json(); } catch { d = null; }
  return { status: r.status, d };
}

mkdirSync(ARTIFACTS, { recursive: true });
const luu = () => writeFileSync(join(ARTIFACTS, "api-the.json"),
                                JSON.stringify(soLieu, null, 1), "utf8");

// Van tay TOAN BANG tru the thu. Day la thu duy nhat bat duoc cai dat dinh danh
// dong bang card_code: the thu khong doi nen moi phep kiem khac van PASS, trong
// khi mot the khach that da bi ghi de.
const VAN_TAY = () => mot(
  `SELECT CONCAT(COUNT(*), ':', COALESCE(MD5(GROUP_CONCAT(
     card_id, '|', card_code, '|', card_no, '|', COALESCE(plate,''), '|',
     COALESCE(customer_name,''), '|', is_active ORDER BY card_id SEPARATOR ';')), 'rong'))
   FROM parking_card WHERE source_label NOT LIKE '${DAU}%'`);

const demODo = () => Number(mot("SELECT COUNT(*) FROM plc_slot_state"));
const demDongNhatKy = () => {
  try { return readFileSync(NHAT_KY, "utf8").split(/\r?\n/).filter((d) => d.length).length; }
  catch { return 0; }
};

// --------------------------------------------------- 0. deploy khop source
{
  const bam = (p) => createHash("sha256").update(readFileSync(p)).digest("hex");
  let ha, hb;
  try { ha = bam(DLL_SRC); hb = bam(DLL_DEPLOY); }
  catch (e) { ghi("deploy_khop_source", false, e.message); process.exit(1); }
  if (ha !== hb) {
    ghi("deploy_khop_source", false, `${ha.slice(0, 12)} != ${hb.slice(0, 12)}`);
    console.log("\nCHUA DEPLOY — chep bin\\ sang ban deploy roi chay lai.");
    process.exit(1);
  }
  ghi("deploy_khop_source", true, ha.slice(0, 12));
}

const soTheTruoc = Number(mot("SELECT COUNT(*) FROM parking_card"));
const soODoTruoc = demODo();
const nhatKyTruoc = demDongNhatKy();
const vanTayDau = VAN_TAY();
soLieu.truoc = { so_the: soTheTruoc, so_o_do: soODoTruoc, nhat_ky: nhatKyTruoc };
luu();

let idA = 0, idB = 0, oMuon = null;

try {
  // Hai the thu: A de thao tac, B de ep loi trung so the.
  sql(`INSERT INTO parking_card
        (card_code, card_no, card_type, vehicle_name, customer_type_id, weight_class_id,
         weight_text, plate, customer_name, source_label, is_active, created_at)
       VALUES
        ('AA${Date.now().toString(16).slice(-6).toUpperCase()}', '${DAU}-A', 'OTO', 'Xe thu A',
         1, 0, '1500KG', '30A-00001', 'Khach Thu A', '${DAU}', 1, NOW()),
        ('BB${Date.now().toString(16).slice(-6).toUpperCase()}', '${DAU}-B', 'OTO', 'Xe thu B',
         1, 0, '1500KG', '30A-00002', 'Khach Thu B', '${DAU}', 1, NOW())`);
  idA = Number(mot(`SELECT card_id FROM parking_card WHERE card_no='${DAU}-A'`));
  idB = Number(mot(`SELECT card_id FROM parking_card WHERE card_no='${DAU}-B'`));
  const maA = mot(`SELECT card_code FROM parking_card WHERE card_id=${idA}`);
  soLieu.the_thu = { idA, idB, maA };

  // ------------------------------------------ 1. List co card_id va tra cuu
  {
    const d = await (await fetch(WEB + "/Cards/List", { cache: "no-store" })).json();
    const dong = (d.cards || []).find((c) => c.card_no === `${DAU}-A`);
    const coId = !!dong && dong.card_id === idA;
    const soLoai = (d.loai_khach || []).length;
    const soHang = (d.hang_tai || []).length;
    const loaiThat = Number(mot("SELECT COUNT(*) FROM customer_type"));
    const hangThat = Number(mot("SELECT COUNT(*) FROM weight_class"));
    soLieu.list = { co_card_id: coId, loai_khach: soLoai, hang_tai: soHang };
    ghi("list_co_card_id", coId && soLoai === loaiThat && soHang === hangThat,
        `card_id=${dong ? dong.card_id : "thieu"} (mong ${idA}), ` +
        `loai_khach ${soLoai}/${loaiThat}, hang_tai ${soHang}/${hangThat}`);
  }

  // ------------------------------------------------------- 2. sua luu duoc
  const vanTayTruocSua = VAN_TAY();
  {
    const kq = await post("/Cards/Update", {
      card_id: idA, nguoi: NGUOI, card_no: `${DAU}-A`, card_type: "OTO",
      vehicle_name: "Xe thu A da sua", plate: "51H-99999",
      customer_name: "Khach Thu A sua", weight_text: "1600KG",
      expiry_date: "31/12/2027", customer_type_id: 2, weight_class_id: 1,
    });
    const r = sql(`SELECT plate, customer_name, customer_type_id, weight_class_id,
                          DATE_FORMAT(expiry_date,'%d/%m/%Y')
                     FROM parking_card WHERE card_id=${idA}`)[0];
    const dat = kq.status === 200 && r[0] === "51H-99999" && r[1] === "Khach Thu A sua"
             && r[2] === "2" && r[3] === "1" && r[4] === "31/12/2027";
    soLieu.sua = { http: kq.status, doc_lai: r };
    ghi("sua_luu_duoc", dat, `HTTP ${kq.status}; doc lai: ${r.join(" | ")}`);
  }

  // ----------------------------- 3. khong the nao khac bi dung
  {
    const sau = VAN_TAY();
    soLieu.van_tay = { truoc: vanTayTruocSua, sau };
    ghi("khong_the_nao_khac_bi_dung", vanTayTruocSua === sau,
        vanTayTruocSua === sau
          ? `van tay toan bang (tru the thu) khong doi: ${sau.split(":")[0]} dong`
          : `LECH: ${vanTayTruocSua} -> ${sau}`);
  }

  // ------------------------------------------------ 4. ma the khong doi duoc
  {
    await post("/Cards/Update", {
      card_id: idA, nguoi: NGUOI, card_no: `${DAU}-A`, card_type: "OTO",
      vehicle_name: "Xe thu A", plate: "51H-99999", customer_name: "Khach Thu A sua",
      weight_text: "1600KG", expiry_date: "31/12/2027",
      customer_type_id: 2, weight_class_id: 1,
      card_code: "DEADBEEF",          // <- gui thang, giao dien khong che duoc
    });
    const sauMa = mot(`SELECT card_code FROM parking_card WHERE card_id=${idA}`);
    const vanTay = VAN_TAY();
    soLieu.ma_the = { truoc: maA, sau: sauMa, van_tay_khop: vanTay === vanTayTruocSua };
    ghi("ma_the_khong_doi_duoc", sauMa === maA && vanTay === vanTayTruocSua,
        `ma the ${maA} -> ${sauMa}` +
        (vanTay === vanTayTruocSua ? "" : "; VA van tay toan bang da LECH"));
  }

  // -------------------------------------------------- 5. tat va bat lai duoc
  {
    const tat = await post("/Cards/SetActive", { card_id: idA, bat: 0, nguoi: NGUOI });
    const sauTat = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    const bat = await post("/Cards/SetActive", { card_id: idA, bat: 1, nguoi: NGUOI });
    const sauBat = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    soLieu.tat_bat = { tat: tat.status, sauTat, bat: bat.status, sauBat };
    ghi("tat_va_bat_lai_duoc",
        tat.status === 200 && sauTat === "0" && bat.status === 200 && sauBat === "1",
        `tat HTTP ${tat.status} -> is_active=${sauTat}; bat HTTP ${bat.status} -> ${sauBat}`);
  }

  // ------------------------------------------ 6. chan tat khi trong o do
  //
  // KHONG chen dong moi: plc_slot_state seed san theo khoa (block_id, slot_index).
  // Muon mot o DANG TRONG cua khoi con NHIEU CHO NHAT roi tra ve NULL — o do tinh
  // ngay vao v_slot_taken va chay vao free_capacity cua BlockAllocator, nen phai
  // chon khoi ma -1 khong day no ve 0, va giu cua so ngan.
  {
    const chon = sql(`
      SELECT s.block_id, s.slot_index, b.block_no,
             b.slot_count - (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id=b.block_id) AS con_trong
        FROM plc_slot_state s JOIN block b ON b.block_id = s.block_id
       WHERE s.card_code IS NULL AND b.is_active = 1
       ORDER BY con_trong DESC, s.slot_index LIMIT 1`);
    if (chon.length === 0) throw new Error("khong tim duoc o dang trong de muon");

    oMuon = { block_id: chon[0][0], slot_index: chon[0][1],
              block_no: chon[0][2], con_trong: Number(chon[0][3]) };
    const routingTruoc = Number(mot("SELECT COUNT(*) FROM vehicle_routing WHERE outcome='ROUTED'"));

    // AND card_code IS NULL: vong quet 45 giay ghi de vo dieu kien moi luot.
    // Neu mot xe that vua duoc xep vao dung o nay giua luc chon va luc muon, cau
    // lenh khong co dieu kien se GHI DE MAT ma the that cua chiec xe do.
    sql(`UPDATE plc_slot_state SET card_code='${maA}'
          WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
            AND card_code IS NULL`);
    const muonDuoc = mot(`SELECT COUNT(*) FROM plc_slot_state
                           WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
                             AND card_code='${maA}'`) === "1";
    if (!muonDuoc) throw new Error("khong muon duoc o do: mot xe vua duoc xep vao o nay");
    const kq = await post("/Cards/SetActive", { card_id: idA, bat: 0, nguoi: NGUOI });
    const conBat = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    // AND card_code='<ma thu>': chi tra ve NULL dung gia tri MINH da ghi. Neu
    // vong quet da ghi ma that vao trong cua so do thi cau nay khong dong nao —
    // tot hon la xoa mat mot o dang co xe.
    sql(`UPDATE plc_slot_state SET card_code=NULL
          WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
            AND card_code='${maA}'`);
    oMuon = null;

    const routingSau = Number(mot("SELECT COUNT(*) FROM vehicle_routing WHERE outcome='ROUTED'"));
    soLieu.o_do = { ...chon[0], http: kq.status, routing_trong_cua_so: routingSau - routingTruoc };
    ghi("chan_tat_khi_trong_o_do", kq.status === 409 && conBat === "1",
        `HTTP ${kq.status}, is_active=${conBat}; khoi ${chon[0][2]} con ${chon[0][3]} cho; ` +
        `xep xe trong cua so do: ${routingSau - routingTruoc}`);
  }

  // ----------------------------------- 7. khong o do nao bien mat
  {
    const nay = demODo();
    soLieu.so_o_do_sau_khi_muon = nay;
    ghi("khong_o_do_nao_bien_mat", nay === soODoTruoc,
        `so o trong plc_slot_state: ${soODoTruoc} -> ${nay}`);
  }

  // ------------------------------------------- 8. chan tat khi co phien
  {
    // active_card_id la COT SINH: no = card_id khi status thuoc nhom dang hoat
    // dong (ASSIGNED/ENTERING/PARKING/PARKED/RETRIEVING). Liet ke no trong INSERT
    // se bi tu choi bang loi 3105 — chi can dat dung status.
    sql(`INSERT INTO parking_session (card_id, status, created_at, updated_at)
         VALUES (${idA}, 'PARKED', NOW(3), NOW(3))`);
    const kq = await post("/Cards/SetActive", { card_id: idA, bat: 0, nguoi: NGUOI });
    const conBat = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    sql(`DELETE FROM parking_session WHERE card_id=${idA}`);
    soLieu.phien = { http: kq.status, is_active: conBat, ly_do: kq.d && kq.d.ly_do };
    ghi("chan_tat_khi_co_phien", kq.status === 409 && conBat === "1",
        `HTTP ${kq.status}, is_active=${conBat}, ly_do=${kq.d ? kq.d.ly_do : "-"}`);
  }

  // ------------------- 9+10. chan khi vua quet, KHONG chan khi quet cu
  {
    sql(`INSERT INTO plc_request (block_id, raw_words, card_code, received_at)
         SELECT MIN(block_id), 'test', '${maA}', NOW(3) FROM block`);
    const moi = await post("/Cards/SetActive", { card_id: idA, bat: 0, nguoi: NGUOI });
    const sauMoi = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);

    sql(`UPDATE plc_request SET received_at = NOW(3) - INTERVAL 30 HOUR
          WHERE card_code='${maA}'`);
    const cu = await post("/Cards/SetActive", { card_id: idA, bat: 0, nguoi: NGUOI });
    const sauCu = mot(`SELECT is_active FROM parking_card WHERE card_id=${idA}`);
    await post("/Cards/SetActive", { card_id: idA, bat: 1, nguoi: NGUOI });

    soLieu.quet = { moi: moi.status, sau_moi: sauMoi, cu: cu.status, sau_cu: sauCu };
    ghi("chan_tat_khi_vua_quet_24h", moi.status === 409 && sauMoi === "1",
        `vua quet: HTTP ${moi.status}, is_active=${sauMoi}`);
    ghi("quet_cu_hon_24h_khong_chan", cu.status === 200 && sauCu === "0",
        `quet 30h truoc: HTTP ${cu.status}, is_active=${sauCu} (0 = tat duoc, dung)`);
  }

  // ------------------------------- 11. loi CSDL thanh cau doc duoc
  {
    const chung = { nguoi: NGUOI, card_type: "OTO", vehicle_name: "X", plate: "30A-1",
                    customer_name: "Y", weight_text: "1000KG", expiry_date: "" };
    const tho = /Duplicate entry|Data too long|foreign key|Cannot add or update/i;

    const trung = await post("/Cards/Update",
      { ...chung, card_id: idA, card_no: `${DAU}-B`, customer_type_id: 1, weight_class_id: 0 });
    const idLa = await post("/Cards/Update",
      { ...chung, card_id: idA, card_no: `${DAU}-A`, customer_type_id: 99, weight_class_id: 0 });
    const daiQua = await post("/Cards/Update",
      { ...chung, card_id: idA, card_no: `${DAU}-A`, plate: "X".repeat(40),
        customer_type_id: 1, weight_class_id: 0 });

    // Phan biet HAI tang: 400 = lop kiem truoc o controller, 409 = anh xa loi
    // CSDL. Chi doi "khac 200" thi mot nhanh DichLoi viet sai van PASS nho lop
    // kiem truoc chan ho.
    const ok = (k, maMongDoi) =>
      k.status === maMongDoi && k.d && k.d.error && !tho.test(k.d.error);
    soLieu.loi = {
      trung: { http: trung.status, error: trung.d && trung.d.error },
      id_la: { http: idLa.status, error: idLa.d && idLa.d.error },
      dai_qua: { http: daiQua.status, error: daiQua.d && daiQua.d.error },
    };
    // trung so the va id tra cuu la di xuong CSDL that (409); chuoi qua dai bi
    // lop kiem truoc chan (400) — do la co y, va probe khang dinh dung tung tang.
    ghi("loi_csdl_thanh_cau_doc_duoc", ok(trung, 409) && ok(idLa, 409) && ok(daiQua, 400),
        `trung=${trung.status} "${(trung.d || {}).error}"; ` +
        `id la=${idLa.status}; qua dai=${daiQua.status}`);
  }

  // ------------------- 12. POST thieu truong KHONG xoa du lieu cu
  //
  // Goi() tra null cho ca "gui len rong" lan "khong co trong form", va cau
  // UPDATE luon dat du 9 cot. Neu khong phan biet hai truong hop do, mot POST
  // thieu vai khoa se xoa trang ho so DEC cua the khach — du lieu khong nhap
  // lai duoc tu trong ung dung.
  {
    sql(`UPDATE parking_card
            SET vehicle_name='Xe can giu', weight_text='1800KG', card_type='TAI',
                expiry_date='2028-01-31'
          WHERE card_id=${idA}`);

    // Gui DUNG nhung truong bat buoc, khong gui bon truong tren.
    const kq = await post("/Cards/Update", {
      card_id: idA, nguoi: NGUOI, card_no: `${DAU}-A`,
      customer_type_id: 1, weight_class_id: 0,
    });
    const r = sql(`SELECT COALESCE(vehicle_name,''), COALESCE(weight_text,''),
                          COALESCE(card_type,''), COALESCE(DATE_FORMAT(expiry_date,'%d/%m/%Y'),'')
                     FROM parking_card WHERE card_id=${idA}`)[0];
    const giu = r[0] === "Xe can giu" && r[1] === "1800KG" && r[2] === "TAI"
             && r[3] === "31/01/2028";
    soLieu.thieu_truong = { http: kq.status, doc_lai: r };
    ghi("thieu_truong_khong_xoa_du_lieu", kq.status === 200 && giu,
        `HTTP ${kq.status}; sau khi POST thieu 4 truong, doc lai: ${r.join(" | ")}`);
  }

  // ------------------------------------------------------ 13. nhat ky ghi du
  {
    const sauDong = demDongNhatKy();
    let noiDung = "";
    try { noiDung = readFileSync(NHAT_KY, "utf8"); } catch { /* chua co file */ }
    const dongMoi = noiDung.split(/\r?\n/).filter((d) => d.includes(maA));
    const coNguoi = dongMoi.every((d) => d.includes(NGUOI));
    const coTuChoi = dongMoi.some((d) => d.includes("TU_CHOI"));
    const coOk = dongMoi.some((d) => d.includes("\tOK\t"));

    soLieu.nhat_ky = { truoc: nhatKyTruoc, sau: sauDong, dong_cua_the_thu: dongMoi.length,
                       co_tu_choi: coTuChoi, co_ok: coOk };
    ghi("nhat_ky_ghi_du", dongMoi.length > 0 && coNguoi && coTuChoi && coOk,
        `${dongMoi.length} dong mang ma the thu; co dong OK: ${coOk}; ` +
        `co dong TU_CHOI: ${coTuChoi}; moi dong deu co ten nguoi: ${coNguoi}`);
  }
} catch (e) {
  ghi("phep_thu_api_the", false, e.message);
} finally {
  try {
    if (oMuon) sql(`UPDATE plc_slot_state SET card_code=NULL
                     WHERE block_id=${oMuon.block_id} AND slot_index=${oMuon.slot_index}
                       AND card_code LIKE 'AA%'`);
    if (idA) sql(`DELETE FROM parking_session WHERE card_id IN (${idA}, ${idB || idA})`);
    const maCu = sql(`SELECT card_code FROM parking_card WHERE source_label LIKE '${DAU}%'`);
    for (const [m] of maCu) sql(`DELETE FROM plc_request WHERE card_code='${m}'`);
    // Xoa cung O DAY la hop le: hai dong nay do chinh script chen, chua bao gio
    // nam trong thanh ghi PLC nao.
    sql(`DELETE FROM parking_card WHERE source_label LIKE '${DAU}%'`);

    const sauThe = Number(mot("SELECT COUNT(*) FROM parking_card"));
    const sauODo = demODo();
    const vanTayCuoi = VAN_TAY();
    soLieu.sau = { so_the: sauThe, so_o_do: sauODo };
    ghi("khoi_phuc_nguyen_trang",
        sauThe === soTheTruoc && sauODo === soODoTruoc && vanTayCuoi === vanTayDau,
        `the ${soTheTruoc}->${sauThe}, o do ${soODoTruoc}->${sauODo}, ` +
        `van tay toan bang ${vanTayCuoi === vanTayDau ? "khop" : "LECH"}`);
  } catch (e) {
    ghi("khoi_phuc_nguyen_trang", false, "KHONG DON DUOC: " + e.message +
        ` — chay tay: DELETE FROM parking_card WHERE source_label LIKE '${DAU}%';`);
  }
  luu();
}

const rot = ketQua.filter((k) => !k.dat);
console.log(`\n${ketQua.length - rot.length}/${ketQua.length} PASS`);
process.exitCode = rot.length === 0 ? 0 : 1;
