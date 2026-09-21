#!/usr/bin/env python3
"""CONG CU TEST — doc/ghi thanh ghi DM cua PLC Omron qua FINS/TCP.

DAY KHONG PHAI MOT DUONG VAN HANH.

    Luong that van la: camera/the quet -> site TotalParking -> vong poll
    (PlcHost + PlcConnectionManager) -> ghi D1000. Script nay KHONG thay the,
    KHONG bo sung, va KHONG duoc noi vao luong do. Khong co gi goi no; no chi
    chay khi co nguoi go tay.

    Muc dich duy nhat: chan doan va nghiem thu — doc mot thanh ghi de xem PLC
    dang giu gi, hoac cuong buc mot gia tri de thu phan ung cua HMI, ke ca khi
    block dang is_active = 0 nen khong the dung /PlcStatus/Write.

    Sua loi that thi sua trong luong that. Neu thay minh dung script nay de
    ha nhiet mot su co lap di lap lai, do la dau hieu luong that dang hong va
    can sua o do, khong phai o day.

Chay truc tiep, KHONG can site TotalParking chay, KHONG can block o trong
vong poll (is_active co the = 0). Chi dung thu vien chuan cua Python.

    python tools/plc_register.py --block 95 --read D1000
    python tools/plc_register.py --block 95 --write D1000 --value 103
    python tools/plc_register.py --ip 192.169.1.195 --read D100 --count 4

Khung tin duoc chuyen the tu TotalParking/Services/Plc/OmronFinsClient.cs —
ban da chay that tren toan bo 112 PLC, khong phai suy dien tu tai lieu.

AN TOAN
    Mac dinh chi cho ghi D1000 (so block tra loi tim xe). Moi thanh ghi khac
    phai them --force. Rieng vung o do thi CHAN HAN, xem DANGER_RANGES.

    Ly do: D200-D211, D300-D311, D400-D411 la thanh ghi ma the tung o do, do
    ladder so huu. Ghi 0 xuong lam o trong mat SCADA trong nhu da trong, ladder
    co the xep xe khac vao — VA CHAM XE THAT. Chi ghi khi co nguoi ra tan noi
    xac nhan o trong.

KHONG tu dong hoa script nay trong vong lap. Mot khung FINS la mot cap
ghi-roi-doc khong the xen ke; neu block dang nam trong vong poll cua site thi
hai ket noi se lam lech khung tin cua nhau.
"""

import argparse
import os
import re
import socket
import struct
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from datetime import datetime

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WEB_CONFIG = os.path.join(REPO_ROOT, "TotalParking", "Web.config")
AUDIT_LOG = os.path.join(REPO_ROOT, "TotalParking", "App_Data", "plc_manual_write.log")

DM_AREA_WORD = 0x82          # ma vung nho DM khi truy cap theo WORD

# Thanh ghi ghi duoc ma khong can co gi them.
SAFE_TO_WRITE = {1000}       # D1000 = so block tra loi tim xe (SCADA -> PLC)

# Vung o do: ghi sai o day lam ladder xep chong xe. Chan han, khong co --force
# nao mo duoc; phai dung co rieng --xac-nhan-o-trong.
DANGER_RANGES = [(200, 211), (300, 311), (400, 411)]


# --------------------------------------------------------------------------
# FINS/TCP
# --------------------------------------------------------------------------

class FinsError(Exception):
    pass


