import { Car, ParkingCircle, AlertTriangle, Layers, Activity, Cpu } from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from "recharts";
import { ZONES, ALARMS, DENSITY_HISTORY, CONNECTION_STATUS, getTotalStats } from "./mockData";
import { BlockStatusBadge, ConnectionDot } from "./StatusBadge";
import type { Block } from "./mockData";

interface DashboardProps {
  lang: "vi" | "en";
  onBlockClick: (block: Block) => void;
}

const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

function KpiCard({ label, value, sub, icon: Icon, color }: {
  label: string; value: string | number; sub?: string;
  icon: React.ElementType; color: string;
}) {
  return (
    <div className="rounded-lg p-4 flex items-start gap-3" style={{ background: "#1E293B", border: "1px solid #334155" }}>
      <div className="rounded-lg p-2 shrink-0" style={{ background: `${color}22` }}>
        <Icon size={20} color={color} />
      </div>
      <div className="min-w-0">
        <div style={{ color: "#94A3B8", fontSize: 12 }}>{label}</div>
        <div style={{ color: "#F8FAFC", fontSize: 28, fontWeight: 700, lineHeight: 1.2, fontVariantNumeric: "tabular-nums" }}>{value}</div>
        {sub && <div style={{ color: "#64748B", fontSize: 11 }}>{sub}</div>}
      </div>
    </div>
  );
}

function ZoneDensityBar({ name, density }: { name: string; density: number }) {
  const color = density > 85 ? "#DC2626" : density > 65 ? "#F59E0B" : "#16A34A";
  return (
    <div className="mb-3">
      <div className="flex justify-between mb-1">
        <span style={{ color: "#CBD5E1", fontSize: 12 }}>{name}</span>
        <span style={{ color, fontSize: 12, fontWeight: 600 }}>{density}%</span>
      </div>
      <div className="h-2 rounded-full" style={{ background: "#334155" }}>
        <div className="h-2 rounded-full transition-all duration-500" style={{ width: `${density}%`, background: color }} />
      </div>
    </div>
  );
}

