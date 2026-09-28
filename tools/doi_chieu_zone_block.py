# -*- coding: utf-8 -*-
"""Đối chiếu bảng `block` trong cơ sở dữ liệu với sheet CL1 của file thống kê.

    python tools/doi_chieu_zone_block.py --phan zone
    python tools/doi_chieu_zone_block.py --phan o-do
    python tools/doi_chieu_zone_block.py --phan luoi
    python tools/doi_chieu_zone_block.py --kiem-truoc-khi-xoa
    python tools/doi_chieu_zone_block.py --phan zone --phan o-do

Thoát 0 khi mọi phần được chọn đều sạch, khác 0 khi còn lệch.

===================== VÌ SAO TỰ ĐỌC XLSX =====================
Máy này không có openpyxl và không nên thêm phụ thuộc chỉ để chạy một lần đối
chiếu. File .xlsx là một file zip chứa XML, đọc bằng thư viện chuẩn là đủ.

===================== HAI TIỀN ĐỀ, KIỂM TRƯỚC MỌI THỨ =====================
Sheet CL1 phải có ĐÚNG 112 dòng và tổng 764 ô. Đây là lá chắn chống đọc nhầm
sheet: CL2 là cụm vật lý khác, 91 block và 651 ô. Đọc nhầm sheet rồi báo "khớp"
sẽ dẫn tới gán sai zone cho toàn bộ bãi.

CHỈ ĐỌC. Không có đường nào từ file này ghi vào cơ sở dữ liệu hay xuống PLC.
"""
import argparse
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.stdout.reconfigure(encoding="utf-8")

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
XLSX = os.path.join(GOC, "docs", "TTP_LUMI_TYPE_BLOCK_RE01.xlsx")
MYSQL = r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe"
NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
NSR = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"

SO_BLOCK_CL1 = 112
TONG_O_CL1 = 764

# Thứ tự thanh ghi của ô đỗ, theo hợp đồng khách xác nhận.
# Xem TotalParking/Services/Plc/SlotOccupancyReader.cs
WORD_ADDR = [400, 202, 204, 206, 208, 300, 302, 304, 306, 308]


# --------------------------------------------------------------- đọc xlsx
def _o(ref):
    m = re.match(r"([A-Z]+)(\d+)", ref)
    if not m:
        return None
    c = 0
    for ch in m.group(1):
        c = c * 26 + (ord(ch) - 64)
    return c - 1, int(m.group(2)) - 1


def doc_cl1():
    z = zipfile.ZipFile(XLSX)
    chuoi = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall(NS + "si"):
            chuoi.append("".join(t.text or "" for t in si.iter(NS + "t")))

    wb = ET.fromstring(z.read("xl/workbook.xml"))
    rels = {r.get("Id"): r.get("Target")
            for r in ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))}
    muc = None
    for s in wb.find(NS + "sheets"):
        if s.get("name").strip().upper() == "CL1":
            muc = "xl/" + rels[s.get(NSR + "id")].lstrip("/")
    if muc is None:
        raise SystemExit("Khong tim thay sheet CL1 trong " + XLSX)

    bang = {}
    for c in ET.fromstring(z.read(muc)).iter(NS + "c"):
        p = _o(c.get("r") or "")
        if not p:
            continue
        v = c.find(NS + "v")
        isn = c.find(NS + "is")
        if c.get("t") == "s" and v is not None:
            val = chuoi[int(v.text)]
        elif isn is not None:
            val = "".join(t.text or "" for t in isn.iter(NS + "t"))
        elif v is not None:
            val = v.text
        else:
            continue
        bang[p] = val

    hdr = {str(bang.get((c, 0))).strip().lower(): c for c in range(24) if bang.get((c, 0))}
    for c in ("số block", "zone", "spaces qty", "1row or 2 row"):
        if c not in hdr:
            raise SystemExit("Sheet CL1 thieu cot %r" % c)

    ra = {}
    for r in range(1, max(k[1] for k in bang) + 1):
        b = bang.get((hdr["số block"], r))
        zo = bang.get((hdr["zone"], r))
        s = bang.get((hdr["spaces qty"], r))
        hg = str(bang.get((hdr["1row or 2 row"], r)) or "").strip()
        if b is None or zo is None:
            continue
        try:
            ra[int(float(b))] = {"zone": int(float(zo)), "o": int(float(s)),
                                 "hang": 1 if hg.startswith("1") else 2}
        except (TypeError, ValueError):
            continue
    return ra


