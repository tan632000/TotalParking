import { useState } from "react";
import { ZONES } from "./mockData";
import type { Block } from "./mockData";
import { BlockStatusBadge } from "./StatusBadge";

interface FloorPlanProps { lang: "vi" | "en"; onBlockClick: (b: Block) => void; }
const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

const STATUS_COLOR = {
  normal: "#16A34A", running: "#2563EB", warning: "#F59E0B",
  fault: "#DC2626", maintenance: "#7C3AED", offline: "#6B7280"
};

export function FloorPlan({ lang, onBlockClick }: FloorPlanProps) {
  const [selectedZone, setSelectedZone] = useState<string | null>(null);
  const zone = selectedZone ? ZONES.find(z => z.id === selectedZone) : null;

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>
        {t(lang, "Mặt bằng hệ thống LUMI Automated Parking", "LUMI Automated Parking Floor Plan")}
      </div>

      {/* Legend */}
      <div className="flex gap-4 flex-wrap">
        {Object.entries(STATUS_COLOR).map(([status, color]) => (
          <div key={status} className="flex items-center gap-1.5">
            <span className="w-3 h-3 rounded" style={{ background: color }} />
            <span style={{ color: "#94A3B8", fontSize: 11 }}>
              {{ normal: t(lang,"Bình thường","Normal"), running: t(lang,"Đang chạy","Running"),
                 warning: t(lang,"Cảnh báo","Warning"), fault: t(lang,"Lỗi","Fault"),
                 maintenance: t(lang,"Bảo trì","Maintenance"), offline: "Offline" }[status]}
            </span>
          </div>
        ))}
      </div>

      {/* Map View */}
      <div className="rounded-xl p-6" style={{ background: "#1E293B", border: "1px solid #334155", minHeight: 400 }}>
        <div style={{ color: "#94A3B8", fontSize: 12, marginBottom: 16 }}>
          {t(lang,"Bản đồ tổng thể — Click vào block để xem chi tiết","Overall map — Click a block for details")}
        </div>
        <div className="space-y-6">
          {ZONES.map((zone) => (
            <div key={zone.id}>
              <div className="flex items-center gap-3 mb-3">
                <button
                  onClick={() => setSelectedZone(selectedZone === zone.id ? null : zone.id)}
                  className="flex items-center gap-2 px-3 py-1.5 rounded-lg transition-colors hover:opacity-80"
                  style={{ background: "#0F172A", border: "1px solid #334155" }}>
                  <span style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 600 }}>{zone.name}</span>
                  <span className="px-2 py-0.5 rounded text-xs" style={{ background: "#2563EB22", color: "#60A5FA" }}>
                    {zone.density}% {t(lang,"mật độ","density")}
                  </span>
                </button>
              </div>
              <div className="grid gap-3" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))" }}>
                {zone.blocks.map((block) => {
                  const color = STATUS_COLOR[block.status];
                  const pct = Math.round(block.occupied / block.capacity * 100);
                  return (
                    <button key={block.id}
                      onClick={() => onBlockClick(block)}
                      className="rounded-lg p-3 text-left transition-all hover:scale-[1.03]"
                      style={{ background: "#0F172A", border: `2px solid ${color}66`, cursor: "pointer" }}>
                      <div className="flex items-center justify-between mb-1.5">
                        <span style={{ color: "#F8FAFC", fontSize: 12, fontWeight: 700 }}>{block.name}</span>
                        <span className="w-2.5 h-2.5 rounded-full"
                          style={{ background: color, boxShadow: `0 0 6px ${color}88` }} />
                      </div>
                      <div className="mb-2">
                        <BlockStatusBadge status={block.status} lang={lang} />
                      </div>
                      <div className="h-1.5 rounded-full mb-1.5" style={{ background: "#334155" }}>
                        <div className="h-1.5 rounded-full transition-all" style={{ width: `${pct}%`, background: color }} />
                      </div>
                      <div className="flex justify-between">
                        <span style={{ color: "#94A3B8", fontSize: 10 }}>
                          {block.occupied}/{block.capacity} {t(lang,"xe","cars")}
                        </span>
                        <span style={{ color, fontSize: 10, fontWeight: 700 }}>{pct}%</span>
                      </div>
                      {block.faults.length > 0 && (
                        <div className="mt-1.5 text-left" style={{ color: "#FCA5A5", fontSize: 9 }}>
                          ⚠ {block.faults[0]}
                        </div>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Zone Detail Panel */}
      {zone && (
        <div className="rounded-xl p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
            {zone.name} — {t(lang,"Chi tiết blocks","Block Details")}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: "1px solid #334155" }}>
                  {[t(lang,"Block","Block"), t(lang,"Trạng thái","Status"), t(lang,"Chế độ","Mode"),
                    t(lang,"Xe/Sức chứa","Cars/Capacity"), "SUV", "Sedan", "PLC", "HMI",
                    t(lang,"Hành động","Action")].map(h => (
                    <th key={h} className="text-left py-2 px-2"
                      style={{ color: "#64748B", fontSize: 11, fontWeight: 600 }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {zone.blocks.map(b => (
                  <tr key={b.id} className="border-b hover:bg-[#0F172A]" style={{ borderColor: "#334155" }}>
                    <td className="py-2 px-2" style={{ color: "#F8FAFC", fontSize: 12, fontWeight: 600 }}>{b.name}</td>
                    <td className="py-2 px-2"><BlockStatusBadge status={b.status} lang={lang} /></td>
                    <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>
                      {{ card: t(lang,"Thẻ từ","Card"), auto: t(lang,"Tự động","Auto"),
                         manual: t(lang,"Bằng tay","Manual"), forced: t(lang,"Cưỡng bức","Forced") }[b.operatingMode]}
                    </td>
                    <td className="py-2 px-2" style={{ color: "#F8FAFC", fontSize: 12 }}>{b.occupied}/{b.capacity}</td>
                    <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{b.suv}</td>
                    <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{b.sedan}</td>
                    <td className="py-2 px-2">
                      <span className="w-2 h-2 rounded-full inline-block"
                        style={{ background: b.plcConnected ? "#16A34A" : "#DC2626" }} />
                    </td>
                    <td className="py-2 px-2">
                      <span className="w-2 h-2 rounded-full inline-block"
                        style={{ background: b.hmiConnected ? "#16A34A" : "#DC2626" }} />
                    </td>
                    <td className="py-2 px-2">
                      <button onClick={() => onBlockClick(b)}
                        className="px-2 py-1 rounded text-xs hover:opacity-80 transition-colors"
                        style={{ background: "#2563EB22", color: "#60A5FA", fontSize: 11 }}>
                        {t(lang,"Chi tiết","Detail")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
