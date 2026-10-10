#!/usr/bin/env python3
"""Kiem tra du lieu duong di man hinh tai xe (specs/man-tai-xe-zones-map, task 01).

Chi DOC: TotalParking/App_Data/driver_lanes.json, anh zones_map.jpeg / plan_map.jpg
va bang `block` trong CSDL (SELECT). Khong ghi gi ngoai anh phu trong
specs/man-tai-xe-zones-map/artifacts/.

    python tools\\kiem_tra_duong_tai_xe.py

9 muc, moi muc in PASS/FAIL; thoat 0 chi khi du 9 PASS.

File nay la NOI DUY NHAT dinh nghia mat na dai vang (mat_na_vang) va cach doc
bang block (doc_block_db); script sinh tools/so_hoa_duong_tai_xe.py import lai,
de hai ben khong the dung hai nguong khac nhau.
"""

import hashlib
import json
import os
import subprocess
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
from skimage.morphology import remove_small_objects

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ANH_ZONES = os.path.join(GOC, "TotalParking", "Images", "zones_map.jpeg")
ANH_PLAN = os.path.join(GOC, "TotalParking", "Images", "plan_map.jpg")
FILE_JSON = os.path.join(GOC, "TotalParking", "App_Data", "driver_lanes.json")
ANH_PHU = os.path.join(GOC, "specs", "man-tai-xe-zones-map", "artifacts", "ban_do_duong_tai_xe.png")

KHUNG_W, KHUNG_H = 4800, 3584
R1 = (3480, 1860)             # ram doc 1, user xac nhan 09/10


# --------------------------------------------------------------- dung chung
def mat_na_vang(rgb):
    """Mat na dai vang (lop xe) da lap lo.

    Mau vang cua lan (~244,209,105). Chu, mui ten va net ke ve de len lan lam
    mat na vo hang tram manh; closing 12 vong (3x3) lap lai.

    Sau do OPENING 8 vong: cat bo vien vang mong 1-2 px quanh cac block (block
    ve de len nen vang). Khong cat, skeleton bam theo vien do va duong di chay
    doc mep block (block 5, 69, 84, 80) ma kiem tra van cham 100% tren vang.
    Cuoi cung bo manh nho (< 20000 px) la o do / ky hieu le.
    """
    a = np.asarray(rgb).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    vang = (r > 220) & (g > 170) & (g < 225) & (b < 140) & (r - b > 110)
    lap = ndi.binary_closing(vang, structure=np.ones((3, 3)), iterations=12)
    lap = ndi.binary_opening(lap, structure=np.ones((3, 3)), iterations=8)
    return remove_small_objects(lap, max_size=20000)


def doc_block_db():
    """block_no -> (zone_id, map_x, map_y) cua moi block is_active = 1.

    map_x/map_y la khung plan_map (1594x1300); NULL voi khoi do nen 901-906.
    Doc qua mysql.exe theo cau hinh Web.config, giong tools/plc_register.py.
    """
    import plc_register as p
    cfg = p.read_db_config()
    env = dict(os.environ, MYSQL_PWD=cfg["password"])
    out = subprocess.run(
        [p.find_mysql_exe(), "-u", cfg["user"], "-h", cfg["host"], "-P", cfg["port"],
         cfg["db"], "-N", "-B", "-e",
         "SELECT block_no, zone_id, IFNULL(map_x,''), IFNULL(map_y,'') "
         "FROM block WHERE is_active = 1 ORDER BY block_no"],
        capture_output=True, text=True, env=env, timeout=20)
    if out.returncode != 0:
        raise RuntimeError("Loi doc CSDL: " + out.stderr.strip())
    kq = {}
    # KHONG strip() ca khoi: dong cuoi (khoi do nen, map_x rong) ket thuc bang
    # tab, strip se cat mat cot rong.
    for dong in out.stdout.splitlines():
        if not dong.strip():
            continue
        so, zone, mx, my = (dong.split("\t") + ["", ""])[:4]
        kq[int(so)] = (int(zone), int(mx) if mx else None, int(my) if my else None)
    return kq


def khop_affine(nguon, dich):
    """Affine binh phuong toi thieu nguon -> dich. Tra (M, sai so tung diem)."""
    A = np.array([[x, y, 1.0] for x, y in nguon])
    B = np.array(dich, float)
    M = np.linalg.lstsq(A, B, rcond=None)[0]
    du = np.hypot(*(A @ M - B).T)
    return M, du


def ap_affine(M, x, y):
    zx, zy = np.array([x, y, 1.0]) @ M
    return int(round(zx)), int(round(zy))