# --------------------------------------------------------------- đọc CSDL
def tv(sql):
    r = subprocess.run([MYSQL, "-h", "127.0.0.1", "-u", "totalparking", "-p12345678",
                        "total_parking", "-N", "-B", "-e", sql],
                       capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise SystemExit("Khong doc duoc CSDL: " + (r.stderr or "").strip()[:300])
    return [d.split("\t") for d in r.stdout.splitlines() if d.strip()]


def tien_de(cl1):
    loi = []
    if len(cl1) != SO_BLOCK_CL1:
        loi.append("Sheet CL1 co %d dong block, phai la %d. Co the da doc nham sheet "
                   "(CL2 co 91 dong)." % (len(cl1), SO_BLOCK_CL1))
    tong = sum(v["o"] for v in cl1.values())
    if tong != TONG_O_CL1:
        loi.append("Tong 'spaces qty' cua CL1 la %d, phai la %d." % (tong, TONG_O_CL1))
    tv("SELECT 1")
    return loi


# --------------------------------------------------------------- các phần
def phan_zone(cl1):
    db = {int(a[0]): int(a[1]) for a in
          tv("SELECT block_no, zone_id FROM block WHERE kind='Mechanical' AND is_active=1")}
    lech = sorted(b for b in db if b in cl1 and db[b] != cl1[b]["zone"])
    print("LECH ZONE: %d" % len(lech))
    for b in lech:
        print("   block %-4d CSDL zone %d  ->  CL1 zone %d" % (b, db[b], cl1[b]["zone"]))

    print("\nBLOCK DO NEN (phai khong doi):")
    for a in tv("SELECT block_no, zone_id, slot_count FROM block "
                "WHERE kind='Ground' ORDER BY block_no"):
        print("   block %-5s zone %-3s %s o" % (a[0], a[1], a[2]))
    return len(lech)


def phan_o_do(cl1):
    db = {int(a[0]): int(a[1]) for a in
          tv("SELECT block_no, slot_count FROM block WHERE kind='Mechanical' AND is_active=1")}
    lech = sorted(b for b in db if b in cl1 and db[b] != cl1[b]["o"])
    print("LECH SO O DO: %d" % len(lech))
    for b in lech:
        print("   block %-4d CSDL %-3d o  ->  CL1 %d o" % (b, db[b], cl1[b]["o"]))

    tong = int(tv("SELECT COALESCE(SUM(slot_count),0) FROM block "
                  "WHERE kind='Mechanical' AND is_active=1")[0][0])
    print("TONG O CO KHI: %d  (phai la %d)" % (tong, TONG_O_CL1))

    # Bất biến có sẵn: v_block_map.lech_so_o đã lọc kind='Mechanical'
    db_lech = tv("SELECT block_no FROM v_block_map WHERE lech_so_o = 1 ORDER BY block_no")
    print("LECH DONG BO O DO: %d" % len(db_lech))
    for a in db_lech:
        print("   block %s: so dong plc_slot_state khac slot_count" % a[0])

    sai_addr = tv(
        "SELECT b.block_no, s.slot_index, s.word_addr FROM plc_slot_state s "
        "JOIN block b ON b.block_id = s.block_id "
        "WHERE b.kind='Mechanical' AND b.is_active=1 AND s.word_addr <> ELT(s.slot_index, %s) "
        "ORDER BY b.block_no, s.slot_index" % ",".join(str(w) for w in WORD_ADDR))
    print("SAI WORD_ADDR: %d" % len(sai_addr))
    for a in sai_addr[:20]:
        print("   block %-4s o %-3s word_addr %s" % (a[0], a[1], a[2]))
    return len(lech) + (0 if tong == TONG_O_CL1 else 1) + len(db_lech) + len(sai_addr)


def phan_luoi(cl1):
    db = tv("SELECT block_no, slot_count, COALESCE(tier_count,-1), COALESCE(column_count,-1) "
            "FROM block WHERE kind='Mechanical' AND is_active=1 ORDER BY block_no")
    lech = []
    for a in db:
        b, s, t, c = int(a[0]), int(a[1]), int(a[2]), int(a[3])
        if t < 0 or c < 0 or t * c != s:
            lech.append((b, s, t, c))
    print("LECH LUOI: %d" % len(lech))
    for b, s, t, c in lech[:20]:
        print("   block %-4d %d o, tier=%s column=%s" % (
            b, s, "NULL" if t < 0 else t, "NULL" if c < 0 else c))

    print("\nTOTAL_TIER0 THEO ZONE:")
    rong = 0
    for a in tv("SELECT zone_id, total_tier0 FROM v_zone_capacity ORDER BY zone_id"):
        if int(a[1]) == 0:
            rong += 1
        print("   zone %-3s total_tier0 = %s" % (a[0], a[1]))
    return len(lech) + rong


def kiem_truoc_khi_xoa():
    """Liệt kê dòng SẮP XOÁ mà KHÔNG thoả cả ba điều kiện an toàn.

    Ba điều kiện phải cùng đúng thì mới được xoá:
      card_code IS NULL      — ô không giữ thẻ
      read_at còn tươi       — PLC vẫn đang đọc được ô đó
      changed_at IS NULL     — ô chưa từng đổi trạng thái

    Thiếu vị từ read_at là lỗ hổng thật: khi PLC rớt mạng, vòng quét bắt lỗi rồi
    bỏ qua, card_code giữ nguyên NULL cũ. Một xe cất bằng HMI lúc đó sẽ bị coi là
    ô trống và xoá mất dấu.
    """
    q = ("SELECT b.block_no, s.slot_index, COALESCE(s.card_code,'-'), "
         "       COALESCE(s.changed_at,'-'), COALESCE(s.read_at,'-'), "
         "       (s.read_at < NOW(3) - INTERVAL 5 MINUTE) AS cu "
         "FROM plc_slot_state s JOIN block b ON b.block_id = s.block_id "
         "WHERE ( (b.block_no=6 AND s.slot_index>6) OR (b.block_no=33 AND s.slot_index>6) "
         "     OR (b.block_no=88 AND s.slot_index>3) ) "
         "  AND NOT (s.card_code IS NULL AND s.changed_at IS NULL "
         "           AND s.read_at >= NOW(3) - INTERVAL 5 MINUTE) "
         "ORDER BY b.block_no, s.slot_index")
    xau = tv(q)
    tong = tv("SELECT COUNT(*) FROM plc_slot_state s JOIN block b ON b.block_id=s.block_id "
              "WHERE (b.block_no=6 AND s.slot_index>6) OR (b.block_no=33 AND s.slot_index>6) "
              "   OR (b.block_no=88 AND s.slot_index>3)")[0][0]
    print("DONG SAP XOA: %s  (ky vong 15)" % tong)
    print("DONG KHONG AN TOAN: %d" % len(xau))
    for a in xau:
        print("   block %-4s o %-3s the=%s changed_at=%s read_at=%s cu=%s"
              % (a[0], a[1], a[2], a[3], a[4], a[5]))
    return len(xau) + (0 if str(tong) == "15" else 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phan", action="append", default=[],
                    choices=["zone", "o-do", "luoi"])
    ap.add_argument("--kiem-truoc-khi-xoa", action="store_true")
    a = ap.parse_args()
    if not a.phan and not a.kiem_truoc_khi_xoa:
        ap.error("chon it nhat mot --phan hoac --kiem-truoc-khi-xoa")

    cl1 = doc_cl1()
    loi = tien_de(cl1)
    print("TIEN DE: CL1 co %d block, tong %d o  ->  %s"
          % (len(cl1), sum(v["o"] for v in cl1.values()), "DAT" if not loi else "HONG"))
    for x in loi:
        print("   ! " + x)
    if loi:
        return 2

    xau = 0
    for p in a.phan:
        print("\n" + "=" * 62)
        xau += {"zone": phan_zone, "o-do": phan_o_do, "luoi": phan_luoi}[p](cl1)
    if a.kiem_truoc_khi_xoa:
        print("\n" + "=" * 62)
        xau += kiem_truoc_khi_xoa()

    print("\n" + "=" * 62)
    print("KET QUA: %s" % ("SACH" if xau == 0 else "CON %d DIEM LECH" % xau))
    return 0 if xau == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
