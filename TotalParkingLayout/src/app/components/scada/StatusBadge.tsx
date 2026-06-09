import type { BlockStatus, AlarmSeverity } from "./mockData";

const BLOCK_STATUS_CONFIG = {
  normal:      { label: { vi: "Bình thường", en: "Normal" },      bg: "#16A34A", text: "white" },
  running:     { label: { vi: "Đang chạy",   en: "Running" },     bg: "#2563EB", text: "white" },
  warning:     { label: { vi: "Cảnh báo",    en: "Warning" },     bg: "#F59E0B", text: "#1E293B" },
  fault:       { label: { vi: "Lỗi",         en: "Fault" },       bg: "#DC2626", text: "white" },
  maintenance: { label: { vi: "Bảo trì",     en: "Maintenance" }, bg: "#7C3AED", text: "white" },
  offline:     { label: { vi: "Offline",     en: "Offline" },     bg: "#6B7280", text: "white" },
};

const ALARM_SEVERITY_CONFIG = {
  critical: { label: { vi: "Nghiêm trọng", en: "Critical" }, bg: "#DC2626", text: "white" },
  major:    { label: { vi: "Cao",          en: "Major" },    bg: "#F59E0B", text: "#1E293B" },
  minor:    { label: { vi: "Trung bình",   en: "Minor" },    bg: "#2563EB", text: "white" },
  info:     { label: { vi: "Thông tin",    en: "Info" },     bg: "#6B7280", text: "white" },
};

export function BlockStatusBadge({ status, lang = "vi" }: { status: BlockStatus; lang?: "vi" | "en" }) {
  const cfg = BLOCK_STATUS_CONFIG[status];
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold"
      style={{ background: cfg.bg, color: cfg.text, fontSize: 11 }}>
      {cfg.label[lang]}
    </span>
  );
}

export function AlarmSeverityBadge({ severity, lang = "vi" }: { severity: AlarmSeverity; lang?: "vi" | "en" }) {
  const cfg = ALARM_SEVERITY_CONFIG[severity];
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold"
      style={{ background: cfg.bg, color: cfg.text, fontSize: 11 }}>
      {cfg.label[lang]}
    </span>
  );
}

export function ConnectionDot({ ok }: { ok: boolean }) {
  return (
    <span className="inline-flex w-2.5 h-2.5 rounded-full"
      style={{ background: ok ? "#16A34A" : "#DC2626", boxShadow: ok ? "0 0 6px #16A34A88" : "0 0 6px #DC262688" }} />
  );
}
