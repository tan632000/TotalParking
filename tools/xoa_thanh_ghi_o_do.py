#!/usr/bin/env python3
"""CONG CU NGHIEM THU — xoa ma the o do tren toan bo PLC de test lai tu dau.

    KHONG PHAI MOT DUONG VAN HANH. Chi chay khi co nguoi go tay, trong dot
    nghiem thu, sau khi da xac nhan KHONG CO XE THAT trong bai.

VI SAO NGUY HIEM

    D200-D209, D300-D309, D400-D401 giu ma the cua tung o do va do LADDER so
    huu. Ghi 0 xuong lam o dang co xe trong nhu da trong; ladder co the xep xe
    khac vao dung o do -> VA CHAM XE THAT.

    Vi vay mac dinh script chi DOC va in ra se ghi gi. Phai them --that moi
    ghi, va --that doi ban da xac nhan bai trong.

XOA DUNG 20 WORD, KHONG XOA CA DAI

    o 1      -> D400-D401
    o 2..5   -> D202-D209
    o 6..10  -> D300-D309

    D402 la phan loai tai trong, D210/D211/D310/D311 duoc doc kem nhung khong
    phai o do. Xoa ca dai D400-D411 se mat nhung gia tri do.

    Moi khoi chi xoa dung so o cua no (block.slot_count la 3, 5, 6 hoac 10).
    Khoi 3 o khong co gi o D300; ghi xuong do la ghi vao vung khong thuoc ve no.

CACH CHAY

    python tools/xoa_thanh_ghi_o_do.py              # doc thu, khong ghi gi
    python tools/xoa_thanh_ghi_o_do.py --that       # ghi that
    python tools/xoa_thanh_ghi_o_do.py --block 21   # chi mot khoi

    Sau khi xoa, bo quet o do (plc:slotScanEnabled, nhip 45 giay) tu dong dua
    plc_slot_state ve rong. Khong can sua CSDL bang tay.
"""

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plc_register import FinsClient, read_db_config, find_mysql_exe

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
THU_MUC_LUU = os.path.join(GOC, "TotalParking", "App_Data", "backup")
NHAT_KY = os.path.join(GOC, "TotalParking", "App_Data", "plc_manual_write.log")


def dai_can_xoa(slot_count):
    """Tra ve [(dia_chi_dau, so_word)] cho dung so o cua khoi."""
    dai = []
    if slot_count >= 1:
        dai.append((400, 2))                              # o 1
    if slot_count >= 2:
        dai.append((202, 2 * (min(slot_count, 5) - 1)))   # o 2..5
    if slot_count >= 6:
        dai.append((300, 2 * (slot_count - 5)))           # o 6..10
    return dai


def doc_danh_sach_khoi(block_no=None):
    cfg = read_db_config()
    loc = "AND b.block_no = %d " % block_no if block_no else ""
    sql = ("SELECT b.block_no, b.slot_count, d.ip_address, d.port, "
           "       d.pc_node, d.plc_node, d.timeout_ms, "
           "       COALESCE(d.is_connected, -1) "
           "FROM   plc_device d JOIN block b ON b.block_id = d.block_id "
           "WHERE  d.is_active = 1 " + loc +
           "ORDER  BY b.block_no;")
    env = dict(os.environ, MYSQL_PWD=cfg["password"])
    out = subprocess.run(
        [find_mysql_exe(), "-u", cfg["user"], "-h", cfg["host"],
         "-P", cfg["port"], cfg["db"], "-N", "-B", "-e", sql],
        capture_output=True, text=True, env=env, timeout=30)
    if out.returncode != 0:
        sys.exit("Khong doc duoc danh sach khoi: " + out.stderr.strip())

    ds = []
    for dong in out.stdout.splitlines():
        if not dong.strip():
            continue
        c = dong.split("\t")
        ds.append({
            "block_no": int(c[0]), "slot_count": int(c[1]), "ip": c[2],
            "port": int(c[3]), "pc_node": int(c[4]), "plc_node": int(c[5]),
            "timeout_ms": int(c[6]), "is_connected": int(c[7]),
        })
    return ds


