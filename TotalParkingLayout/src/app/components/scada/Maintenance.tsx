import { Wrench, AlertTriangle, Clock, TrendingUp } from "lucide-react";
import { ZONES } from "./mockData";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";

interface MaintenanceProps { lang: "vi" | "en" }
const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

const MAINTENANCE_ITEMS = [
  { block: "B2B02", device: t("vi","Motor Nâng","Lift Motor"), current: 9800, threshold: 10000, unit: t("vi","chu kỳ","cycles"), priority: "high" as const },
  { block: "B1A04", device: t("vi","Xích dẫn","Drive Chain"), current: 4820, threshold: 5000, unit: t("vi","chu kỳ","cycles"), priority: "medium" as const },
  { block: "B1A03", device: t("vi","Motor Trượt","Slide Motor"), current: 980, threshold: 1000, unit: "h", priority: "high" as const },
  { block: "B2B01", device: t("vi","Cảm biến LS","Limit Switch LS"), current: 6100, threshold: 8000, unit: t("vi","chu kỳ","cycles"), priority: "low" as const },
  { block: "B3C02", device: t("vi","Puly cáp","Cable Pulley"), current: 800, threshold: 2000, unit: "h", priority: "low" as const },
];

const CHART_DATA = ZONES.flatMap(z => z.blocks).map(b => ({
  name: b.name.replace("Block ", ""),
  cycles: b.cycles,
  runtime: b.motorRuntime,
}));

