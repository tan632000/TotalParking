#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Reset thanh ghi chiem o cua block co khi ve 0 (FINS/UDP, Omron).

VIEC NAY LAM GI
---------------
Moi o do co khi giu MA THE 32-bit cua xe dang nam trong o, tai mot dia chi
thanh ghi D rieng. Hop dong dia chi (SlotOccupancyReader.cs:13-18):

    o 1      -> D400
    o 2..5   -> D202  D204  D206  D208
    o 6..10  -> D300  D302  D304  D306  D308

Dia chi THAT cua tung o lay tu cot plc_slot_state.word_addr chu khong tinh
lai o day: cot do la thu vong quet dang dung, nen ghi theo no thi khong the
lech khoi cho ma SCADA dang doc.

Ghi 0 vao nhung thanh ghi do = bao voi PLC rang o trong.

VI SAO MAC DINH LA CHAY THU
---------------------------
Thanh ghi bi ghi de la mat han, khong co ban sao nao khac. Nen:
  - khong co --xac-nhan  -> chi doc va bao cao, KHONG ghi gi;
  - luon ghi gia tri cu ra file sao luu TRUOC khi ghi 0, ke ca khi chay thu.

File sao luu la thu duy nhat hoan tac duoc thao tac nay. Dung mat no.

VI SAO PHAI DOC LAI SAU KHI GHI
-------------------------------
FINS tra End Code 0 cho lenh ghi ma PLC da nhan, khong phai cho lenh ghi da
co hieu luc. Vung nho bi ladder ghi de lien tuc, hoac dat che do bao ve, se
nhan lenh roi quay ve gia tri cu. Khong doc lai thi khong phan biet duoc
"da reset" voi "tuong la da reset".

CACH DUNG
---------
    python tools/reset_o_chiem.py                      # chay thu toan bo
    python tools/reset_o_chiem.py --block 95           # chay thu 1 block
    python tools/reset_o_chiem.py --block 95 --xac-nhan
    python tools/reset_o_chiem.py --xac-nhan           # reset TOAN BO
    python tools/reset_o_chiem.py --ghi-tat-ca --xac-nhan   # ghi ca o dang 0
