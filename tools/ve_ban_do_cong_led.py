# -*- coding: utf-8 -*-
"""Ban do de ben thiet ke danh dau moi cong LED phuc vu nhung block nao.

Vi sao khong tu suy ra duoc: mui ten tren ban ve chi LO TRINH LAI XE, khong
phai phuong hinh hoc. Phep thu hinh non (quet +-50 do tu bang) cho danh sach
block hop ly nhung chi khop 2/21 voi zone dang khai trong CSDL, nen khong dung
de ket luan. Chi nguoi nam mat bang moi tra loi duoc.

Nen: docs/IP_normal_sensor.pdf — file nay mang san 11 bang LED va 21 mui ten do.
"""
import math
import os
import re
import subprocess
import sys
import warnings

warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")
import pymupdf

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NGUON = os.path.join(GOC, "docs", "IP_normal_sensor.pdf")
DICH = os.path.join(GOC, "docs", "danh_dau_cong_led_block.pdf")
XEM = DICH.replace(".pdf", ".png")
MYSQL = r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe"

# Huong doc tu anh cat cum_1/2/3.png. Hai phep do tu dong deu that bai nen
# day la doc bang mat — chinh vi the ban do nay CAN duoc xac nhan.
BANG = {
    "51": (1241, 519, ["T", "P"]),
    "52": (1190, 225, ["L"]),
    "53": (1323, 464, ["L", "P", "X"]),
    "54": (1245, 712, ["L"]),
    "55": (1203, 465, ["T", "L"]),
    "56": (766, 462, ["T"]),
    "57": (1186, 936, ["T", "P", "X"]),
    "58": (1306, 369, ["L", "P"]),
    "65": (1285, 849, ["L", "T", "X"]),
    "66": (1185, 1056, ["P", "X"]),
    "67": (845, 850, ["T"]),
}
VEC = {"L": (0, -1), "X": (0, 1), "T": (-1, 0), "P": (1, 0)}
CHU = {"L": "len", "X": "xuong", "T": "trai", "P": "phai"}
MA_DIR = {"L": 0, "P": 1, "X": 2, "T": 3}

TIM = (0.55, 0, 0.75)
CAM = (0.9, 0.35, 0)
XANH = (0, 0.3, 0.8)


def truy_van(sql):
    r = subprocess.run([MYSQL, "-h", "127.0.0.1", "-u", "totalparking", "-p12345678",
                        "total_parking", "-N", "-B", "-e", sql],
                       capture_output=True, text=True)
    return [d.split("\t") for d in r.stdout.splitlines() if d.strip()]