export function Maintenance({ lang }: MaintenanceProps) {
  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>
        {t(lang,"Bảo trì & Maintenance Dashboard","Maintenance Dashboard")}
      </div>

      {/* Alert for critical maintenance */}
      <div className="flex items-center gap-3 p-3 rounded-lg border"
        style={{ background: "#F59E0B15", borderColor: "#F59E0B44" }}>
        <AlertTriangle size={16} color="#F59E0B" />
        <span style={{ color: "#FCD34D", fontSize: 13 }}>
          {t(lang,"2 thiết bị sắp đến hạn bảo trì (>90% ngưỡng)","2 devices approaching maintenance threshold (>90%)")}
        </span>
      </div>

      {/* Maintenance Items */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div className="flex items-center gap-2 mb-4">
          <Wrench size={16} color="#7C3AED" />
          <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
            {t(lang,"Thiết bị cần bảo trì","Maintenance Required")}
          </span>
        </div>
        <div className="space-y-3">
          {MAINTENANCE_ITEMS.map((item, i) => {
            const pct = Math.round(item.current / item.threshold * 100);
            const color = pct >= 90 ? "#DC2626" : pct >= 75 ? "#F59E0B" : "#16A34A";
            const priority = { high: t(lang,"Cao","High"), medium: t(lang,"Trung bình","Medium"), low: t(lang,"Thấp","Low") }[item.priority];
            return (
              <div key={i} className="flex items-center gap-4 p-3 rounded-lg"
                style={{ background: "#0F172A", border: `1px solid ${color}33` }}>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <span style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 600 }}>{item.device}</span>
                    <span className="px-1.5 py-0.5 rounded text-xs"
                      style={{ background: `${color}22`, color, fontSize: 10 }}>
                      {priority}
                    </span>
                    <span style={{ color: "#64748B", fontSize: 11 }}>— {item.block}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-2 rounded-full" style={{ background: "#334155" }}>
                      <div className="h-2 rounded-full" style={{ width: `${Math.min(pct, 100)}%`, background: color }} />
                    </div>
                    <span style={{ color, fontSize: 12, fontWeight: 600, minWidth: 40, textAlign: "right" }}>{pct}%</span>
                    <span style={{ color: "#64748B", fontSize: 11 }}>{item.current}/{item.threshold} {item.unit}</span>
                  </div>
                </div>
                <button className="shrink-0 px-3 py-1.5 rounded-lg text-xs hover:opacity-80"
                  style={{ background: "#7C3AED22", color: "#A78BFA", border: "1px solid #7C3AED44" }}>
                  {t(lang,"Tạo Work Order","Create WO")}
                </button>
              </div>
            );
          })}
        </div>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center gap-2 mb-4">
            <TrendingUp size={15} color="#94A3B8" />
            <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
              {t(lang,"Số chu kỳ theo block","Cycles per Block")}
            </span>
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={CHART_DATA} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
              <XAxis dataKey="name" tick={{ fill: "#94A3B8", fontSize: 10 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
              <YAxis tick={{ fill: "#94A3B8", fontSize: 10 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
              <Tooltip contentStyle={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 6 }}
                labelStyle={{ color: "#F8FAFC" }} itemStyle={{ color: "#CBD5E1" }} />
              <Bar dataKey="cycles" name={t(lang,"Chu kỳ","Cycles")} fill="#2563EB" radius={[4,4,0,0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center gap-2 mb-4">
            <Clock size={15} color="#94A3B8" />
            <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>
              Runtime Motor (giờ)
            </span>
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={CHART_DATA} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
              <XAxis dataKey="name" tick={{ fill: "#94A3B8", fontSize: 10 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
              <YAxis tick={{ fill: "#94A3B8", fontSize: 10 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
              <Tooltip contentStyle={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 6 }}
                labelStyle={{ color: "#F8FAFC" }} itemStyle={{ color: "#CBD5E1" }} />
              <Bar dataKey="runtime" name={t(lang,"Runtime (h)","Runtime (h)")} fill="#7C3AED" radius={[4,4,0,0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Work Orders */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
          {t(lang,"Danh sách Work Order đề xuất","Suggested Work Orders")}
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: "1px solid #334155" }}>
                {["WO#", t(lang,"Block","Block"), t(lang,"Thiết bị","Device"), t(lang,"Loại","Type"),
                  t(lang,"Ưu tiên","Priority"), t(lang,"Ngày đề xuất","Proposed Date"), t(lang,"Hành động","Action")].map(h => (
                  <th key={h} className="text-left py-2 px-2" style={{ color: "#64748B", fontSize: 11, fontWeight: 600 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { wo: "WO-2024-001", block: "B2B02", device: t(lang,"Motor Nâng","Lift Motor"), type: t(lang,"Thay thế","Replacement"), priority: "high", date: "2024-06-15" },
                { wo: "WO-2024-002", block: "B1A03", device: t(lang,"Motor Trượt","Slide Motor"), type: t(lang,"Bảo dưỡng","Maintenance"), priority: "high", date: "2024-06-18" },
                { wo: "WO-2024-003", block: "B1A04", device: t(lang,"Xích dẫn","Drive Chain"), type: t(lang,"Kiểm tra","Inspection"), priority: "medium", date: "2024-06-22" },
              ].map(wo => {
                const pColor = { high: "#DC2626", medium: "#F59E0B", low: "#16A34A" }[wo.priority];
                return (
                  <tr key={wo.wo} className="border-b hover:bg-[#0F172A]" style={{ borderColor: "#334155" }}>
                    <td className="py-2 px-2"><code style={{ color: "#60A5FA", fontSize: 12 }}>{wo.wo}</code></td>
                    <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{wo.block}</td>
                    <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{wo.device}</td>
                    <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{wo.type}</td>
                    <td className="py-2 px-2">
                      <span className="px-2 py-0.5 rounded text-xs" style={{ background: `${pColor}22`, color: pColor, fontSize: 11 }}>
                        {{ high: t(lang,"Cao","High"), medium: t(lang,"TB","Med"), low: t(lang,"Thấp","Low") }[wo.priority]}
                      </span>
                    </td>
                    <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{wo.date}</td>
                    <td className="py-2 px-2">
                      <button className="px-2 py-1 rounded text-xs hover:opacity-80"
                        style={{ background: "#2563EB22", color: "#60A5FA", fontSize: 11 }}>
                        {t(lang,"Phân công","Assign")}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
