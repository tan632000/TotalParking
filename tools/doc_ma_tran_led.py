# -*- coding: utf-8 -*-
"""Đọc docs/LumiSlotsMatrix.xlsx — bảng khách trả lời: mỗi cổng LED phục vụ
những block nào và những cảm biến đỗ thường nào.

    python tools/doc_ma_tran_led.py

Thoát 0 khi bảng tự nhất quán và mọi tham chiếu đều phân giải được, khác 0 khi
còn điểm không khớp.

===================== HƯỚNG LÀ Tr / Th / P =====================
Trái / Thẳng / Phải — theo hướng người lái đang đi tới, KHÔNG phải theo trục
bản vẽ. Đây là lý do mọi phép đo hình học trước đó đều trượt: bảng LED nói
trong hệ quy chiếu của người lái, còn mũi tên đỏ trên bản vẽ nằm trong hệ quy
chiếu của tờ giấy. Hai hệ đó chỉ trùng nhau khi xe đi đúng chiều tờ giấy.

===================== KÝ HIỆU CẢM BIẾN =====================
    Za.b.c      a = số ZCU, b = lô (1 hoặc 2), c = ID cảm biến
    Za.b.c~f    cảm biến từ c đến f
    Za          toàn bộ cảm biến của ZCU đó

CHỈ ĐỌC. Không ghi vào cơ sở dữ liệu, không ghi xuống thiết bị.
"""
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.stdout.reconfigure(encoding="utf-8")

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
XLSX = os.path.join(GOC, "docs", "LumiSlotsMatrix.xlsx")
MYSQL = r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe"
NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"

HUONG = {"tr": "trai", "th": "thang", "p": "phai"}

# Bảy khối này KHÔNG THIẾU SÓT, mà cố ý không có cổng LED nào phục vụ: xe vào
# chúng từ ram dốc, và lối đó không đặt bảng LED (người dùng xác nhận 28/09/2026).
# Ram dưới chỉ đi vào, ram trên chỉ đi ra.
#
# Phép đo hình học KHÔNG bác được điều này, dù bảy khối đó nằm khá gần cảm biến
# (trung bình 60 px, gần hơn mức chung 92 px): mọi cảm biến cạnh chúng đều đã
# thuộc cổng khác (54-thẳng, 65-trái, 65-phải, 57-trái), và không cổng nào trong
# số đó chứa khối 63..69. Cảm biến ở đó phục vụ lối đi khác, không phải lối ram.
KHONG_CO_BANG_LED = {63, 64, 65, 66, 67, 68, 69}


def doc_o():
    z = zipfile.ZipFile(XLSX)
    ss = ["".join(t.text or "" for t in si.iter(NS + "t"))
          for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall(NS + "si")]
    o = {}
    for c in ET.fromstring(z.read("xl/worksheets/sheet1.xml")).iter(NS + "c"):
        m = re.match(r"([A-Z]+)(\d+)", c.get("r") or "A1")
        col = 0
        for ch in m.group(1):
            col = col * 26 + (ord(ch) - 64)
        p = (col - 1, int(m.group(2)) - 1)
        v = c.find(NS + "v")
        isn = c.find(NS + "is")
        if c.get("t") == "s" and v is not None:
            o[p] = ss[int(v.text)]
        elif isn is not None:
            o[p] = "".join(t.text or "" for t in isn.iter(NS + "t"))
        elif v is not None:
            o[p] = v.text
    return o


def tach_block(s):
    """'1~10,16~20,30~54' -> tập số block."""
    ra = set()
    for phan in re.split(r"[,;]", s or ""):
        phan = phan.strip()
        if not phan:
            continue
        m = re.fullmatch(r"(\d+)\s*~\s*(\d+)", phan)
        if m:
            ra.update(range(int(m.group(1)), int(m.group(2)) + 1))
        elif re.fullmatch(r"\d+", phan):
            ra.add(int(phan))
        else:
            raise ValueError("khong hieu phan block %r" % phan)
    return ra


def quy_doi(z, lo, c):
    """Nhãn trên tài liệu -> khoá thật trong pgs_sensor_map.

    Hai phép chỉnh, cả hai đều đã được chứng thực độc lập trước đó:

    1. LỆCH +1. Tài liệu đánh cảm biến từ 1, còn `vi_tri` trong cơ sở dữ liệu là
       số kênh, chạy từ 2. Đo được ở CẢ 10 nhóm (zcu, lô): min = 2, max = n+1.

    2. ZCU 2 ĐẢO LÔ. Nhãn trên bản vẽ ghi lô 1 có 11 cảm biến và lô 2 có 7,
       trong khi đấu dây thật thì ngược lại. Phép đảo này đến từ hiện trường
       (3 xe đỗ thường giữa block 63 và 64), và bản vẽ mới `IP_normal_sensor_with_label.pdf`
       đếm được 11/7 đúng như nhãn cũ — tức nhãn sai chứ không phải cơ sở dữ liệu.
    """
    if z == 2:
        lo = 3 - lo
    return (z, lo, c + 1)