def ghi_nhat_ky(dong):
    os.makedirs(os.path.dirname(NHAT_KY), exist_ok=True)
    with open(NHAT_KY, "a", encoding="utf-8") as f:
        f.write(dong + "\n")


def xu_ly_mot_khoi(k, that):
    """Doc truoc, ghi 0, doc lai. Tra ve (ban_ghi, so_word_con_khac_0, loi)."""
    ban_ghi = {"block_no": k["block_no"], "slot_count": k["slot_count"],
               "ip": k["ip"], "truoc": {}, "sau": {}}
    con_khac_0 = 0

    # retries=1: khoi chet phai bo qua nhanh, khong treo ca luot chay.
    with FinsClient(k["ip"], k["port"], k["pc_node"], k["plc_node"],
                    k["timeout_ms"], retries=1) as plc:
        for dau, so in dai_can_xoa(k["slot_count"]):
            truoc = plc.read_words(dau, so)
            ban_ghi["truoc"]["D%d" % dau] = list(truoc)

            if not that:
                if any(v != 0 for v in truoc):
                    con_khac_0 += sum(1 for v in truoc if v != 0)
                continue

            plc.write_words(dau, [0] * so)
            sau = plc.read_words(dau, so)
            ban_ghi["sau"]["D%d" % dau] = list(sau)
            con_khac_0 += sum(1 for v in sau if v != 0)

            ghi_nhat_ky("%s\t%s\tblock=%d\tD%d..D%d\t%s -> %s\t%s\txoa o do de test lai"
                        % (datetime.now().strftime("%Y-%m-%d %H:%M:%S"), k["ip"],
                           k["block_no"], dau, dau + so - 1,
                           list(truoc), list(sau),
                           "OK" if all(v == 0 for v in sau) else "FAIL"))

    return ban_ghi, con_khac_0


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--that", action="store_true",
                   help="Ghi that. Khong co co nay thi chi doc va bao cao.")
    p.add_argument("--block", type=int, help="Chi xu ly mot khoi.")
    args = p.parse_args()

    khoi = doc_danh_sach_khoi(args.block)
    if not khoi:
        sys.exit("Khong co khoi nao khop.")

    print("Che do: %s | %d khoi (%d dang ket noi)\n"
          % ("GHI THAT" if args.that else "DOC THU (khong ghi gi)",
             len(khoi), sum(1 for k in khoi if k["is_connected"] == 1)))

    luu, loi, con_du, da_sach = [], [], [], 0
    for k in khoi:
        try:
            ban_ghi, khac_0 = xu_ly_mot_khoi(k, args.that)
            luu.append(ban_ghi)
            if khac_0:
                con_du.append((k["block_no"], khac_0))
                print("  block %-4d slot_count=%-3d %s"
                      % (k["block_no"], k["slot_count"],
                         ("con %d word khac 0" % khac_0) if args.that
                         else ("co %d word can xoa" % khac_0)))
            else:
                da_sach += 1
        except Exception as e:
            loi.append((k["block_no"], str(e)[:80]))

    os.makedirs(THU_MUC_LUU, exist_ok=True)
    ten = os.path.join(THU_MUC_LUU, "thanh_ghi_o_do_%s.json"
                       % datetime.now().strftime("%Y%m%d_%H%M%S"))
    with open(ten, "w", encoding="utf-8") as f:
        json.dump(luu, f, ensure_ascii=False, indent=1)

    print("\n%d khoi da sach, %d khoi %s, %d khoi khong doc duoc"
          % (da_sach, len(con_du),
             "VAN CON DU LIEU" if args.that else "con du lieu can xoa", len(loi)))
    for b, e in loi:
        print("  loi block %d: %s" % (b, e))
    print("Sao luu gia tri truoc khi ghi: %s" % ten)

    if not args.that:
        print("\nDay moi la DOC THU. Them --that de ghi, sau khi chac chan bai khong co xe.")

    # Con du lieu sau khi ghi that la that bai co that, phai thay duoc tu ma thoat.
    sys.exit(1 if (args.that and con_du) or loi else 0)


if __name__ == "__main__":
    main()