class FinsClient:
    """Mot ket noi FINS/TCP toi mot PLC. Dung nhu context manager."""

    def __init__(self, ip, port=9600, pc_node=0, plc_node=1, timeout_ms=3000,
                 retries=4):
        self.ip = ip
        self.port = port
        self.pc_node = pc_node
        self.plc_node = plc_node
        self.timeout = timeout_ms / 1000.0
        self.retries = retries
        self.sock = None
        self._sid = 0

    def __enter__(self):
        self.connect_with_retry()
        return self

    def connect_with_retry(self):
        """PLC Omron chi nhan mot so it ket noi FINS/TCP cung luc. Khe cua lan
        truoc can vai giay moi duoc giai phong, nen connect timeout KHONG co
        nghia la PLC chet — thu lai thuong an ngay."""
        last = None
        for attempt in range(1, self.retries + 1):
            try:
                self.connect()
                if attempt > 1:
                    print("  (ket noi duoc o lan thu %d)" % attempt)
                return
            except (OSError, FinsError) as e:
                last = e
                self.close()
                if attempt < self.retries:
                    time.sleep(2.0)
        raise last

    def __exit__(self, *exc):
        self.close()

    def connect(self):
        self.sock = socket.create_connection((self.ip, self.port), self.timeout)
        self.sock.settimeout(self.timeout)

        # Bat tay: 20 byte xin cap node, nhan 24 byte tra loi.
        req = b"FINS" + struct.pack(">IIII", 12, 0, 0, self.pc_node)
        self.sock.sendall(req)
        res = self._read_exact(24, "bat tay")

        err = struct.unpack(">I", res[12:16])[0]
        if err != 0:
            raise FinsError("PLC tu choi bat tay FINS/TCP, ma loi 0x%08X." % err)

        # PLC co quyen cap node khac node ta de nghi — lay theo PLC.
        if res[19] > 0:
            self.pc_node = res[19]
        if res[23] > 0:
            self.plc_node = res[23]

    def close(self):
        if self.sock:
            try:
                self.sock.close()
            finally:
                self.sock = None

    def read_words(self, start, count):
        """Doc `count` word lien tiep tu D<start>. Tra list[int] 0..65535."""
        frame = self._header(0x01, 0x01) + struct.pack(
            ">BHBH", DM_AREA_WORD, start, 0x00, count)
        body = self._transact(frame)

        # Du lieu bat dau sau 10 byte header + 2 byte MRC/SRC + 2 byte End Code.
        data = body[14:]
        if len(data) < count * 2:
            raise FinsError("PLC tra ve %d byte du lieu, can %d." % (len(data), count * 2))
        return list(struct.unpack(">%dH" % count, data[:count * 2]))

    def write_words(self, start, values):
        """Ghi list[int] vao D<start> tro di."""
        count = len(values)
        frame = (self._header(0x01, 0x02)
                 + struct.pack(">BHBH", DM_AREA_WORD, start, 0x00, count)
                 + struct.pack(">%dH" % count, *values))
        self._transact(frame)

    # -- noi bo ------------------------------------------------------------

    def _header(self, mrc, src):
        # SID bo qua 0 de mang moi khoi tao khong trung SID hop le.
        self._sid = 1 if self._sid == 255 else self._sid + 1
        return bytes([
            0x80,           # ICF: command, can phan hoi
            0x00,           # RSV
            0x02,           # GCT
            0x00,           # DNA: mang noi bo
            self.plc_node,  # DA1
            0x00,           # DA2: CPU unit
            0x00,           # SNA
            self.pc_node,   # SA1
            0x00,           # SA2
            self._sid,
            mrc, src,
        ])

    def _transact(self, fins_frame):
        sid = fins_frame[9]
        packet = b"FINS" + struct.pack(">III", 8 + len(fins_frame), 2, 0) + fins_frame
        self.sock.sendall(packet)

        header = self._read_exact(16, "TCP header")
        if header[:4] != b"FINS":
            raise FinsError("Phan hoi khong bat dau bang 'FINS'.")

        tcp_err = struct.unpack(">I", header[12:16])[0]
        if tcp_err != 0:
            raise FinsError("FINS/TCP header bao loi 0x%08X." % tcp_err)

        # Length dem tu byte thu 8 cua goi, nen phan con lai = Length - 8.
        remaining = struct.unpack(">I", header[4:8])[0] - 8
        if not (14 <= remaining <= 4096):
            raise FinsError("Do dai phan hoi FINS khong hop le: %d." % (remaining + 8))

        body = self._read_exact(remaining, "FINS body")

        # SID lech nghia la dang doc phai phan hoi cua luot truoc — ket noi da
        # lech pha, khong dung tiep duoc.
        if body[9] != sid:
            raise FinsError("SID phan hoi (%d) khac SID yeu cau (%d)." % (body[9], sid))

        main, sub = body[12], body[13]
        if main != 0 or sub != 0:
            raise FinsError("PLC tra ve End Code 0x%02X%02X." % (main, sub))
        return body

    def _read_exact(self, count, what):
        """Doc du `count` byte hoac nem loi. Tra ve phan doc do la cach chac
        chan nhat de lam hong ket noi lau dai."""
        buf = b""
        while len(buf) < count:
            try:
                chunk = self.sock.recv(count - len(buf))
            except socket.timeout:
                raise FinsError("Qua thoi gian khi doc %s (%d/%d byte)."
                                % (what, len(buf), count))
            if not chunk:
                raise FinsError("PLC dong ket noi khi dang doc %s (%d/%d byte)."
                                % (what, len(buf), count))
            buf += chunk
        return buf


# --------------------------------------------------------------------------
# Tra cuu cau hinh PLC tu DB
# --------------------------------------------------------------------------

