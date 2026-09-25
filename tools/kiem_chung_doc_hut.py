#!/usr/bin/env python3
"""Kiem chung: D1004 khong con nhan bang tinh tu mot gia tri D106 nua voi.

DUNG LAI LOI THAT, khong mo phong. Chuoi ba buoc duoi day lap lai dung nhung gi
nhat ky ghi duoc 5 lan trong 4 ngay (block 75/103/103/64/103):

    D106 = 7660 A0F3  ->  D1004 = 1        (the a0f37660, 2200kg)
    D106 = 0000 A0F3  ->  D1004 = 3        <-- LOI: qua tai sai
    D106 = 0000 0000  ->  D1004 = 0

Sau ban va, buoc giua phai giu D1004 = 1.

VI SAO GHI THANG VAO D106
    D106 la thanh ghi ma the quet, do ladder ghi. Ghi tay vao do la gia lap mot
    luot quet. KHONG thuoc vung o do (D200-D211, D300-D311, D400-D411) nen khong
    co nguy co ladder xep chong xe. Nhung van phai chon block DANG RANH.

    Script dung thu vien cua tools/plc_register.py chu khong qua CLI cua no, nen
    hang rao --force khong ap dung. D106 nam ngoai vung nguy hiem nen day khong
    phai duong vong; hang rao do danh cho vung o do.

DIEU KIEN HOP LE
    tools/plc_register.py:38-40 canh bao khong chay vao block dang trong vong
    poll vi hai ket noi FINS co the lam lech khung tin. O day KHONG tranh duoc:
    phai co vong poll chay thi D1004 moi duoc ghi. Bu lai bang kiem tra cuoi:
    neu plc_audit.log co dong loi FINS nao cho IP block thu trong cua so chay
    thi HUY luot do, khong duoc dien giai ket qua.
"""
import argparse
import json
import os
import re
import sys
import time
import urllib.request
from datetime import datetime, timedelta

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(REPO, "tools"))

from plc_register import FinsClient, FinsError, lookup_plc   # noqa: E402

TRIEN_KHAI = r"C:\Users\Admin\Documents\Web\totalParking"
LOG_DOI = os.path.join(TRIEN_KHAI, "App_Data", "plc_register_changes.log")
LOG_AUDIT = os.path.join(TRIEN_KHAI, "App_Data", "plc_audit.log")
TRANG_THAI = "http://localhost:8080/PlcStatus"

THE_THAP, THE_CAO = 0x7660, 0xA0F3        # the a0f37660, 2200kg -> bang 1
CHO_TOI_DA = 6.0                          # giay cho D1004 doi, ~12 nhip poll


def doc_trang_thai():
    with urllib.request.urlopen(TRANG_THAI, timeout=30) as r:
        return json.load(r)


def tien_de(block):
    """Moi dieu kien phai dat truoc khi do. Tra list loi; rong = qua."""
    loi = []

    dll_repo = os.path.join(REPO, "TotalParking", "bin", "TotalParking.dll")
    dll_chay = os.path.join(TRIEN_KHAI, "bin", "TotalParking.dll")
    if not os.path.exists(dll_chay):
        loi.append("Khong thay DLL o ban trien khai: %s" % dll_chay)
    elif os.path.getmtime(dll_chay) < os.path.getmtime(dll_repo) - 1:
        loi.append("Ban trien khai CU hon ban build (%s < %s). Phai deploy truoc."
                   % (datetime.fromtimestamp(os.path.getmtime(dll_chay)),
                      datetime.fromtimestamp(os.path.getmtime(dll_repo))))

    try:
        tt = doc_trang_thai()
    except Exception as e:
        loi.append("Khong goi duoc %s: %s" % (TRANG_THAI, e))
        return loi

    if not tt.get("poll_running"):
        loi.append("poll_running = False; vong poll khong chay thi D1004 khong ai ghi.")

    hang = [b for b in tt.get("blocks", []) if b.get("block_no") == block]
    if not hang:
        loi.append("Block %d khong nam trong vong poll." % block)
    elif not hang[0].get("online"):
        loi.append("Block %d dang offline (%s)." % (block, hang[0].get("error")))

    # Block phai rang: khong co luot quet nao 60 giay gan day.
    #
    # Bo qua dong [dau tien]: do la anh chup trang thai luc app khoi dong, khong
    # phai luot quet. Moi lan deploy sinh ra MOT dong nhu vay cho TUNG block
    # (do duoc 217 dong cung mot moc), nen dem chung vao thi khong block nao qua
    # duoc cua nay trong vai phut sau moi lan trien khai — dung luc can chay.
    nguong = datetime.now() - timedelta(seconds=60)
    for luc, so, reg, con in doc_nhat_ky():
        if (so == block and reg == "D106" and luc >= nguong
                and "[dau tien]" not in con):
            loi.append("Block %d vua co luot quet luc %s — chon block khac."
                       % (block, luc.strftime("%H:%M:%S")))
            break

    return loi


DONG = re.compile(r"^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d+)\s+\S+\s+block (\d+)\s+(\S+)\s+(.*)$")


