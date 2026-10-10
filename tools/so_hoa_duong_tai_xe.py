#!/usr/bin/env python3
"""Sinh TotalParking/App_Data/driver_lanes.json cho man hinh tai xe.

(specs/man-tai-xe-zones-map, task 01). Chay tay, chi DOC anh va CSDL:

    python tools\\so_hoa_duong_tai_xe.py
    python tools\\kiem_tra_duong_tai_xe.py      # kiem lai + ve anh phu

KHUNG TOA DO: anh goc zones_map.jpeg 4800x3584. Anh nay ve DOC LAP voi ban ve CAD
(BlockMapRepository.cs:13-17), nen mang loi di duoc so hoa thang tren no.

1. TIM DUONG: lay tu mat na dai vang (mat_na_vang, dinh nghia trong script kiem)
   -> skeleton -> do thi: diem giao/diem cut la nut, doan giua la canh; cat nhanh
   vun < 80 px; lam gon bang RDP 12 px; gop nut cach nhau < 15 px; cat canh dai
   thanh doan <= 120 px. Giu thanh phan lien thong lon nhat.
2. NUT XUAT PHAT: nut gan R1 nhat (ram doc 1, user xac nhan 09/10).
3. VI TRI BLOCK CO KHI: toa do khung plan_map (block.map_x/map_y) -> zones_map
   bang MOT affine toan cuc khop tren bang NEO (chon tay). SUA_TAY ghi de block
   lech sau khi nguoi xem anh phu. Khong do khoi mau, khong can theo zone.
4. KHU DO NEN 901-906: diem dich trong bang DO_NEN (de xuat, cho nguoi xac nhan).
5. NUT DEN: chieu vi tri block len canh gan nhat, chen nut tai diem chieu.
"""

import json
import os
import sys

import networkx as nx
import numpy as np
from PIL import Image
from skimage.morphology import skeletonize

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kiem_tra_duong_tai_xe import (ANH_ZONES, FILE_JSON, KHUNG_H, KHUNG_W, R1,  # noqa: E402
                                   ap_affine, doc_block_db, khop_affine, mat_na_vang)

# Diem neo: block_no -> tam block tren zones_map (khung 4800x3584), chon bang mat o
# cac goc va giua mat bang. Nguon toa do (khung plan_map) lay tu CSDL.
NEO = {
    1: (1678, 217),   112: (3045, 3120), 43: (711, 816),   24: (3600, 810),
    66: (3324, 1971), 79: (1425, 1620),  13: (3180, 525),  102: (3444, 2586),
    90: (2112, 2265), 63: (2910, 1770),  44: (846, 1065),  20: (2784, 825),
}

# Block can dat tay (ghi de affine) sau khi nguoi xem anh phu: block_no -> (x, y).
SUA_TAY = {}

# Khu do nen: moi zone mot khoi (BlockAllocator.cs:57-58). Diem dich nam trong
# vung o do thuong cua zone (theo nhan ZONE in tren ban do), sat dai vang.
# DE XUAT — nguoi dung xac nhan tren anh phu.
DO_NEN = {
    901: (3250, 2300),   # Zone 1: day o do thuong phia tren lan tren cua Zone 1
    902: (2760, 2300),   # Zone 2: o do thuong canh lan doc Zone 2, duoi lan ngang duoi
    903: (744, 1068),    # Zone 3: o do thuong duoi lan ngang Zone 3
    904: (1992, 1440),   # Zone 4: o do thuong canh lan giua Zone 4
    905: (3600, 672),    # Zone 5: o do thuong ben phai lan Zone 5
    906: (3552, 1344),   # Zone 6: o do thuong cuoi lan duoi Zone 6
}

# Vung KHONG duoc coi la lan du mat na co mau vang: (x1, y1, x2, y2), kem ly do.
# Skeleton bat vao nhung dai hep ma xe khong di duoc; dan xe vao do la lach sat block.
BO_VUNG = [
    # Dai vang hep giua mep duoi block 29 va tuong do (x 2790-3030, y ~1162):
    # canh di sat mep block, chi 89% tren vang. Hai dau da noi qua lan ngang
    # y~960 va lan doc ben phai, nen bo khong lam mat duong nao.
    (2790, 1140, 3030, 1185),
]

GOP_NUT = 15       # gop nut cach nhau < 15 px
CANH_MAX = 120     # canh dai hon thi chia nho
CHIEU_MAX = 180    # vi tri block -> lan xa hon la loi