def main():
    d = pymupdf.open(NGUON)
    p = d[0]

    # --- diem net do: dung de tim chop moi mui ten ---
    do_mau = {(0.96, 0.07, 0.04), (0.98, 0.21, 0.02), (1.0, 0.0, 0.0)}
    diem = []
    for g in p.get_drawings():
        c = g.get("color") or g.get("fill")
        if c and tuple(round(v, 2) for v in c) in do_mau:
            for it in g["items"]:
                for v in it[1:]:
                    if hasattr(v, "x"):
                        diem.append((v.x, v.y))

    # --- vi tri 112 block, lay tu chinh ban ve ---
    ung = {}
    for x0, y0, x1, y1, t, *_ in p.get_text("words"):
        s = t.strip()
        if re.fullmatch(r"\d{1,3}", s) and round(y1 - y0) == 9 and 1 <= int(s) <= 112:
            ung.setdefault(int(s), []).append(((x0 + x1) / 2, (y0 + y1) / 2))
    # 4 nhan trung, da phan giai bang phep khop affine voi toa do CAD (bien 105-594 lan)
    ro = {1: (766, 148), 2: (824, 148), 3: (941, 149), 6: (772, 294)}
    block = {n: (ro[n] if n in ro else v[0]) for n, v in ung.items()}

    zone_ht = {int(a[0]): int(a[1]) for a in
               truy_van("SELECT block_no, zone_id FROM block WHERE kind='Mechanical'")}
    khai = {}
    for a in truy_van("SELECT p.code, o.arrow_direction, o.zone_list FROM led_panel p "
                      "JOIN led_panel_port o ON o.panel_id = p.panel_id "
                      "WHERE p.kind='DIRECTIONAL'"):
        khai.setdefault(a[0], {})[int(a[1])] = a[2]

    # --- danh dau block ---
    for n, (x, y) in block.items():
        p.draw_circle(pymupdf.Point(x, y), 11, color=XANH, width=1.1)

    # --- gan moi net do cho DUNG MOT bang: bang gan nhat ---
    # Bang 53 va 58 chi cach nhau ~100 px, nen loc theo ban kinh se bat nham
    # mui ten cua bang ben canh va dat nhan sai cho.
    cua = {ma: [] for ma in BANG}
    for q in diem:
        ma = min(BANG, key=lambda k: math.hypot(q[0] - BANG[k][0], q[1] - BANG[k][1]))
        if math.hypot(q[0] - BANG[ma][0], q[1] - BANG[ma][1]) <= 130:
            cua[ma].append(q)

    # --- danh dau 21 cong ---
    dong = []
    for ma, (bx, by, hs) in sorted(BANG.items()):
        for h in hs:
            ux, uy = VEC[h]
            gan = cua[ma]
            chop = max(gan, key=lambda q: (q[0] - bx) * ux + (q[1] - by) * uy) if gan else (bx, by)
            tx, ty = chop[0] + ux * 26, chop[1] + uy * 26
            nhan = "%s-%s" % (ma, h)
            r = pymupdf.Rect(tx - 27, ty - 12, tx + 27, ty + 12)
            p.draw_rect(r, color=TIM, fill=(1, 1, 1), width=2)
            p.insert_text(pymupdf.Point(r.x0 + 4, r.y0 + 17), nhan,
                          fontsize=15, color=TIM, fontname="hebo")
            # KHONG tra theo ma huong: chinh ma huong trong CSDL dang tranh chap
            # (chi 3/11 bang khop voi mui ten tren ban ve). Liet ke ca cum zone
            # cua bang thi trung thuc hon mot o trong.
            zs = sorted(set(v for v in khai.get(ma, {}).values()
                            if v not in (None, "None")))
            dong.append((nhan, ma, CHU[h], ", ".join(zs) if zs else "-"))

    # --- chu giai ---
    k = pymupdf.Rect(60, 1150, 900, 1330)
    p.draw_rect(k, color=(0, 0, 0), fill=(1, 1, 1), width=2)
    p.insert_text(pymupdf.Point(78, 1185), "CAN DANH DAU: moi CONG LED phuc vu nhung BLOCK so may",
                  fontsize=21, color=(0, 0, 0), fontname="hebo")
    p.insert_text(pymupdf.Point(78, 1218), "O TIM  = 21 cong LED. Ma vd 53-P = bang 53, mui ten sang PHAI.",
                  fontsize=16, color=TIM, fontname="helv")
    p.insert_text(pymupdf.Point(78, 1246), "VONG XANH = 112 block co khi (so block in san giua vong).",
                  fontsize=16, color=XANH, fontname="helv")
    p.insert_text(pymupdf.Point(78, 1274),
                  "L=len  X=xuong  T=trai  P=phai. Huong doc tu mui ten do - xin xac nhan luon.",
                  fontsize=16, color=CAM, fontname="helv")
    p.insert_text(pymupdf.Point(78, 1302),
                  "Vi du cach ghi:  53-P: block 22, 23, 24, 25, 26",
                  fontsize=16, color=(0, 0, 0), fontname="helv")

    # --- trang 2: bang dien ---
    t2 = d.new_page(width=p.rect.width, height=p.rect.height)
    t2.insert_text(pymupdf.Point(90, 90), "BANG DIEN - moi cong LED phuc vu nhung block nao",
                   fontsize=30, color=(0, 0, 0), fontname="hebo")
    t2.insert_text(pymupdf.Point(90, 130),
                   "Cot 'zone dang khai' liet ke cac zone bang do dang khai trong he thong, de "
                   "doi chieu - khong phai dap an.",
                   fontsize=16, color=(0.35, 0.35, 0.35), fontname="helv")
    y = 190
    for tieu_de, x, w in (("Ma cong", 90, 130), ("Bang", 220, 90), ("Huong", 310, 110),
                          ("Zone dang khai (ca bang)", 420, 250), ("Block phuc vu (dien vao day)", 670, 1540)):
        t2.draw_rect(pymupdf.Rect(x, y - 26, x + w, y + 8), color=(0, 0, 0),
                     fill=(0.88, 0.88, 0.88), width=1.5)
        t2.insert_text(pymupdf.Point(x + 8, y), tieu_de, fontsize=17,
                       color=(0, 0, 0), fontname="hebo")
    y += 8
    for nhan, ma, huong, zl in dong:
        h = 52
        for x, w, val, mau in ((90, 130, nhan, TIM), (220, 90, ma, (0, 0, 0)),
                               (310, 110, huong, (0, 0, 0)),
                               (420, 250, "zone %s" % zl, (0.4, 0.4, 0.4)),
                               (670, 1540, "", (0, 0, 0))):
            t2.draw_rect(pymupdf.Rect(x, y, x + w, y + h), color=(0.3, 0.3, 0.3), width=1)
            if val:
                t2.insert_text(pymupdf.Point(x + 8, y + 33), val, fontsize=19,
                               color=mau, fontname="hebo" if x == 90 else "helv")
        y += h

    d.save(DICH)
    d[0].get_pixmap(matrix=pymupdf.Matrix(1.1, 1.1)).save(XEM)
    print("Da ghi %s" % DICH)
    print("%d cong, %d block da danh dau" % (len(dong), len(block)))
    for nhan, ma, huong, zl in dong:
        print("   %-7s bang %-3s huong %-6s zone dang khai: %s" % (nhan, ma, huong, zl))


if __name__ == "__main__":
    main()
