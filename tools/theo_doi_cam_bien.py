# -*- coding: utf-8 -*-
"""Theo dõi cảm biến đỗ thường đổi trạng thái, theo thời gian thực.

Dùng khi cần biết ô đỗ nào ứng với địa chỉ cảm biến nào: một người ra đỗ xe
vào một ô cụ thể, công cụ này in ra ngay địa chỉ vừa đổi.

    python tools/theo_doi_cam_bien.py                 # theo dõi mọi cảm biến
    python tools/theo_doi_cam_bien.py --zcu 3,4       # chỉ hai ZCU
    python tools/theo_doi_cam_bien.py --giay 600      # dừng sau 10 phút

ĐỌC changed_at CHỨ KHÔNG ĐỌC read_at. read_at nhích mỗi 5 giây kể cả khi
không có gì xảy ra, nên lọc theo nó thì mọi cảm biến đều "vừa đổi".

CHỈ ĐỌC. Không ghi gì xuống cơ sở dữ liệu, không chạm vào CCU hay ZCU.
"""
import argparse
import io
import os
import re
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")

MYSQL = r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe"

# Nhãn bản vẽ = vi_tri - 1. Vị trí 1 của mọi lộ luôn là "không lắp"; cảm biến
# thật chạy từ vị trí 2. Đo 28/09/2026 trên cả 10 lộ.
TEN = {0: "TRỐNG", 1: "CÓ XE", 2: "lỗi", 3: "không lắp"}


def doc(sql):
    """Chạy một câu lệnh, trả về danh sách dòng đã tách cột."""
    r = subprocess.run(
        [MYSQL, "-h", "127.0.0.1", "-u", "totalparking", "-p12345678",
         "total_parking", "-N", "-B", "-e", sql],
        capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise RuntimeError((r.stderr or "").strip()[:300])
    return [d.split("\t") for d in r.stdout.splitlines() if d.strip()]


def nhan_ban_ve(zcu, lo, vi_tri):
    # ZCU 2 bị đảo hai lộ (người dùng xác nhận 28/09/2026), nên nhãn trên bản
    # vẽ dùng số lộ ngược lại.
    lo_ve = (3 - lo) if zcu == 2 else lo
    # Bảy địa chỉ của ZCU 4 mang nhãn Z3 trên bản vẽ: bản vẽ được vẽ trước khi
    # lắp ZCU 4 và chưa cập nhật.
    z_ve = 3 if zcu == 4 else zcu
    return "Z%d,%d.%d" % (z_ve, lo_ve, vi_tri - 1)


def chup(loc_zcu):
    dk = ""
    if loc_zcu:
        dk = " AND zcu_id IN (%s)" % ",".join(str(z) for z in loc_zcu)
    sql = ("SELECT zcu_id, lo, vi_tri, trang_thai, changed_at "
           "FROM pgs_sensor_state WHERE trang_thai <> 3" + dk)
    ra = {}
    for d in doc(sql):
        ra[(int(d[0]), int(d[1]), int(d[2]))] = (int(d[3]), d[4])
    return ra


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--zcu", default="", help="loc theo ZCU, vd 3,4")
    ap.add_argument("--giay", type=int, default=0, help="dung sau bao nhieu giay, 0 = chay mai")
    ap.add_argument("--nhip", type=float, default=2.0, help="giay giua hai lan doc")
    a = ap.parse_args()

    loc = [int(x) for x in re.findall(r"\d+", a.zcu)]
    truoc = chup(loc)
    print("Đang theo dõi %d cảm biến%s. Ctrl+C để dừng.\n"
          % (len(truoc), (" (ZCU %s)" % a.zcu) if loc else ""))
    print("%-9s %-22s %-13s %-9s -> %s" % ("giờ", "địa chỉ", "nhãn bản vẽ", "từ", "thành"))
    print("-" * 76)

    bat_dau = time.time()
    try:
        while True:
            time.sleep(a.nhip)
            try:
                nay = chup(loc)
            except Exception as e:
                # Mất kết nối một nhịp không phải lý do để mất cả phiên theo dõi.
                print("  ! không đọc được: %s" % e)
                continue

            for k, (tt, ch) in sorted(nay.items()):
                cu = truoc.get(k)
                # So bằng changed_at, không bằng trạng thái: hai lần đổi liên
                # tiếp về lại giá trị cũ vẫn là hai sự kiện có thật.
                if cu is not None and cu[1] != ch:
                    print("%-9s ZCU%d lộ%d vị trí %-6d %-13s %-9s -> %s"
                          % (time.strftime("%H:%M:%S"), k[0], k[1], k[2],
                             nhan_ban_ve(*k), TEN.get(cu[0], "?"), TEN.get(tt, "?")))
            truoc = nay

            if a.giay and time.time() - bat_dau >= a.giay:
                print("\nHết %d giây." % a.giay)
                return
    except KeyboardInterrupt:
        print("\nDừng.")


if __name__ == "__main__":
    main()
