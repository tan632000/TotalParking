#!/usr/bin/env python3
"""CONG CU TEST — doc/ghi thanh ghi DM cua PLC Omron qua FINS/UDP.

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

Khung tin chuyen the tu TotalParking/Services/Plc/OmronFinsUdpClient.cs — ban
da chay that tren toan bo 112 PLC. Tu 07/10 he thong KHONG noi FINS/TCP voi PLC
nua (TCP chi co 3 khe/PLC, phien bo roi lam PLC bao 0x20); script nay cung vay.

AN TOAN
    Mac dinh chi cho ghi D1000 (so block tra loi tim xe). Moi thanh ghi khac
    phai them --force. Rieng vung o do thi CHAN HAN, xem DANGER_RANGES.

    Ly do: D200-D211, D300-D311, D400-D411 la thanh ghi ma the tung o do, do
    ladder so huu. Ghi 0 xuong lam o trong mat SCADA trong nhu da trong, ladder
    co the xep xe khac vao — VA CHAM XE THAT. Chi ghi khi co nguoi ra tan noi
    xac nhan o trong.

KHONG tu dong hoa script nay trong vong lap. Qua UDP, script va vong poll cua
site dung hai socket khac cong nen khong lam lech khung tin cua nhau (do 05/10:
hai nguon cung SA1, 100/100 dung) — nhung ghi tay van co the dam vao gia tri
ma vong poll dang quan ly.
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

# Ma vung nho khi truy cap theo WORD. Khac han ma khi truy cap theo BIT.
# Chuyen the tu OmronFinsUdpClient.WordAreaCode.
AREA_WORD = {"D": 0x82, "DM": 0x82, "CIO": 0xB0, "W": 0xB1, "WR": 0xB1,
             "H": 0xB2, "HR": 0xB2, "A": 0xB3, "AR": 0xB3}

# Thanh ghi ghi duoc ma khong can co gi them.
SAFE_TO_WRITE = {1000}       # D1000 = so block tra loi tim xe (SCADA -> PLC)

# Vung o do: ghi sai o day lam ladder xep chong xe. Chan han, khong co --force
# nao mo duoc; phai dung co rieng --xac-nhan-o-trong.
DANGER_RANGES = [(200, 211), (300, 311), (400, 411)]


# --------------------------------------------------------------------------
# FINS/UDP
# --------------------------------------------------------------------------

class FinsError(Exception):
    pass


class FinsClient:
    """Mot socket FINS/UDP toi mot PLC. Dung nhu context manager.

    Khong co phien, khong co bat tay, nen khong chiem khe nao cua PLC — script
    bi kill giua chung cung khong de lai gi tren PLC.

    Node FINS suy tu IP, KHONG tu tham so (do tren CP2E tai bai): DA1 = octet
    cuoi IP PLC, SA1 = octet cuoi IP may gui. pc_node / plc_node van nhan de cac
    script cu goi khong phai sua, nhung bi bo qua — gia tri DB la cua thoi TCP va
    PLC tu choi neu dung (End Code 9005 / 2108)."""

    def __init__(self, ip, port=9600, pc_node=0, plc_node=1, timeout_ms=3000,
                 retries=4):
        self.ip = ip
        self.port = port
        self.pc_node = 0
        self.plc_node = 0
        self.timeout = timeout_ms / 1000.0
        self.retries = retries
        self.sock = None
        self._sid = 0

    def __enter__(self):
        self.connect_with_retry()
        return self

    def connect_with_retry(self):
        """Thu lai khi PLC chua tra loi (vua khoi dong, mang chap chon)."""
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
        """UDP khong co ket noi that: connect() chi co dinh dia chi dich de socket
        loc goi tu may khac. Doc thu D0 de biet PLC co tra loi."""
        ip = socket.gethostbyname(self.ip)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.settimeout(self.timeout)
        # Cong nguon tam do he dieu hanh cap — KHONG bind 9600. PLC tra loi ve
        # dung cong nguon cua tung goi.
        self.sock.connect((ip, self.port))

        self.plc_node = int(ip.rsplit(".", 1)[1])
        self.pc_node = int(self.sock.getsockname()[0].rsplit(".", 1)[1])
        if not 1 <= self.pc_node <= 254:
            raise FinsError("Octet cuoi IP may nay (%s) khong dung lam node FINS."
                            % self.sock.getsockname()[0])

        self.read_words(0, 1)

    def close(self):
        if self.sock:
            try:
                self.sock.close()
            finally:
                self.sock = None

    def read_words(self, start, count, area="D"):
        """Doc `count` word lien tiep tu <area><start>. Tra list[int] 0..65535."""
        ma_vung = AREA_WORD[area.upper()]
        body = self._transact(0x01, 0x01,
                              struct.pack(">BHBH", ma_vung, start, 0x00, count),
                              resend=True)

        # Du lieu bat dau sau 10 byte header + 2 byte MRC/SRC + 2 byte End Code.
        data = body[14:]
        if len(data) < count * 2:
            raise FinsError("PLC tra ve %d byte du lieu, can %d." % (len(data), count * 2))
        return list(struct.unpack(">%dH" % count, data[:count * 2]))

    def cpu_status(self):
        """CONTROLLER STATUS READ (MRC 06, SRC 01).

        Tra loi cau hoi "ladder co dang chay khong". Day la lenh cua tang truyen
        thong trong CPU, hoat dong doc lap voi chuong trinh — nen doc duoc ca khi
        ladder da dung. Do chinh la luc can no nhat."""
        body = self._transact(0x06, 0x01, b"", resend=True)
        if len(body) < 16:
            raise FinsError("Phan hoi trang thai CPU thieu byte (%d)." % len(body))

        status = body[14]   # 00 = dung, 01 = dang chay
        mode   = body[15]   # 00 = PROGRAM, 02 = MONITOR, 04 = RUN
        ten_mode = {0x00: "PROGRAM (ladder KHONG chay)",
                    0x01: "DEBUG",
                    0x02: "MONITOR (ladder dang chay)",
                    0x04: "RUN (ladder dang chay)"}.get(mode, "khong ro (0x%02X)" % mode)
        loi_nang = (body[16] << 8 | body[17]) if len(body) >= 18 else 0
        loi_nhe  = (body[18] << 8 | body[19]) if len(body) >= 20 else 0
        return {"chay": status == 1, "mode": ten_mode,
                "loi_nang": loi_nang, "loi_nhe": loi_nhe}

    def write_words(self, start, values):
        """Ghi list[int] vao D<start> tro di."""
        count = len(values)
        # Lenh GHI gui dung MOT lan (resend=False): bit yeu cau va D1002 cung do
        # ladder ghi, gui lai co the xoa mat mot luot quet moi.
        self._transact(0x01, 0x02,
                       struct.pack(">BHBH", DM_AREA_WORD, start, 0x00, count)
                       + struct.pack(">%dH" % count, *values),
                       resend=False)

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

    def _transact(self, mrc, src, params, resend):
        """Gui mot lenh FINS/UDP, tra ve ca khung phan hoi.

        resend=True chi cho lenh DOC: toi da 2 lan gui, moi lan mot SID moi va
        cho mot nua thoi han. Lenh GHI gui mot lan, cho tron thoi han."""
        attempts = 2 if resend else 1
        wait = max(0.5, self.timeout / 2) if resend else self.timeout

        # Loi ICMP (vd 10054) toi giua hai lenh nam cho trong socket. Bao loi
        # TRUOC khi gui — gui roi moi phat hien thi mot lenh ghi da toi PLC lai
        # bi bao hong (giong OmronFinsUdpClient). Goi cu con sot cung bo luon.
        self.sock.setblocking(False)
        try:
            while True:
                self.sock.recv(4096)
        except BlockingIOError:
            pass
        except OSError as e:
            raise FinsError("Loi nhan FINS/UDP tu truoc: %s" % e)
        finally:
            self.sock.setblocking(True)

        for _ in range(attempts):
            frame = self._header(mrc, src) + params
            sid = frame[9]
            try:
                self.sock.send(frame)
            except OSError as e:
                raise FinsError("Loi gui FINS/UDP: %s" % e)

            deadline = time.monotonic() + wait
            while True:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    break
                self.sock.settimeout(remaining)
                try:
                    data = self.sock.recv(4096)
                except socket.timeout:
                    break
                except OSError as e:
                    # Vi du 10054: Windows bao ICMP port unreachable.
                    raise FinsError("Loi nhan FINS/UDP: %s" % e)

                if len(data) < 14:
                    raise FinsError("Phan hoi FINS/UDP qua ngan: %d byte." % len(data))
                # SID khac = phan hoi tre cua lan gui truoc — bo qua, cho tiep.
                if data[9] != sid:
                    continue

                main, sub = data[12], data[13]
                if main != 0 or sub != 0:
                    raise FinsError("PLC tra ve End Code 0x%02X%02X." % (main, sub))
                return data

        raise FinsError("PLC khong tra loi qua FINS/UDP sau %d lan gui (%.1fs moi lan)."
                        % (attempts, wait))


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
        description="Doc/ghi thanh ghi DM cua PLC Omron qua FINS/UDP.",
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
                   help="So lan thu doc tham do D0 khi PLC chua tra loi (vua khoi dong, mang chap chon)")

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
        # tra loi, ma gia tri trong DB duoc chinh cho vong poll 500ms.
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
