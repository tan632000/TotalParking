import { Navigation, AlertTriangle, CheckCircle, WifiOff } from "lucide-react";
import { ZONES, CONNECTION_STATUS } from "./mockData";

interface VehicleRoutingProps { lang: "vi" | "en" }
const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

const VEHICLE_QUEUE = [
  { id: "VH001", plate: "51A-123.45", type: "SEDAN", length: 4500, height: 1520, status: "classified", zone: "Z2", block: "B2B03", card: "C-SEDAN" },
  { id: "VH002", plate: "51G-678.90", type: "SUV", length: 4800, height: 1720, status: "routing", zone: "Z1", block: "B1A01", card: "C-SUV" },
  { id: "VH003", plate: "51B-000.01", type: "OVERSIZED", length: 5200, height: 1600, status: "rejected", zone: null, block: null, card: null },
];

export function VehicleRouting({ lang }: VehicleRoutingProps) {
  const aiVdsOk = CONNECTION_STATUS.aiVds;

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>
        {t(lang,"Điều hướng xe","Vehicle Routing")}
      </div>

      {/* AI-VDS Status */}
      <div className={`flex items-center gap-3 p-3 rounded-lg border`}
        style={{
          background: aiVdsOk ? "#16A34A15" : "#DC262615",
          borderColor: aiVdsOk ? "#16A34A44" : "#DC262644"
        }}>
        {aiVdsOk
          ? <CheckCircle size={16} color="#16A34A" />
          : <WifiOff size={16} color="#DC2626" />}
        <div>
          <span style={{ color: aiVdsOk ? "#86EFAC" : "#FCA5A5", fontSize: 13, fontWeight: 600 }}>
            AI-VDS: {aiVdsOk ? t(lang,"Kết nối","Connected") : t(lang,"Mất kết nối — Chế độ Fallback","Disconnected — Fallback Mode")}
          </span>
          <div style={{ color: "#64748B", fontSize: 11 }}>
            {t(lang,"Dữ liệu cuối:","Last data:")} {CONNECTION_STATUS.lastAiVdsUpdate}
          </div>
        </div>
      </div>

      {/* Vehicle Queue */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div className="flex items-center gap-2 mb-4">
          <Navigation size={16} color="#2563EB" />
          <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
            {t(lang,"Hàng đợi phân loại xe","Vehicle Classification Queue")}
          </span>
        </div>
        <div className="space-y-3">
          {VEHICLE_QUEUE.map(v => {
            const statusConfig = {
              classified: { label: t(lang,"Đã phân loại","Classified"), color: "#16A34A" },
              routing: { label: t(lang,"Đang điều hướng","Routing"), color: "#2563EB" },
              rejected: { label: t(lang,"Từ chối","Rejected"), color: "#DC2626" },
            }[v.status];
            return (
              <div key={v.id} className="rounded-lg p-4"
                style={{ background: "#0F172A", border: `1px solid ${statusConfig.color}33` }}>
                <div className="flex items-center justify-between flex-wrap gap-2">
                  <div className="flex items-center gap-3">
                    <div className="rounded px-2 py-1" style={{ background: "#1E293B" }}>
                      <span style={{ color: "#60A5FA", fontSize: 13, fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{v.plate}</span>
                    </div>
                    <span className="px-2 py-0.5 rounded text-xs font-semibold"
                      style={{ background: `${statusConfig.color}22`, color: statusConfig.color }}>
                      {v.type}
                    </span>
                    <span className="px-2 py-0.5 rounded text-xs"
                      style={{ background: `${statusConfig.color}22`, color: statusConfig.color }}>
                      {statusConfig.label}
                    </span>
                  </div>
                  <div className="flex items-center gap-4 text-xs" style={{ color: "#94A3B8" }}>
                    <span>L: {v.length}mm</span>
                    <span>H: {v.height}mm</span>
                  </div>
                </div>
                {v.status !== "rejected" && (
                  <div className="mt-3 grid grid-cols-3 gap-3">
                    <div className="rounded p-2" style={{ background: "#1E293B" }}>
                      <div style={{ color: "#64748B", fontSize: 10 }}>{t(lang,"Zone đề xuất","Suggested Zone")}</div>
                      <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700 }}>{v.zone}</div>
                    </div>
                    <div className="rounded p-2" style={{ background: "#1E293B" }}>
                      <div style={{ color: "#64748B", fontSize: 10 }}>{t(lang,"Block đề xuất","Suggested Block")}</div>
                      <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700 }}>{v.block}</div>
                    </div>
                    <div className="rounded p-2" style={{ background: "#1E293B" }}>
                      <div style={{ color: "#64748B", fontSize: 10 }}>{t(lang,"Loại thẻ","Card Type")}</div>
                      <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700 }}>{v.card}</div>
                    </div>
                  </div>
                )}
                {v.status === "rejected" && (
                  <div className="mt-2 flex items-center gap-2">
                    <AlertTriangle size={13} color="#DC2626" />
                    <span style={{ color: "#FCA5A5", fontSize: 12 }}>
                      {t(lang,"Xe quá kích thước — Không thể đỗ trong hệ thống tự động","Oversized — Cannot park in automated system")}
                    </span>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Zone Density for Routing Decision */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
          {t(lang,"Mật độ Zone — Hỗ trợ điều hướng","Zone Density — Routing Aid")}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {ZONES.map(zone => {
            const available = zone.blocks.reduce((s, b) => s + (b.capacity - b.occupied), 0);
            const total = zone.blocks.reduce((s, b) => s + b.capacity, 0);
            const density = zone.density;
            const color = density > 85 ? "#DC2626" : density > 65 ? "#F59E0B" : "#16A34A";
            return (
              <div key={zone.id} className="rounded-lg p-3" style={{ background: "#0F172A", border: `1px solid ${color}33` }}>
                <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 600, marginBottom: 4 }}>{zone.name}</div>
                <div className="flex justify-between mb-2">
                  <span style={{ color: "#94A3B8", fontSize: 11 }}>{t(lang,"Còn trống","Available")}: {available}</span>
                  <span style={{ color, fontSize: 12, fontWeight: 700 }}>{density}%</span>
                </div>
                <div className="h-2 rounded-full" style={{ background: "#334155" }}>
                  <div className="h-2 rounded-full" style={{ width: `${density}%`, background: color }} />
                </div>
                <div className="mt-2">
                  <span className="px-2 py-0.5 rounded text-xs"
                    style={{ background: `${color}22`, color, fontSize: 10 }}>
                    {density < 50 ? t(lang,"Ưu tiên điều hướng vào","Priority routing in")
                      : density < 80 ? t(lang,"Có thể điều hướng","Can route")
                      : t(lang,"Sắp đầy — Hạn chế","Almost full — Limit")}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
