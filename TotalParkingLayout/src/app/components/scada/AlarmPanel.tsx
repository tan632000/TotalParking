import { useState } from "react";
import { Filter, Download, CheckCheck, X, AlertTriangle, Info } from "lucide-react";
import { ALARMS, type Alarm, type AlarmSeverity } from "./mockData";
import { AlarmSeverityBadge } from "./StatusBadge";

interface AlarmPanelProps { lang: "vi" | "en" }
const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

function AlarmDetail({ alarm, lang, onClose }: { alarm: Alarm; lang: "vi" | "en"; onClose: () => void }) {
  const [showAck, setShowAck] = useState(false);
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4"
      style={{ background: "rgba(0,0,0,0.7)" }}>
      <div className="w-full max-w-lg rounded-xl overflow-hidden"
        style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div className="flex items-center justify-between px-5 py-4 border-b" style={{ borderColor: "#334155" }}>
          <div className="flex items-center gap-2">
            <AlertTriangle size={18} color="#DC2626" />
            <span style={{ color: "#F8FAFC", fontSize: 15, fontWeight: 700 }}>
              {t(lang, "Chi tiết Alarm", "Alarm Detail")}
            </span>
          </div>
          <button onClick={onClose} className="p-1 rounded hover:bg-[#334155]"><X size={16} color="#94A3B8" /></button>
        </div>
        <div className="p-5 space-y-3">
          <div className="flex gap-2 flex-wrap">
            <AlarmSeverityBadge severity={alarm.severity} lang={lang} />
            <span className="px-2 py-0.5 rounded text-xs"
              style={{ background: "#334155", color: "#CBD5E1" }}>{alarm.code}</span>
            <span className="px-2 py-0.5 rounded text-xs"
              style={{ background: alarm.status === "active" ? "#DC262622" : "#16A34A22",
                       color: alarm.status === "active" ? "#FCA5A5" : "#86EFAC" }}>
              {alarm.status}
            </span>
          </div>

          {[
            { label: t(lang,"Thời gian","Time"), value: alarm.time },
            { label: t(lang,"Zone","Zone"), value: alarm.zone },
            { label: t(lang,"Block","Block"), value: alarm.block },
            { label: t(lang,"Thiết bị","Device"), value: alarm.device },
            { label: t(lang,"Thông báo","Message"), value: alarm.message },
            { label: t(lang,"Hướng xử lý","Suggested Action"), value: alarm.suggestedAction },
            ...(alarm.ackUser ? [{ label: t(lang,"Người xác nhận","Ack User"), value: alarm.ackUser }] : []),
            ...(alarm.recoveryTime ? [{ label: t(lang,"Thời gian khôi phục","Recovery"), value: alarm.recoveryTime }] : []),
          ].map(row => (
            <div key={row.label} className="grid grid-cols-3 gap-2">
              <span style={{ color: "#64748B", fontSize: 12 }}>{row.label}</span>
              <span style={{ color: "#F8FAFC", fontSize: 12, gridColumn: "2 / 4" }}>{row.value}</span>
            </div>
          ))}

          <div className="border-t pt-3 flex gap-2" style={{ borderColor: "#334155" }}>
            {alarm.status === "active" && !showAck && (
              <button onClick={() => setShowAck(true)}
                className="flex items-center gap-1.5 px-3 py-2 rounded-lg"
                style={{ background: "#16A34A", color: "white", fontSize: 12 }}>
                <CheckCheck size={14} />
                {t(lang,"Xác nhận (Acknowledge)","Acknowledge")}
              </button>
            )}
            {showAck && (
              <div className="flex-1 rounded-lg p-3" style={{ background: "#0F172A", border: "1px solid #16A34A44" }}>
                <div style={{ color: "#86EFAC", fontSize: 12, marginBottom: 8 }}>
                  {t(lang,"Xác nhận đã tiếp nhận cảnh báo này?","Confirm acknowledgement of this alarm?")}
                </div>
                <div className="flex gap-2">
                  <button onClick={() => { setShowAck(false); onClose(); }}
                    className="px-3 py-1.5 rounded" style={{ background: "#16A34A", color: "white", fontSize: 12 }}>
                    {t(lang,"Xác nhận","Confirm")}
                  </button>
                  <button onClick={() => setShowAck(false)}
                    className="px-3 py-1.5 rounded" style={{ background: "#334155", color: "#CBD5E1", fontSize: 12 }}>
                    {t(lang,"Hủy","Cancel")}
                  </button>
                </div>
              </div>
            )}
            <button className="flex items-center gap-1.5 px-3 py-2 rounded-lg ml-auto"
              style={{ background: "#334155", color: "#CBD5E1", fontSize: 12 }}>
              <Download size={14} />
              {t(lang,"Xuất báo cáo","Export")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export function AlarmPanel({ lang }: AlarmPanelProps) {
  const [selected, setSelected] = useState<Alarm | null>(null);
  const [filter, setFilter] = useState<AlarmSeverity | "all">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "acknowledged" | "cleared">("all");

  const filtered = ALARMS.filter(a =>
    (filter === "all" || a.severity === filter) &&
    (statusFilter === "all" || a.status === statusFilter)
  );

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>
          {t(lang,"Alarm & Event","Alarm & Event")}
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <div className="flex items-center gap-1.5 px-2 py-1.5 rounded-lg" style={{ background: "#1E293B" }}>
            <Filter size={13} color="#94A3B8" />
            <span style={{ color: "#94A3B8", fontSize: 11 }}>{t(lang,"Mức độ:","Severity:")}</span>
            {(["all","critical","major","minor","info"] as const).map(f => (
              <button key={f} onClick={() => setFilter(f)}
                className="px-2 py-0.5 rounded text-xs transition-colors"
                style={{ background: filter === f ? "#2563EB" : "transparent", color: filter === f ? "white" : "#94A3B8" }}>
                {f === "all" ? t(lang,"Tất cả","All") : f.toUpperCase()}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-1.5 px-2 py-1.5 rounded-lg" style={{ background: "#1E293B" }}>
            {(["all","active","acknowledged","cleared"] as const).map(f => (
              <button key={f} onClick={() => setStatusFilter(f)}
                className="px-2 py-0.5 rounded text-xs transition-colors"
                style={{ background: statusFilter === f ? "#2563EB" : "transparent", color: statusFilter === f ? "white" : "#94A3B8" }}>
                {f === "all" ? t(lang,"Tất cả","All") : f === "active" ? t(lang,"Đang lỗi","Active")
                  : f === "acknowledged" ? t(lang,"Đã biết","Ack'd") : t(lang,"Đã xóa","Cleared")}
              </button>
            ))}
          </div>
          <button className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg"
            style={{ background: "#1E293B", color: "#94A3B8", fontSize: 12 }}>
            <Download size={13} />
            Export
          </button>
        </div>
      </div>

      {/* Summary Counts */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {([
          { label: t(lang,"Nghiêm trọng","Critical"), sev: "critical", color: "#DC2626" },
          { label: t(lang,"Cao","Major"), sev: "major", color: "#F59E0B" },
          { label: t(lang,"Trung bình","Minor"), sev: "minor", color: "#2563EB" },
          { label: t(lang,"Thông tin","Info"), sev: "info", color: "#6B7280" },
        ] as const).map(({ label, sev, color }) => {
          const count = ALARMS.filter(a => a.severity === sev).length;
          const active = ALARMS.filter(a => a.severity === sev && a.status === "active").length;
          return (
            <button key={sev} onClick={() => setFilter(filter === sev ? "all" : sev)}
              className="rounded-lg p-3 text-left transition-all hover:scale-[1.02]"
              style={{ background: "#1E293B", border: `1px solid ${filter === sev ? color : "#334155"}` }}>
              <div className="flex items-center gap-2 mb-1">
                <span className="w-2 h-2 rounded-full" style={{ background: color }} />
                <span style={{ color: "#94A3B8", fontSize: 11 }}>{label}</span>
              </div>
              <div style={{ color, fontSize: 22, fontWeight: 700 }}>{count}</div>
              {active > 0 && <div style={{ color: "#FCA5A5", fontSize: 10 }}>{active} active</div>}
            </button>
          );
        })}
      </div>

      {/* Alarm Table */}
      <div className="rounded-lg overflow-hidden" style={{ border: "1px solid #334155" }}>
        <div className="overflow-x-auto">
          <table className="w-full" style={{ borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ background: "#1E293B", borderBottom: "1px solid #334155" }}>
                {[t(lang,"Thời gian","Time"), t(lang,"Mức độ","Sev"), "Zone", "Block",
                  t(lang,"Thiết bị","Device"), "Code", t(lang,"Thông báo","Message"),
                  t(lang,"Trạng thái","Status"), t(lang,"Người xác nhận","Ack User"), ""].map((h, i) => (
                  <th key={i} className="text-left py-2.5 px-3"
                    style={{ color: "#64748B", fontSize: 11, fontWeight: 600, whiteSpace: "nowrap" }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filtered.map((alarm) => (
                <tr key={alarm.id}
                  className="border-b hover:bg-[#1E293B] transition-colors cursor-pointer"
                  style={{ borderColor: "#334155",
                    background: alarm.severity === "critical" && alarm.status === "active" ? "#DC262608" : "#0F172A" }}
                  onClick={() => setSelected(alarm)}>
                  <td className="py-2.5 px-3" style={{ color: "#94A3B8", fontSize: 12, fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>{alarm.time}</td>
                  <td className="py-2.5 px-3"><AlarmSeverityBadge severity={alarm.severity} lang={lang} /></td>
                  <td className="py-2.5 px-3" style={{ color: "#CBD5E1", fontSize: 12 }}>{alarm.zone}</td>
                  <td className="py-2.5 px-3" style={{ color: "#CBD5E1", fontSize: 12, whiteSpace: "nowrap" }}>{alarm.block}</td>
                  <td className="py-2.5 px-3" style={{ color: "#94A3B8", fontSize: 12, whiteSpace: "nowrap" }}>{alarm.device}</td>
                  <td className="py-2.5 px-3">
                    <code style={{ color: "#60A5FA", fontSize: 11 }}>{alarm.code}</code>
                  </td>
                  <td className="py-2.5 px-3 max-w-xs" style={{ color: "#F8FAFC", fontSize: 12 }}>
                    <div className="truncate max-w-[200px]">{alarm.message}</div>
                  </td>
                  <td className="py-2.5 px-3">
                    <span className="px-2 py-0.5 rounded text-xs"
                      style={{
                        background: { active: "#DC262622", acknowledged: "#F59E0B22", cleared: "#16A34A22" }[alarm.status],
                        color: { active: "#FCA5A5", acknowledged: "#FCD34D", cleared: "#86EFAC" }[alarm.status]
                      }}>
                      {alarm.status === "active" ? t(lang,"Đang lỗi","Active")
                        : alarm.status === "acknowledged" ? t(lang,"Đã biết","Ack'd") : t(lang,"Đã xóa","Cleared")}
                    </span>
                  </td>
                  <td className="py-2.5 px-3" style={{ color: "#64748B", fontSize: 11 }}>{alarm.ackUser || "—"}</td>
                  <td className="py-2.5 px-3">
                    <button className="p-1 rounded hover:bg-[#334155] transition-colors" onClick={e => { e.stopPropagation(); setSelected(alarm); }}>
                      <Info size={14} color="#60A5FA" />
                    </button>
                  </td>
                </tr>
              ))}
              {filtered.length === 0 && (
                <tr><td colSpan={10} className="py-8 text-center" style={{ color: "#64748B", fontSize: 13 }}>
                  {t(lang,"Không có cảnh báo","No alarms found")}
                </td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {selected && <AlarmDetail alarm={selected} lang={lang} onClose={() => setSelected(null)} />}
    </div>
  );
}