def doc_nhat_ky():
    """Tra [(luc, block, thanh_ghi, phan_con_lai)] tu nhat ky doi thanh ghi."""
    ra = []
    if not os.path.exists(LOG_DOI):
        return ra
    with open(LOG_DOI, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = DONG.match(line.lstrip("\ufeff"))
            if m:
                ra.append((datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S.%f"),
                           int(m.group(2)), m.group(3), m.group(4)))
    return ra


def loi_fins(ip, tu_luc):
    """Dong loi FINS cho IP nay ke tu tu_luc. Rong = luot do hop le."""
    if not os.path.exists(LOG_AUDIT):
        return []
    ra = []
    moc = tu_luc.strftime("%Y-%m-%d %H:%M:%S")
    with open(LOG_AUDIT, encoding="utf-8", errors="replace") as f:
        for line in f:
            if ip in line and line[:19] >= moc and re.search(r"LOI|ERROR|0x0000", line):
                ra.append(line.rstrip())
    return ra


def cho_d1004(c, mong_doi, nhan):
    """Doc D1004 toi khi bang mong_doi hoac het gio. Tra (dat, gia_tri_cuoi)."""
    het = time.time() + CHO_TOI_DA
    v = None
    while time.time() < het:
        v = c.read_words(1004, 1)[0]
        if v == mong_doi:
            print("    %-46s D1004 = %d  DAT" % (nhan, v))
            return True, v
        time.sleep(0.25)
    print("    %-46s D1004 = %s  TRUOT (can %d)" % (nhan, v, mong_doi))
    return False, v


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--block", type=int, required=True, help="Block DANG RANH de thu")
    args = p.parse_args()

    print("=== KIEM CHUNG CHAN DOC HUT D106 -> D1004 ===")
    print("Block %d | %s\n" % (args.block, datetime.now().strftime("%Y-%m-%d %H:%M:%S")))

    print("[Tien de]")
    loi = tien_de(args.block)
    if loi:
        for x in loi:
            print("    KHONG DAT: " + x)
        return 2
    print("    Tat ca dat: da trien khai, poll dang chay, block online va rang\n")

    plc = lookup_plc(args.block)
    bat_dau = datetime.now()
    ket_qua = {}

    with FinsClient(plc["ip"], plc["port"], pc_node=plc["pc_node"],
                    plc_node=plc["plc_node"], timeout_ms=plc["timeout_ms"]) as c:
        try:
            print("[Buoc 1] AC-02 — the day du phai duoc ghi bang binh thuong")
            c.write_words(106, [THE_THAP, THE_CAO])
            ket_qua["AC-02"] = cho_d1004(c, 1, "D106 = 7660 A0F3 (a0f37660, 2200kg)")[0]

            print("\n[Buoc 2] AC-01 — nua voi KHONG duoc lam D1004 nhay sang 3")
            c.write_words(106, [0x0000, THE_CAO])
            time.sleep(CHO_TOI_DA / 2)
            v = c.read_words(1004, 1)[0]
            ket_qua["AC-01"] = (v == 1)
            print("    %-46s D1004 = %d  %s"
                  % ("D106 = 0000 A0F3 (nua voi)", v,
                     "DAT (giu nguyen 1)" if v == 1 else
                     "TRUOT — day dung la loi can chan" if v == 3 else "TRUOT"))

            print("\n[Buoc 3] AC-03 — nhat ky van phai ghi duoc gia tri nua voi")
            time.sleep(1.0)
            thay = [(l, r, s) for l, b, r, s in doc_nhat_ky()
                    if b == args.block and r == "D106" and l >= bat_dau
                    and "0000 A0F3" in s.upper()]
            ket_qua["AC-03"] = bool(thay)
            if thay:
                print("    DAT: %s  %s" % (thay[-1][0].strftime("%H:%M:%S.%f")[:-3], thay[-1][2]))
            else:
                print("    TRUOT: khong tim thay dong D106 nao mang 0000 A0F3")
        finally:
            print("\n[Buoc 4] Don dep — luon chay ke ca khi buoc tren truot")
            c.write_words(106, [0x0000, 0x0000])
            cho_d1004(c, 0, "D106 = 0000 0000 (sach)")

    print("\n[Dieu kien hop le] Loi FINS cho %s trong cua so chay" % plc["ip"])
    lf = loi_fins(plc["ip"], bat_dau)
    if lf:
        print("    CO %d dong loi -> HUY luot do, chay lai:" % len(lf))
        for x in lf[:5]:
            print("      " + x)
        return 3
    print("    Khong co. Luot do hop le.")

    print("\n=== KET QUA ===")
    for k in ("AC-01", "AC-02", "AC-03"):
        print("  %s : %s" % (k, "PASS" if ket_qua.get(k) else "FAIL"))
    tat_ca = all(ket_qua.get(k) for k in ("AC-01", "AC-02", "AC-03"))
    print("\n  Verification: %s" % ("PASS" if tat_ca else "FAIL"))
    return 0 if tat_ca else 1


if __name__ == "__main__":
    sys.exit(main())
