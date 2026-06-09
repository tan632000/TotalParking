import { useState, useEffect } from "react";
import { AlertTriangle, Globe, LogOut, Wifi, WifiOff } from "lucide-react";
import { CONNECTION_STATUS } from "./mockData";

type SystemStatus = "normal" | "warning" | "fault" | "maintenance";
interface HeaderProps {
  systemStatus: SystemStatus;
  lang: "vi" | "en";
  onLangToggle: () => void;
  activeAlarms: number;
}

const STATUS_CONFIG = {
  normal:      { label: { vi: "Bình thường", en: "Normal" },      color: "bg-[#16A34A]", pulse: false },
  warning:     { label: { vi: "Cảnh báo",    en: "Warning" },     color: "bg-[#F59E0B]", pulse: true },
  fault:       { label: { vi: "Lỗi hệ thống", en: "Fault" },      color: "bg-[#DC2626]", pulse: true },
  maintenance: { label: { vi: "Bảo trì",     en: "Maintenance" }, color: "bg-[#7C3AED]", pulse: false },
};

export function Header({ systemStatus, lang, onLangToggle, activeAlarms }: HeaderProps) {
  const [time, setTime] = useState(new Date());
  useEffect(() => {
    const t = setInterval(() => setTime(new Date()), 1000);
    return () => clearInterval(t);
  }, []);

  const cfg = STATUS_CONFIG[systemStatus];
  const t = (vi: string, en: string) => lang === "vi" ? vi : en;

  return (
    <header className="h-14 flex items-center px-4 gap-4 border-b border-[#334155]" style={{ background: "#1E293B" }}>
      {/* Logo & Title */}
      <div className="flex items-center gap-3 min-w-0">
        <div className="w-8 h-8 rounded flex items-center justify-center shrink-0" style={{ background: "#2563EB" }}>
          <span style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700 }}>TP</span>
        </div>
        <div className="hidden md:block">
          <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700, lineHeight: 1.2 }}>TotalParking</div>
          <div style={{ color: "#94A3B8", fontSize: 11, lineHeight: 1.2 }}>LUMI Automated Parking SCADA</div>
        </div>
      </div>

      <div className="h-6 w-px bg-[#334155] mx-1 hidden md:block" />

      {/* System Status */}
      <div className={`flex items-center gap-2 px-3 py-1 rounded ${cfg.color}`}>
        {cfg.pulse && (
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full opacity-75" style={{ background: "white" }} />
            <span className="relative inline-flex rounded-full h-2 w-2" style={{ background: "white" }} />
          </span>
        )}
        {!cfg.pulse && <span className="w-2 h-2 rounded-full bg-white opacity-90" />}
        <span style={{ color: "white", fontSize: 12, fontWeight: 600 }}>{cfg.label[lang]}</span>
      </div>

      {/* Active Alarms indicator */}
      {activeAlarms > 0 && (
        <div className="flex items-center gap-1.5 px-2 py-1 rounded animate-pulse" style={{ background: "#DC2626" }}>
          <AlertTriangle size={13} color="white" />
          <span style={{ color: "white", fontSize: 12, fontWeight: 600 }}>{activeAlarms} {t("Alarm", "Alarms")}</span>
        </div>
      )}

      <div className="flex-1" />

      {/* Connections */}
      <div className="hidden lg:flex items-center gap-3">
        {[
          { key: "PLC", ok: CONNECTION_STATUS.plcBlock },
          { key: "HMI", ok: CONNECTION_STATUS.hmiBlock },
          { key: "AI-VDS", ok: CONNECTION_STATUS.aiVds },
          { key: "LED/PGS", ok: CONNECTION_STATUS.ledPgs },
          { key: "SCADA", ok: CONNECTION_STATUS.scadaServer },
        ].map(c => (
          <div key={c.key} className="flex items-center gap-1">
            {c.ok
              ? <Wifi size={12} color="#16A34A" />
              : <WifiOff size={12} color="#DC2626" />}
            <span style={{ fontSize: 11, color: c.ok ? "#86EFAC" : "#FCA5A5" }}>{c.key}</span>
          </div>
        ))}
      </div>

      <div className="h-6 w-px bg-[#334155] mx-1 hidden lg:block" />

      {/* Time */}
      <div className="text-right hidden sm:block">
        <div style={{ color: "#F8FAFC", fontSize: 15, fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
          {time.toLocaleTimeString("vi-VN")}
        </div>
        <div style={{ color: "#94A3B8", fontSize: 11 }}>
          {time.toLocaleDateString("vi-VN", { weekday: "short", day: "2-digit", month: "2-digit", year: "numeric" })}
        </div>
      </div>

      <div className="h-6 w-px bg-[#334155] mx-1 hidden sm:block" />

      {/* User */}
      <div className="hidden sm:flex items-center gap-2">
        <div className="w-7 h-7 rounded-full flex items-center justify-center" style={{ background: "#2563EB" }}>
          <span style={{ color: "white", fontSize: 11, fontWeight: 700 }}>NV</span>
        </div>
        <div>
          <div style={{ color: "#F8FAFC", fontSize: 11, fontWeight: 600 }}>Nguyễn Văn A</div>
          <div style={{ color: "#94A3B8", fontSize: 10 }}>Operator</div>
        </div>
      </div>

      {/* Lang & Logout */}
      <button onClick={onLangToggle}
        className="flex items-center gap-1 px-2 py-1 rounded hover:bg-[#334155] transition-colors"
        style={{ color: "#94A3B8", fontSize: 12 }}>
        <Globe size={14} />
        <span>{lang === "vi" ? "EN" : "VI"}</span>
      </button>

      <button className="flex items-center gap-1 px-2 py-1 rounded hover:bg-[#334155] transition-colors"
        style={{ color: "#94A3B8", fontSize: 12 }}>
        <LogOut size={14} />
        <span className="hidden sm:inline">{t("Đăng xuất", "Logout")}</span>
      </button>
    </header>
  );
}