def kc_diem_doan(p, a, b):
    p, a, b = (np.array(v, float) for v in (p, a, b))
    ab = b - a
    t = 0.0 if not ab.any() else max(0.0, min(1.0, float((p - a) @ ab / (ab @ ab))))
    return float(np.hypot(*(a + t * ab - p)))


# --------------------------------------------------------------- kiem tra
def main():
    loi = []

    def ket_qua(ten, ok, chi_tiet):
        print("%s  %-22s %s" % ("PASS" if ok else "FAIL", ten, chi_tiet))
        if not ok:
            loi.append(ten)

    with open(FILE_JSON, encoding="utf-8") as f:
        du_lieu = json.load(f)
    nut = {n["id"]: (n["x"], n["y"]) for n in du_lieu["nodes"]}
    canh = [tuple(e) for e in du_lieu["edges"]]
    block = {int(k): v for k, v in du_lieu["blocks"].items()}
    entry = du_lieu["entry"]

    # Toan ven truoc moi kiem tra khac: du lieu treo thi bao FAIL, khong crash.
    treo = ([e for e in canh if e[0] not in nut or e[1] not in nut]
            + [b for b, v in block.items() if v.get("node") not in nut])
    khung_dung = du_lieu.get("frame") == {"w": KHUNG_W, "h": KHUNG_H}
    if treo or entry not in nut or not khung_dung:
        print("FAIL  %-22s entry_ton_tai=%s khung_dung=%s tham_chieu_treo=%s"
              % ("toan_ven", entry in nut, khung_dung, treo[:5]))
        print("\nTONG KET: du lieu khong toan ven")
        return 1

    zones = Image.open(ANH_ZONES).convert("RGB")
    mask = mat_na_vang(zones)

    # 1. entry tai R1
    kc = np.hypot(nut[entry][0] - R1[0], nut[entry][1] - R1[1]) if entry in nut else 1e9
    ket_qua("entry_tai_R1", entry in nut and kc <= 60,
            "entry=%s toa_do=%s cach_R1=%.0f px" % (entry, nut.get(entry), kc))

    # 2. moi canh lay mau moi 10 px, >= 90% nam tren mat na vang
    te = []
    for a, b in canh:
        (x1, y1), (x2, y2) = nut[a], nut[b]
        n = max(2, int(np.hypot(x2 - x1, y2 - y1) // 10) + 1)
        xs = np.linspace(x1, x2, n).round().astype(int)
        ys = np.linspace(y1, y2, n).round().astype(int)
        ti_le = mask[ys, xs].mean()
        if ti_le < 0.9:
            te.append((round(float(ti_le), 2), a, b))
    ket_qua("canh_tren_duong_vang", not te,
            "%d canh, %d canh < 90%% tren vang%s" % (len(canh), len(te), (": " + str(sorted(te)[:5])) if te else ""))

    # 3. lien thong
    ke = {i: [] for i in nut}
    for a, b in canh:
        ke[a].append(b)
        ke[b].append(a)
    toi = {entry} if entry in nut else set()
    hang = deque(toi)
    while hang:
        u = hang.popleft()
        for v in ke[u]:
            if v not in toi:
                toi.add(v)
                hang.append(v)
    ket_qua("lien_thong", len(toi) == len(nut), "%d/%d nut toi duoc tu entry" % (len(toi), len(nut)))

    # 4. dung tap block active trong CSDL
    db = doc_block_db()
    thieu, thua = sorted(set(db) - set(block)), sorted(set(block) - set(db))
    ket_qua("du_block_active", not thieu and not thua,
            "CSDL %d, file %d; thieu %s; thua %s" % (len(db), len(block), thieu, thua))

    # 5. nut den ton tai va toi duoc
    hong = [b for b, v in block.items() if v["node"] not in nut or v["node"] not in toi]
    ket_qua("block_toi_duoc", not hong, "%d block, khong toi duoc: %s" % (len(block), hong))

    # 6. chang cuoi block -> nut den <= 180 px, khong sat block khac < 40 px
    vt = {b: (v["x"], v["y"]) for b, v in block.items()}
    xau = []
    dai_max = 0.0
    for b, v in block.items():
        if v["node"] not in nut:
            continue
        a, c = vt[b], nut[v["node"]]
        dai = float(np.hypot(a[0] - c[0], a[1] - c[1]))
        dai_max = max(dai_max, dai)
        sat = [o for o in vt if o != b and kc_diem_doan(vt[o], a, c) < 40]
        if dai > 180 or sat:
            xau.append((b, round(dai), sat[:3]))
    ket_qua("chang_cuoi", not xau, "dai nhat %.0f px; vi pham: %s" % (dai_max, xau[:6]))

    # 7. khong hai block chung vi tri
    so = sorted(vt)
    gan = [(a, b) for i, a in enumerate(so) for b in so[i + 1:]
           if np.hypot(vt[a][0] - vt[b][0], vt[a][1] - vt[b][1]) < 20]
    ket_qua("block_khong_trung", not gan, "cap < 20 px: %s" % gan)

    # 8. affine: TU TINH LAI tu bang NEO, khong doc so do script sinh ghi
    import so_hoa_duong_tai_xe as sinh
    neo = sorted(sinh.NEO)
    M, du = khop_affine([db[b][1:] for b in neo], [sinh.NEO[b] for b in neo])
    for b, e in zip(neo, du):
        print("      neo block %d: sai so %.1f px" % (b, e))
    # Vi tri trong file phai dung vi tri ky vong; lech > 1 px nghia la file cu
    # (sua NEO/SUA_TAY/DO_NEN ma chua sinh lai).
    lech = []
    for b, v in block.items():
        if b in sinh.DO_NEN:
            ky_vong = sinh.DO_NEN[b]
        elif b in sinh.SUA_TAY:
            ky_vong = sinh.SUA_TAY[b]
        elif b in db and db[b][1] is not None:
            ky_vong = ap_affine(M, *db[b][1:])
        else:
            ky_vong = None
        if ky_vong is None or np.hypot(v["x"] - ky_vong[0], v["y"] - ky_vong[1]) > 1:
            lech.append(b)
    ket_qua("affine_sai_so", du.max() < 50 and not lech,
            "%d neo, sai so lon nhat %.1f px (block %d), trung binh %.1f px; block lech vi tri ky vong: %s"
            % (len(neo), du.max(), neo[int(du.argmax())], du.mean(), lech[:8]))
    for b, (x, y) in sorted(sinh.SUA_TAY.items()):
        ax, ay = ap_affine(M, *db[b][1:])
        print("      SUA_TAY block %d: (%d,%d) thay cho affine (%d,%d), lech %.0f px"
              % (b, x, y, ax, ay, np.hypot(x - ax, y - ay)))

    # 9. anh phu cho nguoi doi chieu
    ve_anh_phu(zones, nut, canh, entry, block)
    sha = hashlib.sha256(open(ANH_PHU, "rb").read()).hexdigest()
    ket_qua("anh_phu", os.path.getsize(ANH_PHU) > 0, "%s sha256=%s" % (os.path.relpath(ANH_PHU, GOC), sha))

    print()
    if loi:
        print("TONG KET: %d FAIL: %s" % (len(loi), ", ".join(loi)))
        return 1
    print("TONG KET: 9/9 PASS")
    return 0


def ve_anh_phu(zones, nut, canh, entry, block):
    """Trai: zones_map + mang (xanh) + R1 + so block dung + doan block->nut den.
    Phai: plan_map.jpg (so block in tren ban ve CAD la dung) de doi chieu."""
    im = zones.copy()
    d = ImageDraw.Draw(im)
    for a, b in canh:
        d.line([nut[a], nut[b]], fill=(0, 90, 255), width=12)
    f = ImageFont.truetype("arialbd.ttf", 46)
    fn = ImageFont.truetype("arialbd.ttf", 40)
    for b, v in sorted(block.items()):
        x, y = v["x"], v["y"]
        if v["node"] in nut:
            d.line([(x, y), nut[v["node"]]], fill=(255, 0, 200), width=6)
        mau = (0, 140, 60) if b >= 900 else (200, 0, 0)
        d.ellipse([x - 16, y - 16, x + 16, y + 16], fill=mau)
        d.text((x + 18, y - 26), str(b), fill=mau, font=fn if b < 900 else f,
               stroke_width=5, stroke_fill=(255, 255, 255))
    ex, ey = nut[entry]
    d.ellipse([ex - 45, ey - 45, ex + 45, ey + 45], outline=(220, 0, 0), width=12)
    d.text((ex - 40, ey - 120), "R1", fill=(220, 0, 0), font=ImageFont.truetype("arialbd.ttf", 80),
           stroke_width=6, stroke_fill=(255, 255, 255))

    H = 1800
    trai = im.resize((int(im.width * H / im.height), H))
    plan = Image.open(ANH_PLAN).convert("RGB")
    phai = plan.resize((int(plan.width * H / plan.height), H))
    tong = Image.new("RGB", (trai.width + phai.width + 20, H), (255, 255, 255))
    tong.paste(trai, (0, 0))
    tong.paste(phai, (trai.width + 20, 0))
    os.makedirs(os.path.dirname(ANH_PHU), exist_ok=True)
    tong.save(ANH_PHU, optimize=True)


if __name__ == "__main__":
    sys.exit(main())