# ------------------------------------------------------------ skeleton -> polyline
def _do_thi_pixel(m):
    """Do thi networkx: moi pixel skeleton la mot nut, noi 8 huong. Canh cheo
    chi them khi hai pixel KHONG co lang gieng ngang/doc chung — neu khong, goc
    chu L thanh tam giac va tao nut bac 3 gia."""
    G = nx.Graph()
    ys, xs = np.nonzero(m)
    co = set(zip(ys.tolist(), xs.tolist()))
    for y, x in co:
        G.add_node((y, x))
        for dy, dx in ((0, 1), (1, 0)):
            if (y + dy, x + dx) in co:
                G.add_edge((y, x), (y + dy, x + dx))
        for dy, dx in ((1, 1), (1, -1)):
            q = (y + dy, x + dx)
            if q in co and (y + dy, x) not in co and (y, x + dx) not in co:
                G.add_edge((y, x), q)
    return G


def _chuoi_tu(G, dau, ke):
    """Di tu `dau` qua `ke` doc chuoi bac 2 toi nut bac khac 2 (hoac quay ve dau)."""
    duong, truoc, cur = [dau, ke], dau, ke
    while G.degree(cur) == 2 and cur != dau:
        tiep = [q for q in G.neighbors(cur) if q != truoc][0]
        duong.append(tiep)
        truoc, cur = cur, tiep
    return duong


def _cat_nhanh_vun(G, nguong):
    for _ in range(6):
        xoa = []
        for la in [v for v in G if G.degree(v) == 1]:
            duong = _chuoi_tu(G, la, next(iter(G.neighbors(la))))
            if len(duong) < nguong and G.degree(duong[-1]) >= 3:
                xoa.extend(duong[:-1])
        if not xoa:
            break
        G.remove_nodes_from(xoa)


def _polyline(G):
    """Moi chuoi giua hai nut bac khac 2 la mot polyline (x, y)."""
    then = {v for v in G if G.degree(v) != 2}
    for tp in nx.connected_components(G):            # vong kin thuan tuy
        if not (tp & then):
            then.add(next(iter(tp)))
    da_di, out = set(), []
    for a in then:
        for ke in G.neighbors(a):
            if (a, ke) in da_di:
                continue
            duong = _chuoi_tu(G, a, ke) if ke not in then else [a, ke]
            da_di.add((a, duong[1]))
            da_di.add((duong[-1], duong[-2]))
            out.append([(x, y) for y, x in duong])
    return out


def _rdp(pts, eps):
    if len(pts) < 3:
        return pts
    a, b = np.array(pts[0], float), np.array(pts[-1], float)
    ab = b - a
    L = float(np.hypot(*ab)) or 1.0
    d = [abs(ab[0] * (p[1] - a[1]) - ab[1] * (p[0] - a[0])) / L for p in pts[1:-1]]
    i = int(np.argmax(d)) + 1
    if d[i - 1] > eps:
        return _rdp(pts[:i + 1], eps)[:-1] + _rdp(pts[i:], eps)
    return [pts[0], pts[-1]]


def polyline_tu_mat_na(mask):
    G = _do_thi_pixel(skeletonize(mask))
    _cat_nhanh_vun(G, 80)
    return [[(int(x), int(y)) for x, y in _rdp(p, 12)] for p in _polyline(G) if len(p) >= 2]


# ------------------------------------------------------------ polyline -> do thi
class DoThi:
    def __init__(self):
        self.nut = {}            # id -> (x, y)
        self.canh = set()        # (a, b) voi a < b

    def them_nut(self, x, y):
        for i, (nx_, ny_) in self.nut.items():
            if abs(nx_ - x) < GOP_NUT and abs(ny_ - y) < GOP_NUT and np.hypot(nx_ - x, ny_ - y) < GOP_NUT:
                return i
        i = len(self.nut) + 1
        while i in self.nut:
            i += 1
        self.nut[i] = (int(x), int(y))
        return i

    def them_canh(self, a, b):
        if a != b:
            self.canh.add((min(a, b), max(a, b)))

    def chia_canh_dai(self):
        for a, b in list(self.canh):
            (x1, y1), (x2, y2) = self.nut[a], self.nut[b]
            k = int(np.ceil(np.hypot(x2 - x1, y2 - y1) / CANH_MAX))
            if k <= 1:
                continue
            self.canh.discard((a, b))
            truoc = a
            for j in range(1, k):
                t = j / k
                moi = self.them_nut(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t)
                self.them_canh(truoc, moi)
                truoc = moi
            self.them_canh(truoc, b)

    def giu_thanh_phan_lon_nhat(self):
        ke = {i: set() for i in self.nut}
        for a, b in self.canh:
            ke[a].add(b)
            ke[b].add(a)
        con_lai, lon = set(self.nut), set()
        while con_lai:
            goc = con_lai.pop()
            tp, hang = {goc}, [goc]
            while hang:
                u = hang.pop()
                for v in ke[u] - tp:
                    tp.add(v)
                    hang.append(v)
            con_lai -= tp
            if len(tp) > len(lon):
                lon = tp
        self.nut = {i: p for i, p in self.nut.items() if i in lon}
        self.canh = {(a, b) for a, b in self.canh if a in lon and b in lon}

    def chieu_len_canh(self, x, y):
        """Chen nut tai diem chieu cua (x,y) len canh gan nhat. Tra (id, khoang cach)."""
        p = np.array([x, y], float)
        tot = None
        for a, b in self.canh:
            A, B = np.array(self.nut[a], float), np.array(self.nut[b], float)
            ab = B - A
            t = max(0.0, min(1.0, float((p - A) @ ab / (ab @ ab)))) if ab.any() else 0.0
            q = A + t * ab
            d = float(np.hypot(*(q - p)))
            if tot is None or d < tot[0]:
                tot = (d, a, b, q)
        d, a, b, q = tot
        moi = self.them_nut(round(q[0]), round(q[1]))
        if moi not in (a, b):
            self.canh.discard((a, b))
            self.them_canh(a, moi)
            self.them_canh(moi, b)
        return moi, d