"""

import argparse
import os
import re
import socket
import struct
import subprocess
import sys
import time
from datetime import datetime

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WEB_CONFIG = os.path.join(GOC, "TotalParking", "Web.config")

# Ma vung nho cho lenh doc/ghi word. 0x82 = DM (thanh ghi D).
VUNG_DM = 0x82


# --------------------------------------------------------------------------
# Cau hinh: doc tu Web.config de mat khau chi ton tai MOT cho.
# --------------------------------------------------------------------------
def doc_cau_hinh():
    """Tra ve (thong so ket noi mysql, so word moi o)."""
    with open(WEB_CONFIG, "r", encoding="utf-8-sig") as f:
        noi_dung = f.read()

    m = re.search(r'connectionString="([^"]+)"', noi_dung)
    if not m:
        raise SystemExit("Khong tim thay connectionString trong " + WEB_CONFIG)

    truong = {}
    for phan in m.group(1).split(";"):
        if "=" in phan:
            k, v = phan.split("=", 1)
            truong[k.strip().lower()] = v.strip()

    db = {
        "host": truong.get("server", "127.0.0.1"),
        "port": truong.get("port", "3306"),
        "user": truong.get("user id") or truong.get("uid") or "root",
        "password": truong.get("password") or truong.get("pwd") or "",
        "database": truong.get("database", ""),
    }

    # So word moi o: mac dinh 2 (ma the 32 bit). Doc tu appSettings de khop
    # voi thu SlotOccupancyReader dang dung.
    m2 = re.search(r'key="plc:slotWordCount"\s+value="(\d+)"', noi_dung)
    so_word = int(m2.group(1)) if m2 else 2
    if not 1 <= so_word <= 4:
        so_word = 2

    return db, so_word


def tim_mysql():
    """Tim mysql.exe. Khong co driver python nen phai goi client."""
    for goi_y in (
        r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
        r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
    ):
        if os.path.exists(goi_y):
            return goi_y
    import shutil
    duong_dan = shutil.which("mysql")
    if duong_dan:
        return duong_dan
    raise SystemExit("Khong tim thay mysql.exe.")


def truy_van(db, sql):
    """Chay SQL, tra ve list cac dong da tach cot. Bo dong tieu de."""
    lenh = [
        tim_mysql(),
        "-h", db["host"], "-P", db["port"],
        "-u", db["user"],
        "--password=" + db["password"],
        db["database"],
        "--batch", "--raw", "--skip-column-names",
        "-e", sql,
    ]
    ket_qua = subprocess.run(lenh, capture_output=True, text=True)
    if ket_qua.returncode != 0:
        loi = ket_qua.stderr.replace(db["password"], "***")
        raise SystemExit("Truy van that bai: " + loi)
    dong = [d for d in ket_qua.stdout.splitlines() if d.strip()]
    return [d.split("\t") for d in dong]


def nap_ban_do(db, loc_block):
    """Ghep thiet bi PLC voi danh sach o cua no.

    Tra ve dict: block_no -> {thong tin ket noi, danh sach o}
    """
    sql = (
        "SELECT b.block_no, d.ip_address, d.port, d.plc_node, d.pc_node, "
        "       d.timeout_ms, s.slot_index, s.word_addr, "
        "       COALESCE(s.card_code, '') "
        "FROM   plc_device d "
        "JOIN   block b ON b.block_id = d.block_id "
        "JOIN   plc_slot_state s ON s.block_id = d.block_id "
        "WHERE  d.is_active = 1 "
        "ORDER  BY b.block_no, s.slot_index"
    )
    ban_do = {}
    for dong in truy_van(db, sql):
        (block_no, ip, cong, plc_node, pc_node, timeout,
         slot_index, word_addr, card_code) = dong[:9]
        block_no = int(block_no)
        if loc_block and block_no not in loc_block:
            continue
        muc = ban_do.setdefault(block_no, {
            "ip": ip,
            "port": int(cong),
            "plc_node": int(plc_node),
            "pc_node": int(pc_node),
            "timeout_ms": int(timeout),
            "o": [],
        })
        muc["o"].append({
            "slot_index": int(slot_index),
            "word_addr": int(word_addr),
            "card_code_db": card_code,
        })
    return ban_do


# --------------------------------------------------------------------------
# FINS/UDP — dung chung client cua plc_register.py (ban da chay that tren 112
# PLC). Tu 07/10 he thong KHONG noi FINS/TCP voi PLC nua: TCP chi co 3 khe moi
# PLC va phien bo roi lam PLC bao 0x20. UDP khong co phien, nen script bi kill
# giua chung cung khong de lai gi tren PLC, va chay song song voi vong poll cua
# ung dung khong can dung poll.
# --------------------------------------------------------------------------
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plc_register import FinsClient, FinsError   # noqa: E402


class LoiFins(Exception):
    pass


class PlcFins(object):
    """Doc/ghi word DM qua FINS/UDP. Giu ten ham cu (doc_word, ghi_word) de
    phan xu ly ben duoi khong phai sua.

    pc_node / plc_node duoc nhan nhung bo qua: FINS/UDP suy node tu IP."""

    def __init__(self, ip, cong, pc_node, plc_node, timeout_ms):
        self._c = FinsClient(ip, cong, timeout_ms=max(timeout_ms, 1000), retries=1)

    def __enter__(self):
        try:
            self._c.connect()
        except (FinsError, OSError) as ex:
            self._c.close()
            raise LoiFins(str(ex))
        return self

    def __exit__(self, *_):
        self._c.close()

    def doc_word(self, dia_chi, so_luong):
        try:
            return self._c.read_words(dia_chi, so_luong)
        except FinsError as ex:
            raise LoiFins(str(ex))

    def ghi_word(self, dia_chi, gia_tri):
        if not gia_tri:
            return
        try:
            self._c.write_words(dia_chi, list(gia_tri))
        except FinsError as ex:
            raise LoiFins(str(ex))


# --------------------------------------------------------------------------
# Xu ly chinh
# --------------------------------------------------------------------------
def hex_word(words):
    return " ".join("%04X" % w for w in words)


def xu_ly_block(block_no, cau_hinh, so_word, ghi_that, ghi_tat_ca, sao_luu, pc_node):
    """Reset mot block. Tra ve dict thong ke.

    Mot PLC hong KHONG duoc lam hong ca luot chay -> loi duoc bat o day va
    tra ve, khong nem len tren.
    """
    tk = {"block_no": block_no, "o_xet": 0, "o_co_the": 0,
          "o_da_ghi": 0, "o_that_bai": 0, "loi": None, "chi_tiet": []}

    try:
        # FINS/UDP: khong bat tay, khong chiem khe; pc_node bi bo qua.
        with PlcFins(cau_hinh["ip"], cau_hinh["port"], pc_node,
                     cau_hinh["plc_node"], cau_hinh["timeout_ms"]) as plc:
            for o in cau_hinh["o"]:
                tk["o_xet"] += 1
                dia_chi = o["word_addr"]
                nhan = "block %d o %d D%d" % (block_no, o["slot_index"], dia_chi)

                try:
                    cu = plc.doc_word(dia_chi, so_word)
                except LoiFins as ex:
                    tk["o_that_bai"] += 1
                    tk["chi_tiet"].append((nhan, "DOC LOI: %s" % ex))
                    continue

                co_the = any(w != 0 for w in cu)
                if co_the:
                    tk["o_co_the"] += 1

                # Ghi sao luu TRUOC khi ghi 0, ke ca khi chay thu. Day la ban
                # duy nhat hoan tac duoc.
                sao_luu.write("%s\tD%d\t%s\t%s\n" % (
                    nhan, dia_chi, hex_word(cu), o["card_code_db"]))

                if not co_the and not ghi_tat_ca:
                    continue
                if not ghi_that:
                    tk["chi_tiet"].append((nhan, "SE GHI 0 (dang chay thu), cu = %s"
                                           % hex_word(cu)))
                    continue

                try:
                    plc.ghi_word(dia_chi, [0] * so_word)
                    # Doc lai: End Code 0 chi noi PLC NHAN lenh, khong noi lenh
                    # da co hieu luc.
                    moi = plc.doc_word(dia_chi, so_word)
                except LoiFins as ex:
                    tk["o_that_bai"] += 1
                    tk["chi_tiet"].append((nhan, "GHI LOI: %s" % ex))
                    continue

                if any(w != 0 for w in moi):
                    tk["o_that_bai"] += 1
                    tk["chi_tiet"].append(
                        (nhan, "GHI KHONG AN: sau khi ghi van con %s" % hex_word(moi)))
                else:
                    tk["o_da_ghi"] += 1
                    tk["chi_tiet"].append((nhan, "da reset, cu = %s" % hex_word(cu)))

    except (socket.error, OSError, LoiFins) as ex:
        tk["loi"] = str(ex)

    return tk


def main():
    bo_doc = argparse.ArgumentParser(
        description="Reset thanh ghi chiem o cua block co khi ve 0.",
        formatter_class=argparse.RawDescriptionHelpFormatter)
    bo_doc.add_argument("--block", help="Chi lam nhung block nay, vd: 95 hoac 12,95,101")
    bo_doc.add_argument("--xac-nhan", action="store_true",
                        help="GHI THAT. Khong co co nay thi chi doc va bao cao.")
    bo_doc.add_argument("--ghi-tat-ca", action="store_true",
                        help="Ghi 0 ca nhung o dang la 0. Mac dinh chi ghi o co the.")
    bo_doc.add_argument("--sao-luu", help="Duong dan file sao luu gia tri cu.")
    bo_doc.add_argument("--pc-node", type=int, default=0,
                        help="BO QUA — giu lai de lenh cu khong loi. FINS/UDP suy "
                             "node tu IP (octet cuoi IP may nay).")
    tham_so = bo_doc.parse_args()

    loc_block = None
    if tham_so.block:
        loc_block = {int(x) for x in tham_so.block.replace(" ", "").split(",") if x}

    db, so_word = doc_cau_hinh()
    ban_do = nap_ban_do(db, loc_block)
    if not ban_do:
        raise SystemExit("Khong co block nao khop dieu kien.")

    duong_sao_luu = tham_so.sao_luu or os.path.join(
        GOC, "tools", "sao_luu_o_chiem_%s.tsv" % datetime.now().strftime("%Y%m%d_%H%M%S"))

    che_do = "GHI THAT" if tham_so.xac_nhan else "CHAY THU (khong ghi gi)"
    print("=" * 68)
    print("Reset thanh ghi chiem o  --  %s" % che_do)
    print("So block: %d | so word moi o: %d" % (len(ban_do), so_word))
    print("File sao luu: %s" % duong_sao_luu)
    print("=" * 68)

    bat_dau = time.time()
    tong = {"block": 0, "block_loi": 0, "o_xet": 0, "o_co_the": 0,
            "o_da_ghi": 0, "o_that_bai": 0}
    that_bai = []

    with open(duong_sao_luu, "w", encoding="utf-8") as sl:
        sl.write("# gia tri thanh ghi TRUOC khi reset, %s\n" % datetime.now().isoformat())
        sl.write("# nhan\tthanh_ghi\tgia_tri_cu_hex\tcard_code_trong_db\n")

        for block_no in sorted(ban_do):
            tk = xu_ly_block(block_no, ban_do[block_no], so_word,
                             tham_so.xac_nhan, tham_so.ghi_tat_ca, sl,
                             tham_so.pc_node)
            tong["block"] += 1
            if tk["loi"]:
                tong["block_loi"] += 1
                that_bai.append((block_no, ban_do[block_no]["ip"], tk["loi"]))
                print("block %-4d %-15s KHONG KET NOI: %s"
                      % (block_no, ban_do[block_no]["ip"], tk["loi"]))
                continue

            for k in ("o_xet", "o_co_the", "o_da_ghi", "o_that_bai"):
                tong[k] += tk[k]

            if tk["chi_tiet"]:
                print("block %-4d %-15s  %d o, %d co the"
                      % (block_no, ban_do[block_no]["ip"], tk["o_xet"], tk["o_co_the"]))
                for nhan, ghi_chu in tk["chi_tiet"]:
                    print("    %-24s %s" % (nhan, ghi_chu))

    print("=" * 68)
    print("block chay xong      : %d" % tong["block"])
    print("block khong ket noi  : %d" % tong["block_loi"])
    print("o da xet             : %d" % tong["o_xet"])
    print("o dang giu the       : %d" % tong["o_co_the"])
    print("o da reset ve 0      : %d" % tong["o_da_ghi"])
    print("o that bai           : %d" % tong["o_that_bai"])
    print("thoi gian            : %.1fs" % (time.time() - bat_dau))
    print("sao luu              : %s" % duong_sao_luu)

    if not tham_so.xac_nhan:
        print()
        print("Day la CHAY THU, chua ghi gi xuong PLC.")
        print("Chay that: them --xac-nhan")

    # Ma thoat khac 0 khi co that bai -> goi tu script khac biet duoc.
    return 1 if (tong["block_loi"] or tong["o_that_bai"]) else 0


if __name__ == "__main__":
    sys.exit(main())