def read_db_config():
    """Lay thong tin ket noi MySQL tu Web.config — khong hard-code mat khau."""
    root = ET.parse(WEB_CONFIG).getroot()
    for node in root.iter("add"):
        cs = node.get("connectionString")
        if cs and "total_parking" in cs:
            parts = dict(
                (k.strip().lower(), v.strip())
                for k, v in (p.split("=", 1) for p in cs.split(";") if "=" in p)
            )
            return {
                "host": parts.get("server", "127.0.0.1"),
                "port": parts.get("port", "3306"),
                "db": parts.get("database", "total_parking"),
                "user": parts.get("user id", "root"),
                "password": parts.get("password", ""),
            }
    raise RuntimeError("Khong tim thay connectionString trong " + WEB_CONFIG)


def find_mysql_exe():
    for path in (os.environ.get("MYSQL_EXE"),
                 r"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
                 r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
                 "mysql"):
        if path and (path == "mysql" or os.path.exists(path)):
            return path
    raise RuntimeError("Khong tim thay mysql.exe. Dat bien moi truong MYSQL_EXE.")


def lookup_plc(block_no):
    """Tra ip/port/node cua mot block. Khong loc is_active: block tat van ghi duoc."""
    cfg = read_db_config()
    sql = ("SELECT d.ip_address, d.port, d.plc_node, d.pc_node, d.timeout_ms, d.is_active "
           "FROM plc_device d JOIN block b ON b.block_id = d.block_id "
           "WHERE b.block_no = %d;" % block_no)

    env = dict(os.environ, MYSQL_PWD=cfg["password"])
    out = subprocess.run(
        [find_mysql_exe(), "-u", cfg["user"], "-h", cfg["host"],
         "-P", cfg["port"], cfg["db"], "-N", "-B", "-e", sql],
        capture_output=True, text=True, env=env, timeout=15)

    if out.returncode != 0:
        raise RuntimeError("Truy van DB that bai: " + out.stderr.strip())
    line = out.stdout.strip()
    if not line:
        raise RuntimeError("Khong co PLC nao cho block %d." % block_no)

    ip, port, plc_node, pc_node, timeout_ms, is_active = line.split("\t")
    return {
        "ip": ip, "port": int(port),
        "plc_node": int(plc_node), "pc_node": int(pc_node),
        "timeout_ms": int(timeout_ms), "is_active": is_active == "1",
    }


# --------------------------------------------------------------------------
# Hang rao an toan
# --------------------------------------------------------------------------

def parse_register(text):
    """'D1000' hoac '1000' -> 1000."""
    m = re.fullmatch(r"[Dd]?(\d+)", text.strip())
    if not m:
        raise argparse.ArgumentTypeError("Thanh ghi phai dang D1000 hoac 1000: " + text)
    addr = int(m.group(1))
    if not (0 <= addr <= 65535):
        raise argparse.ArgumentTypeError("Dia chi phai trong 0..65535: " + text)
    return addr


def in_danger_zone(addr, count):
    """True neu bat ky word nao trong dai cham vung o do."""
    for a in range(addr, addr + count):
        if any(lo <= a <= hi for lo, hi in DANGER_RANGES):
            return True
    return False


def check_write_allowed(addr, count, args):
    if in_danger_zone(addr, count):
        if not args.xac_nhan_o_trong:
            sys.exit(
                "TU CHOI: D%d..D%d cham vung thanh ghi o do (D200-D211, D300-D311,\n"
                "         D400-D411). Day la ma the tung o do, do ladder so huu.\n"
                "         Ghi sai -> ladder xep chong xe -> VA CHAM XE THAT.\n\n"
                "         Chi chay lai voi --xac-nhan-o-trong SAU KHI co nguoi ra\n"
                "         tan noi nhin thay o do trong."
                % (addr, addr + count - 1))
        return

    if addr in SAFE_TO_WRITE and count == 1:
        return

    if not args.force:
        sys.exit(
            "TU CHOI: D%d khong nam trong danh sach an toan (D1000).\n"
            "         Them --force neu ban chac chan biet thanh ghi nay lam gi.\n"
            "         Tham khao: D100-D101 ma the, D402 phan loai,\n"
            "         D1002-D1003 ma the tim xe, D1000 so block tra loi."
            % addr)