def main():
    zones = Image.open(ANH_ZONES).convert("RGB")
    assert zones.size == (KHUNG_W, KHUNG_H), "zones_map.jpeg khong phai 4800x3584"
    mask = mat_na_vang(zones)
    for x1, y1, x2, y2 in BO_VUNG:
        mask[y1:y2, x1:x2] = False

    g = DoThi()
    polylines = polyline_tu_mat_na(mask)
    for pl in polylines:
        ids = [g.them_nut(x, y) for x, y in pl]
        for a, b in zip(ids, ids[1:]):
            g.them_canh(a, b)
    g.giu_thanh_phan_lon_nhat()
    g.chia_canh_dai()
    print("tim duong: %d polyline -> %d nut, %d canh" % (len(polylines), len(g.nut), len(g.canh)))

    # Nut xuat phat DAT TAI R1. Buoc cat nhanh vun lam skeleton co lai o dau mut
    # (cach R1 ~50 px), nen them mot nut dung tai R1 roi noi vao nut lan gan nhat.
    if not mask[R1[1], R1[0]]:
        sys.exit("R1 %s khong nam tren dai vang." % (R1,))
    gan = min(g.nut, key=lambda i: np.hypot(g.nut[i][0] - R1[0], g.nut[i][1] - R1[1]))
    entry = g.them_nut(*R1)
    g.them_canh(entry, gan)
    kc = np.hypot(g.nut[entry][0] - R1[0], g.nut[entry][1] - R1[1])
    print("nut xuat phat %d tai %s (cach R1 %.0f px), noi vao nut %d %s"
          % (entry, g.nut[entry], kc, gan, g.nut[gan]))

    db = doc_block_db()
    neo = sorted(NEO)
    M, du = khop_affine([db[b][1:] for b in neo], [NEO[b] for b in neo])
    print("affine: %d neo, sai so lon nhat %.1f px" % (len(neo), du.max()))
    for b, e in zip(neo, du):
        print("  neo block %d: sai so %.1f px" % (b, e))
    if du.max() >= 50:
        sys.exit("Sai so affine >= 50 px — kiem lai bang NEO.")

    vi_tri = {}
    for b, (zone, mx, my) in db.items():
        if b in DO_NEN:
            vi_tri[b] = DO_NEN[b]
        elif b in SUA_TAY:
            vi_tri[b] = SUA_TAY[b]
        elif mx is not None:
            vi_tri[b] = ap_affine(M, mx, my)
        else:
            sys.exit("Block %d khong co toa do va khong co trong DO_NEN/SUA_TAY." % b)

    blocks = {}
    for b in sorted(vi_tri):
        x, y = vi_tri[b]
        node, d = g.chieu_len_canh(x, y)
        if d > CHIEU_MAX:
            sys.exit("Block %d cach lan %.0f px > %d — kiem lai vi tri." % (b, d, CHIEU_MAX))
        blocks[str(b)] = {"x": int(x), "y": int(y), "node": node}

    du_lieu = {
        "frame": {"w": KHUNG_W, "h": KHUNG_H},
        "entry": entry,
        "nodes": [{"id": i, "x": x, "y": y} for i, (x, y) in sorted(g.nut.items())],
        "edges": [[a, b] for a, b in sorted(g.canh)],
        "blocks": blocks,
    }
    with open(FILE_JSON, "w", encoding="utf-8", newline="\n") as f:
        json.dump(du_lieu, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    print("da ghi %s: %d nut, %d canh, %d block" % (os.path.relpath(FILE_JSON), len(g.nut), len(g.canh), len(blocks)))


if __name__ == "__main__":
    main()
