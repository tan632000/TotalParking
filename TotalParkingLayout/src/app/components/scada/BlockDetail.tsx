import { X, AlertTriangle, CheckCircle, Wrench, Clock, RotateCcw } from "lucide-react";
import type { Block } from "./mockData";
import { BlockStatusBadge, ConnectionDot } from "./StatusBadge";
import { useState } from "react";

interface BlockDetailProps {
  block: Block;
  onClose: () => void;
  lang: "vi" | "en";
}

const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

export function BlockDetail({ block, onClose, lang }: BlockDetailProps) {
  const [showConfirm, setShowConfirm] = useState<string | null>(null);

  const modeLabel = {
    card: t(lang, "Thẻ từ", "Card"),
    auto: t(lang, "Tự động", "Auto"),
    manual: t(lang, "Bằng tay", "Manual"),
    forced: t(lang, "Cưỡng bức", "Forced"),
  }[block.operatingMode];

  const modeColor = { card: "#2563EB", auto: "#16A34A", manual: "#F59E0B", forced: "#DC2626" }[block.operatingMode];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4"
      style={{ background: "rgba(0,0,0,0.7)" }}>
      <div className="w-full max-w-3xl rounded-xl overflow-hidden flex flex-col max-h-[90vh]"
        style={{ background: "#1E293B", border: "1px solid #334155" }}>

        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b" style={{ borderColor: "#334155" }}>
          <div className="flex items-center gap-3">
            <div>
              <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>{block.name}</div>
              <div style={{ color: "#94A3B8", fontSize: 12 }}>{t(lang,"Zone","Zone")}: {block.zone}</div>
            </div>
            <BlockStatusBadge status={block.status} lang={lang} />
            <span className="px-2 py-0.5 rounded text-xs font-semibold"
              style={{ background: `${modeColor}22`, color: modeColor, border: `1px solid ${modeColor}44` }}>
              {modeLabel}
            </span>
          </div>
          <button onClick={onClose} className="p-1.5 rounded hover:bg-[#334155] transition-colors">
            <X size={18} color="#94A3B8" />
          </button>
        </div>

        <div className="overflow-y-auto flex-1">
          <div className="p-5 space-y-4">
            {/* Stats Row */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                { label: t(lang,"Sức chứa","Capacity"), value: block.capacity, color: "#94A3B8" },
                { label: t(lang,"Có xe","Occupied"), value: block.occupied, color: "#16A34A" },
                { label: t(lang,"Còn trống","Empty"), value: block.capacity - block.occupied, color: "#2563EB" },
                { label: t(lang,"SUV","SUV"), value: block.suv, color: "#7C3AED" },
              ].map(s => (
                <div key={s.label} className="rounded-lg p-3 text-center" style={{ background: "#0F172A" }}>
                  <div style={{ color: s.color, fontSize: 22, fontWeight: 700 }}>{s.value}</div>
                  <div style={{ color: "#64748B", fontSize: 11 }}>{s.label}</div>
                </div>
              ))}
            </div>

            {/* Faults */}
            {block.faults.length > 0 && (
              <div className="rounded-lg p-3" style={{ background: "#DC262615", border: "1px solid #DC262644" }}>
                <div className="flex items-center gap-2 mb-2">
                  <AlertTriangle size={14} color="#DC2626" />
                  <span style={{ color: "#FCA5A5", fontSize: 13, fontWeight: 600 }}>
                    {t(lang,"Lỗi hiện tại","Current Faults")}
                  </span>
                </div>
                {block.faults.map((f, i) => (
                  <div key={i} style={{ color: "#FCA5A5", fontSize: 12 }}>• {f}</div>
                ))}
              </div>
            )}

            {/* System Info */}
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-lg p-3" style={{ background: "#0F172A" }}>
                <div style={{ color: "#64748B", fontSize: 11, marginBottom: 6 }}>
                  {t(lang,"Thông tin vận hành","Operating Info")}
                </div>
                {[
                  { label: "PLC", ok: block.plcConnected },
                  { label: "HMI", ok: block.hmiConnected },
                ].map(c => (
                  <div key={c.label} className="flex justify-between items-center py-1">
                    <span style={{ color: "#94A3B8", fontSize: 12 }}>{c.label}</span>
                    <div className="flex items-center gap-1">
                      <ConnectionDot ok={c.ok} />
                      <span style={{ color: c.ok ? "#86EFAC" : "#FCA5A5", fontSize: 11 }}>
                        {c.ok ? t(lang,"Kết nối","Online") : t(lang,"Mất kết nối","Offline")}
                      </span>
                    </div>
                  </div>
                ))}
                <div className="flex justify-between items-center py-1">
                  <span style={{ color: "#94A3B8", fontSize: 12 }}>{t(lang,"Chu kỳ","Cycles")}</span>
                  <span style={{ color: "#F8FAFC", fontSize: 12, fontWeight: 600 }}>{block.cycles.toLocaleString()}</span>
                </div>
                <div className="flex justify-between items-center py-1">
                  <span style={{ color: "#94A3B8", fontSize: 12 }}>Runtime Motor</span>
                  <span style={{ color: "#F8FAFC", fontSize: 12, fontWeight: 600 }}>{block.motorRuntime}h</span>
                </div>
                <div className="flex justify-between items-center py-1">
                  <span style={{ color: "#94A3B8", fontSize: 12 }}>{t(lang,"Hoạt động cuối","Last Activity")}</span>
                  <span style={{ color: "#F8FAFC", fontSize: 12, fontVariantNumeric: "tabular-nums" }}>{block.lastActivity}</span>
                </div>
              </div>

              {/* Pallet grid */}
              <div className="rounded-lg p-3" style={{ background: "#0F172A" }}>
                <div style={{ color: "#64748B", fontSize: 11, marginBottom: 6 }}>
                  {t(lang,"Trạng thái Pallet","Pallet Status")}
                </div>
                <div className="grid gap-1" style={{ gridTemplateColumns: "repeat(5, 1fr)" }}>
                  {block.pallets.slice(0, 20).map((p) => {
                    const bg = p.fault ? "#DC2626" : p.locked ? "#7C3AED" : p.hasVehicle
                      ? (p.vehicleType === "SUV" ? "#1D4ED8" : "#16A34A")
                      : "#334155";
                    return (
                      <div key={p.id} title={`Pallet ${p.id}${p.hasVehicle ? ` - ${p.vehicleType}` : " - Trống"}`}
                        className="rounded aspect-square flex items-center justify-center"
                        style={{ background: bg, cursor: "default" }}>
                        <span style={{ color: "white", fontSize: 8, fontWeight: 600 }}>{p.id}</span>
                      </div>
                    );
                  })}
                </div>
                <div className="flex gap-3 mt-2 flex-wrap">
                  {[
                    { color: "#16A34A", label: t(lang,"Sedan","Sedan") },
                    { color: "#1D4ED8", label: "SUV" },
                    { color: "#334155", label: t(lang,"Trống","Empty") },
                    { color: "#DC2626", label: t(lang,"Lỗi","Fault") },
                    { color: "#7C3AED", label: t(lang,"Khóa","Locked") },
                  ].map(l => (
                    <div key={l.label} className="flex items-center gap-1">
                      <span className="w-2 h-2 rounded-sm" style={{ background: l.color }} />
                      <span style={{ color: "#64748B", fontSize: 9 }}>{l.label}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex gap-2 flex-wrap">
              <button className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors hover:opacity-90"
                style={{ background: "#2563EB", color: "white", fontSize: 12 }}>
                <Clock size={14} />
                {t(lang,"Lịch sử","History")}
              </button>
              <button className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors hover:opacity-90"
                style={{ background: "#7C3AED", color: "white", fontSize: 12 }}>
                <Wrench size={14} />
                {t(lang,"Bảo trì","Maintenance")}
              </button>
              <button onClick={() => setShowConfirm("reset")}
                className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors hover:opacity-90"
                style={{ background: "#16A34A", color: "white", fontSize: 12 }}>
                <RotateCcw size={14} />
                {t(lang,"Reset Alarm","Reset Alarm")}
              </button>
              <button onClick={() => setShowConfirm("forced")}
                className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors hover:opacity-90"
                style={{ background: "#DC2626", color: "white", fontSize: 12 }}>
                <AlertTriangle size={14} />
                {t(lang,"Cưỡng bức","Forced Mode")}
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Confirm Modal */}
      {showConfirm && (
        <div className="absolute inset-0 z-10 flex items-center justify-center"
          style={{ background: "rgba(0,0,0,0.5)" }}>
          <div className="rounded-xl p-6 max-w-sm w-full mx-4"
            style={{ background: "#1E293B", border: "1px solid #DC2626" }}>
            <div className="flex items-center gap-2 mb-3">
              <AlertTriangle size={20} color="#DC2626" />
              <span style={{ color: "#F8FAFC", fontSize: 15, fontWeight: 700 }}>
                {t(lang,"Xác nhận thao tác","Confirm Action")}
              </span>
            </div>
            <p style={{ color: "#CBD5E1", fontSize: 13, marginBottom: 12 }}>
              {showConfirm === "forced"
                ? t(lang,"Chế độ Cưỡng bức cần quyền Admin. Bạn có chắc muốn tiếp tục?",
                    "Forced Mode requires Admin permission. Are you sure?")
                : t(lang,"Xác nhận Reset Alarm cho block này?","Confirm Reset Alarm for this block?")}
            </p>
            <div className="flex gap-2">
              <button onClick={() => setShowConfirm(null)}
                className="flex-1 px-3 py-2 rounded-lg"
                style={{ background: "#334155", color: "#CBD5E1", fontSize: 13 }}>
                {t(lang,"Hủy","Cancel")}
              </button>
              <button onClick={() => setShowConfirm(null)}
                className="flex-1 px-3 py-2 rounded-lg"
                style={{ background: "#DC2626", color: "white", fontSize: 13, fontWeight: 600 }}>
                <div className="flex items-center justify-center gap-1">
                  <CheckCircle size={14} />
                  {t(lang,"Xác nhận","Confirm")}
                </div>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