def tach_cam_bien(s, ban_do):
    """'Z1.2.4~13, Z2.2.1~7' hoặc 'Z4' -> tập (zcu, lo, vi_tri) theo khoá thật.

    ban_do: tập mọi (zcu, lo, vi_tri) có thật, dùng để mở rộng dạng 'Za'.
    """
    ra = set()
    for phan in re.split(r"[,;]", s or ""):
        phan = phan.strip()
        if not phan:
            continue
        m = re.fullmatch(r"Z(\d)\.(\d)\.(\d+)\s*~\s*(\d+)", phan)
        if m:
            z, lo, a, b = (int(x) for x in m.groups())
            ra.update(quy_doi(z, lo, i) for i in range(a, b + 1))
            continue
        m = re.fullmatch(r"Z(\d)\.(\d)\.(\d+)", phan)
        if m:
            z, lo, c = (int(x) for x in m.groups())
            ra.add(quy_doi(z, lo, c))
            continue
        m = re.fullmatch(r"Z(\d)", phan)
        if m:
            z = int(m.group(1))
            ra.update(k for k in ban_do if k[0] == z)
            continue
        raise ValueError("khong hieu phan cam bien %r" % phan)
    return ra


def tv(sql):
    r = subprocess.run([MYSQL, "-h", "127.0.0.1", "-u", "totalparking", "-p12345678",
                        "total_parking", "-N", "-B", "-e", sql],
                       capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise SystemExit("khong doc duoc CSDL: " + (r.stderr or "")[:200])
    return [d.split("\t") for d in r.stdout.splitlines() if d.strip()]


def main():
    o = doc_o()
    ban_do = {(int(a[0]), int(a[1]), int(a[2])) for a in
              tv("SELECT zcu_id, lo, vi_tri FROM pgs_sensor_map")}
    khoi = {int(a[0]) for a in tv("SELECT block_no FROM block WHERE kind='Mechanical'")}

    cong = []
    led = None
    for r in range(1, max(k[1] for k in o) + 1):
        c0 = (o.get((0, r)) or "").strip()
        h = (o.get((1, r)) or "").strip().lower()
        if c0:
            led = c0
        if not h:
            continue
        cong.append({"led": led, "huong": HUONG.get(h, h),
                     "block_raw": (o.get((2, r)) or "").strip(),
                     "cb_raw": (o.get((3, r)) or "").strip()})

    xau = 0
    print("=== %d CONG DOC DUOC ===\n" % len(cong))
    print("%-6s %-7s %-5s %-6s %s" % ("led", "huong", "block", "cam bien", "block phuc vu"))
    tong_b, tong_c = set(), set()
    for c in cong:
        try:
            c["block"] = tach_block(c["block_raw"])
            c["cb"] = tach_cam_bien(c["cb_raw"], ban_do)
        except ValueError as e:
            print("  ! %s-%s: %s" % (c["led"], c["huong"], e))
            xau += 1
            continue
        tong_b |= c["block"]
        tong_c |= c["cb"]
        print("  %-6s %-7s %-5d %-6d %s" % (c["led"], c["huong"], len(c["block"]),
                                            len(c["cb"]), c["block_raw"]))

    print("\n=== KIEM TRA ===")
    la = sorted(tong_b - khoi)
    print("block trong bang nhung KHONG CO trong CSDL: %d %s" % (len(la), la))
    thieu = sorted(khoi - tong_b)
    ngoai_y = sorted(set(thieu) - KHONG_CO_BANG_LED)
    print("block khong cong nao phuc vu: %d %s" % (len(thieu), thieu))
    print("   trong do da biet ly do (vao tu ram, khong co bang LED): %d %s"
          % (len(set(thieu) & KHONG_CO_BANG_LED), sorted(set(thieu) & KHONG_CO_BANG_LED)))
    print("   CHUA GIAI THICH DUOC: %d %s" % (len(ngoai_y), ngoai_y))
    la_c = sorted(tong_c - ban_do)
    print("cam bien trong bang nhung KHONG CO that   : %d %s" % (len(la_c), la_c[:12]))
    thieu_c = sorted(ban_do - tong_c)
    print("cam bien co that nhung khong cong nao dung: %d %s" % (len(thieu_c), thieu_c[:12]))
    xau += len(la) + len(ngoai_y) + len(la_c)

    db = {}
    for a in tv("SELECT p.code, COUNT(*) FROM led_panel p JOIN led_panel_port o "
                "ON o.panel_id=p.panel_id WHERE p.kind='DIRECTIONAL' GROUP BY p.code"):
        db[a[0]] = int(a[1])
    dem = {}
    for c in cong:
        dem[c["led"]] = dem.get(c["led"], 0) + 1
    print("\nSO CONG MOI BANG — bang khach so voi CSDL:")
    for ma in sorted(set(list(db) + list(dem))):
        ok = db.get(ma) == dem.get(ma)
        xau += (not ok)
        print("   bang %-4s khach %-3s CSDL %-3s %s"
              % (ma, dem.get(ma, "-"), db.get(ma, "-"), "OK" if ok else "LECH"))

    print("\nKET QUA: %s" % ("SACH" if xau == 0 else "CON %d DIEM CAN XEM" % xau))
    return 0 if xau == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