export function Dashboard({ lang, onBlockClick }: DashboardProps) {
  const stats = getTotalStats(ZONES);
  const allBlocks = ZONES.flatMap(z => z.blocks);
  const faultBlocks = allBlocks.filter(b => b.status === "fault" || b.status === "offline");
  const activeAlarms = ALARMS.filter(a => a.status === "active");

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      {/* KPI Row */}
      <div className="grid grid-cols-2 lg:grid-cols-4 xl:grid-cols-6 gap-3">
        <KpiCard label={t(lang, "Tổng Block", "Total Blocks")} value={stats.totalBlocks} icon={Layers} color="#2563EB" />
        <KpiCard label={t(lang, "Tổng Pallet", "Total Pallets")} value={stats.totalPallets} icon={ParkingCircle} color="#16A34A" />
        <KpiCard label={t(lang, "Có xe", "Occupied")} value={stats.occupied}
          sub={`${Math.round(stats.occupied/stats.totalPallets*100)}%`} icon={Car} color="#16A34A" />
        <KpiCard label={t(lang, "Còn trống", "Available")} value={stats.empty} icon={ParkingCircle} color="#6B7280" />
        <KpiCard label={t(lang, "SUV", "SUV")} value={stats.suv} icon={Car} color="#7C3AED" />
        <KpiCard label={t(lang, "Sedan", "Sedan")} value={stats.sedan} icon={Car} color="#2563EB" />
      </div>

      {/* Alert Banner */}
      {activeAlarms.length > 0 && (
        <div className="flex items-center gap-3 p-3 rounded-lg border animate-pulse"
          style={{ background: "#DC262615", borderColor: "#DC2626" }}>
          <AlertTriangle size={18} color="#DC2626" />
          <span style={{ color: "#FCA5A5", fontSize: 13, fontWeight: 600 }}>
            {activeAlarms.length} {t(lang, "cảnh báo đang hoạt động:", "active alarms:")}
          </span>
          <span style={{ color: "#FCA5A5", fontSize: 12 }}>
            {activeAlarms[0]?.message}
          </span>
        </div>
      )}

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        {/* Density Chart */}
        <div className="xl:col-span-2 rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center justify-between mb-4">
            <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
              {t(lang, "Mật độ khai thác theo thời gian", "Occupancy Over Time")}
            </div>
            <Activity size={16} color="#94A3B8" />
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={DENSITY_HISTORY} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
              <defs>
                <linearGradient id="gZ1" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#16A34A" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#16A34A" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gZ2" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#2563EB" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#2563EB" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gZ3" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#7C3AED" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#7C3AED" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
              <XAxis dataKey="time" tick={{ fill: "#94A3B8", fontSize: 11 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
              <YAxis tick={{ fill: "#94A3B8", fontSize: 11 }} axisLine={{ stroke: "#334155" }} tickLine={false} domain={[0, 100]} />
              <Tooltip contentStyle={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 6 }}
                labelStyle={{ color: "#F8FAFC" }} itemStyle={{ color: "#CBD5E1" }} />
              <Legend wrapperStyle={{ fontSize: 12, color: "#94A3B8" }} />
              <Area type="monotone" dataKey="Z1" name="Zone A" stroke="#16A34A" fill="url(#gZ1)" strokeWidth={2} />
              <Area type="monotone" dataKey="Z2" name="Zone B" stroke="#2563EB" fill="url(#gZ2)" strokeWidth={2} />
              <Area type="monotone" dataKey="Z3" name="Zone C" stroke="#7C3AED" fill="url(#gZ3)" strokeWidth={2} />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        {/* Zone Density + Connections */}
        <div className="space-y-4">
          <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
            <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
              {t(lang, "Mật độ theo Zone", "Zone Density")}
            </div>
            {ZONES.map(z => <ZoneDensityBar key={z.id} name={z.name.split(" - ")[0]} density={z.density} />)}
          </div>
          <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
            <div className="flex items-center gap-2 mb-3">
              <Cpu size={15} color="#94A3B8" />
              <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
                {t(lang, "Kết nối hệ thống", "System Connections")}
              </span>
            </div>
            {[
              { label: "PLC Block", ok: CONNECTION_STATUS.plcBlock },
              { label: "HMI Block", ok: CONNECTION_STATUS.hmiBlock },
              { label: "AI-VDS", ok: CONNECTION_STATUS.aiVds },
              { label: "LED/PGS", ok: CONNECTION_STATUS.ledPgs },
              { label: "SCADA Server", ok: CONNECTION_STATUS.scadaServer },
            ].map(c => (
              <div key={c.label} className="flex items-center justify-between py-1.5 border-b last:border-0" style={{ borderColor: "#334155" }}>
                <span style={{ color: "#CBD5E1", fontSize: 12 }}>{c.label}</span>
                <div className="flex items-center gap-2">
                  <ConnectionDot ok={c.ok} />
                  <span style={{ color: c.ok ? "#86EFAC" : "#FCA5A5", fontSize: 11 }}>
                    {c.ok ? t(lang, "Kết nối", "Online") : t(lang, "Mất kết nối", "Offline")}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Block Status Grid */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div className="flex items-center justify-between mb-3">
          <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
            {t(lang, "Trạng thái Block", "Block Status")}
          </span>
          {faultBlocks.length > 0 && (
            <span style={{ color: "#FCA5A5", fontSize: 12 }}>
              {faultBlocks.length} {t(lang, "block lỗi/offline", "fault/offline blocks")}
            </span>
          )}
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6 gap-2">
          {allBlocks.map(block => {
            const pct = Math.round(block.occupied / block.capacity * 100);
            const statusColor = {
              normal: "#16A34A", running: "#2563EB", warning: "#F59E0B",
              fault: "#DC2626", maintenance: "#7C3AED", offline: "#6B7280"
            }[block.status];
            return (
              <button key={block.id}
                onClick={() => onBlockClick(block)}
                className="rounded-lg p-3 text-left transition-all hover:scale-[1.02] hover:shadow-lg"
                style={{ background: "#0F172A", border: `1px solid ${statusColor}44`, cursor: "pointer" }}>
                <div className="flex items-center justify-between mb-1">
                  <span style={{ color: "#F8FAFC", fontSize: 11, fontWeight: 600 }}>{block.name}</span>
                  <span className="w-2 h-2 rounded-full" style={{ background: statusColor }} />
                </div>
                <div style={{ color: "#94A3B8", fontSize: 10 }}>{block.zone}</div>
                <div className="flex items-center justify-between mt-2">
                  <span style={{ color: statusColor, fontSize: 13, fontWeight: 700 }}>{pct}%</span>
                  <span style={{ color: "#64748B", fontSize: 10 }}>{block.occupied}/{block.capacity}</span>
                </div>
                <div className="h-1 rounded-full mt-1" style={{ background: "#334155" }}>
                  <div className="h-1 rounded-full" style={{ width: `${pct}%`, background: statusColor }} />
                </div>
                <div className="mt-1.5">
                  <BlockStatusBadge status={block.status} lang={lang} />
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Recent Alarms */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
          {t(lang, "Cảnh báo gần nhất", "Recent Alarms")}
        </div>
        <div className="overflow-x-auto">
          <table className="w-full" style={{ borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid #334155" }}>
                {[t(lang,"Thời gian","Time"), t(lang,"Mức độ","Sev"), t(lang,"Zone","Zone"),
                  t(lang,"Block","Block"), t(lang,"Thông báo","Message"), t(lang,"Trạng thái","Status")].map(h => (
                  <th key={h} className="text-left py-2 px-2"
                    style={{ color: "#64748B", fontSize: 11, fontWeight: 600 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {ALARMS.slice(0,5).map(alarm => (
                <tr key={alarm.id} className="border-b hover:bg-[#0F172A] transition-colors"
                  style={{ borderColor: "#1E293B" }}>
                  <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12, fontVariantNumeric: "tabular-nums" }}>{alarm.time}</td>
                  <td className="py-2 px-2">
                    <span className="px-1.5 py-0.5 rounded text-xs font-semibold"
                      style={{
                        background: { critical: "#DC2626", major: "#F59E0B", minor: "#2563EB", info: "#6B7280" }[alarm.severity],
                        color: alarm.severity === "major" ? "#1E293B" : "white", fontSize: 10
                      }}>
                      {alarm.severity.toUpperCase()}
                    </span>
                  </td>
                  <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{alarm.zone}</td>
                  <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{alarm.block}</td>
                  <td className="py-2 px-2 max-w-xs truncate" style={{ color: "#F8FAFC", fontSize: 12 }}>{alarm.message}</td>
                  <td className="py-2 px-2">
                    <span className="px-1.5 py-0.5 rounded text-xs"
                      style={{
                        background: { active: "#DC262622", acknowledged: "#F59E0B22", cleared: "#16A34A22" }[alarm.status],
                        color: { active: "#FCA5A5", acknowledged: "#FCD34D", cleared: "#86EFAC" }[alarm.status], fontSize: 11
                      }}>
                      {alarm.status === "active" ? t(lang,"Đang lỗi","Active") :
                       alarm.status === "acknowledged" ? t(lang,"Đã biết","Ack'd") : t(lang,"Đã xóa","Cleared")}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