def write_audit(ip, block_no, addr, before, after, ok, note):
    os.makedirs(os.path.dirname(AUDIT_LOG), exist_ok=True)
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    with open(AUDIT_LOG, "a", encoding="utf-8") as f:
        f.write("%s\t%s\tblock=%s\tD%d\t%s -> %s\t%s\t%s\n"
                % (stamp, ip, block_no, addr, before, after,
                   "OK" if ok else "FAIL", note))


# --------------------------------------------------------------------------

def main():
    p = argparse.ArgumentParser(
        description="Doc/ghi thanh ghi DM cua PLC Omron qua FINS/TCP.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__)

    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--block", type=int, help="So block, tra ip/port tu DB")
    src.add_argument("--ip", help="IP PLC, dung truc tiep khong qua DB")

    p.add_argument("--port", type=int, default=9600)
    p.add_argument("--plc-node", type=int, default=1)
    p.add_argument("--pc-node", type=int, default=0)
    p.add_argument("--timeout-ms", type=int, default=3000)
    p.add_argument("--retries", type=int, default=4,
                   help="So lan thu ket noi lai (PLC gioi han so ket noi dong thoi)")

    act = p.add_mutually_exclusive_group(required=True)
    act.add_argument("--read", type=parse_register, metavar="D1000")
    act.add_argument("--write", type=parse_register, metavar="D1000")

    p.add_argument("--count", type=int, default=1, help="So word khi --read")
    p.add_argument("--value", type=int, help="Gia tri 0..65535 khi --write")
    p.add_argument("--force", action="store_true",
                   help="Cho ghi thanh ghi ngoai danh sach an toan")
    p.add_argument("--xac-nhan-o-trong", action="store_true",
                   dest="xac_nhan_o_trong",
                   help="Da co nguoi xac nhan tan noi o do trong (vung D200/D300/D400)")

    args = p.parse_args()

    # Nguon cau hinh: DB theo block, hoac tham so dong lenh.
    if args.block is not None:
        try:
            cfg = lookup_plc(args.block)
        except Exception as e:
            sys.exit("Loi tra cuu DB: %s" % e)
        if not cfg["is_active"]:
            print("Luu y: block %d dang is_active = 0 (khong nam trong vong poll "
                  "cua site). Ghi truc tiep van duoc." % args.block)
        # timeout_ms tren dong lenh de cao hon DB: co PLC cham toi vai giay moi
        # nhan TCP, ma gia tri trong DB duoc chinh cho vong poll 500ms.
        if args.timeout_ms != p.get_default("timeout_ms"):
            cfg["timeout_ms"] = args.timeout_ms
        block_label = str(args.block)
    else:
        cfg = {"ip": args.ip, "port": args.port, "plc_node": args.plc_node,
               "pc_node": args.pc_node, "timeout_ms": args.timeout_ms}
        block_label = "-"

    if args.read is not None and not (1 <= args.count <= 64):
        sys.exit("--count phai trong 1..64.")
    if args.write is not None:
        if args.value is None:
            sys.exit("--write can di kem --value.")
        if not (0 <= args.value <= 65535):
            sys.exit("--value phai trong 0..65535.")
        check_write_allowed(args.write, 1, args)

    print("PLC %s:%d (block %s)" % (cfg["ip"], cfg["port"], block_label))

    try:
        with FinsClient(cfg["ip"], cfg["port"], cfg["pc_node"],
                        cfg["plc_node"], cfg["timeout_ms"], args.retries) as plc:

            if args.read is not None:
                words = plc.read_words(args.read, args.count)
                for i, w in enumerate(words):
                    print("  D%-6d = %-6d  0x%04X" % (args.read + i, w, w))
                return

            # Ghi: doc truoc, ghi, doc lai. Khong bao "xong" neu chua doc lai.
            before = plc.read_words(args.write, 1)[0]
            plc.write_words(args.write, [args.value])
            after = plc.read_words(args.write, 1)[0]

            ok = after == args.value
            print("  D%d: %d -> %d   %s"
                  % (args.write, before, after, "OK" if ok else "LECH!"))
            write_audit(cfg["ip"], block_label, args.write, before, after,
                        ok, "ghi tay bang tools/plc_register.py")
            if not ok:
                sys.exit("Doc lai ra %d, khong phai %d. PLC co the dang bi ladder "
                         "ghi de." % (after, args.value))

    except FinsError as e:
        if args.write is not None:
            write_audit(cfg["ip"], block_label, args.write, "?", args.value,
                        False, str(e))
        sys.exit("Loi FINS: %s" % e)
    except OSError as e:
        sys.exit("Loi mang toi %s:%d - %s" % (cfg["ip"], cfg["port"], e))


if __name__ == "__main__":
    main()
