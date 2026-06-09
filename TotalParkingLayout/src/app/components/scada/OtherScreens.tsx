import { FileDown, Filter, Plus, Trash2, Edit3, Shield, Users } from "lucide-react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { ZONES } from "./mockData";
import { BlockStatusBadge } from "./StatusBadge";
import type { Block } from "./mockData";

const t = (lang: "vi" | "en", vi: string, en: string) => lang === "vi" ? vi : en;

// ===== REPORTS =====
export function Reports({ lang }: { lang: "vi" | "en" }) {
  const reportTypes = [
    { key: "density", label: t(lang,"Mật độ / Lưu lượng","Density / Traffic") },
    { key: "fault", label: t(lang,"Lỗi","Faults") },
    { key: "maintenance", label: t(lang,"Bảo trì","Maintenance") },
    { key: "card", label: t(lang,"Thẻ / Xe","Card / Vehicle") },
    { key: "history", label: t(lang,"Lịch sử gửi/lấy xe","Parking History") },
  ];

  const paretoData = [
    { name: "ERR_CHAIN", count: 12, pct: 35 },
    { name: "COMM_LOST", count: 8, pct: 58 },
    { name: "LIMIT_SW", count: 5, pct: 73 },
    { name: "OVERSIZED", count: 4, pct: 85 },
    { name: "E-STOP", count: 3, pct: 94 },
    { name: "OTHER", count: 2, pct: 100 },
  ];

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>{t(lang,"Báo cáo","Reports")}</div>

      {/* Filter Bar */}
      <div className="flex gap-3 flex-wrap items-center p-3 rounded-lg" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div className="flex items-center gap-2">
          <Filter size={14} color="#94A3B8" />
          <span style={{ color: "#94A3B8", fontSize: 12 }}>{t(lang,"Lọc:","Filter:")}</span>
        </div>
        <select className="px-2 py-1 rounded text-xs" style={{ background: "#0F172A", color: "#F8FAFC", border: "1px solid #334155" }}>
          <option>{t(lang,"Hôm nay","Today")}</option>
          <option>{t(lang,"7 ngày","7 days")}</option>
          <option>{t(lang,"Tháng này","This month")}</option>
        </select>
        <select className="px-2 py-1 rounded text-xs" style={{ background: "#0F172A", color: "#F8FAFC", border: "1px solid #334155" }}>
          <option>{t(lang,"Tất cả Zone","All Zones")}</option>
          <option>Zone A</option>
          <option>Zone B</option>
          <option>Zone C</option>
        </select>
        <div className="flex gap-2 ml-auto">
          <button className="flex items-center gap-1.5 px-3 py-1.5 rounded text-xs hover:opacity-80"
            style={{ background: "#16A34A", color: "white" }}>
            <FileDown size={13} /> Excel
          </button>
          <button className="flex items-center gap-1.5 px-3 py-1.5 rounded text-xs hover:opacity-80"
            style={{ background: "#DC2626", color: "white" }}>
            <FileDown size={13} /> PDF
          </button>
        </div>
      </div>

      {/* Report Type Tabs */}
      <div className="flex gap-2 flex-wrap">
        {reportTypes.map(r => (
          <button key={r.key} className="px-3 py-1.5 rounded-lg text-xs transition-colors hover:opacity-80"
            style={{ background: r.key === "density" ? "#2563EB" : "#1E293B",
                     color: r.key === "density" ? "white" : "#94A3B8",
                     border: "1px solid #334155" }}>
            {r.label}
          </button>
        ))}
      </div>

      {/* Pareto Chart */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
          {t(lang,"Biểu đồ Pareto lỗi","Fault Pareto Chart")}
        </div>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={paretoData} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
            <XAxis dataKey="name" tick={{ fill: "#94A3B8", fontSize: 11 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
            <YAxis yAxisId="left" tick={{ fill: "#94A3B8", fontSize: 11 }} axisLine={{ stroke: "#334155" }} tickLine={false} />
            <YAxis yAxisId="right" orientation="right" domain={[0, 100]} tick={{ fill: "#94A3B8", fontSize: 11 }}
              axisLine={{ stroke: "#334155" }} tickLine={false} tickFormatter={(v: number) => `${v}%`} />
            <Tooltip contentStyle={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 6 }}
              labelStyle={{ color: "#F8FAFC" }} />
            <Bar yAxisId="left" dataKey="count" name={t(lang,"Số lần","Count")} fill="#DC2626" radius={[4,4,0,0]} />
            <Bar yAxisId="right" dataKey="pct" name="Pareto %" fill="#2563EB" radius={[4,4,0,0]} opacity={0.5} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Sample Table */}
      <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <div style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600, marginBottom: 12 }}>
          {t(lang,"Lịch sử gửi/lấy xe (mẫu)","Parking History (sample)")}
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: "1px solid #334155" }}>
                {[t(lang,"Biển số","Plate"), t(lang,"Loại xe","Type"), "Block", t(lang,"Vào","Entry"),
                  t(lang,"Ra","Exit"), t(lang,"Thời gian đỗ","Duration"), t(lang,"Thẻ","Card")].map(h => (
                  <th key={h} className="text-left py-2 px-2" style={{ color: "#64748B", fontSize: 11, fontWeight: 600 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { plate: "51A-123.45", type: "SEDAN", block: "B1A01", entry: "08:12", exit: "16:45", dur: "8h 33m", card: "C-0014" },
                { plate: "51G-678.90", type: "SUV", block: "B2B03", entry: "09:05", exit: "—", dur: t(lang,"Đang đỗ","Parking"), card: "C-0089" },
                { plate: "51B-000.01", type: "SEDAN", block: "B1A02", entry: "07:30", exit: "12:00", dur: "4h 30m", card: "C-0022" },
              ].map((row, i) => (
                <tr key={i} className="border-b hover:bg-[#0F172A]" style={{ borderColor: "#334155" }}>
                  <td className="py-2 px-2"><code style={{ color: "#60A5FA", fontSize: 12 }}>{row.plate}</code></td>
                  <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{row.type}</td>
                  <td className="py-2 px-2" style={{ color: "#CBD5E1", fontSize: 12 }}>{row.block}</td>
                  <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{row.entry}</td>
                  <td className="py-2 px-2" style={{ color: "#94A3B8", fontSize: 12 }}>{row.exit}</td>
                  <td className="py-2 px-2" style={{ color: row.dur === t(lang,"Đang đỗ","Parking") ? "#16A34A" : "#F8FAFC", fontSize: 12 }}>{row.dur}</td>
                  <td className="py-2 px-2" style={{ color: "#64748B", fontSize: 12 }}>{row.card}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

// ===== CARD MANAGEMENT =====
export function CardManagement({ lang }: { lang: "vi" | "en" }) {
  const cards = [
    { id: "C-0014", type: "SEDAN", vehicle: "51A-123.45", zone: "Z1, Z2", pallet: t(lang,"Bất kỳ","Any"), status: "active" },
    { id: "C-0022", type: "SEDAN", vehicle: "51B-000.01", zone: "Z1", pallet: "A01-P03", status: "active" },
    { id: "C-0089", type: "SUV", vehicle: "51G-678.90", zone: t(lang,"Tất cả","All"), pallet: t(lang,"Bất kỳ","Any"), status: "active" },
    { id: "C-0103", type: "SUV", vehicle: "—", zone: "—", pallet: "—", status: "inactive" },
  ];

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>{t(lang,"Quản lý thẻ","Card Management")}</div>
        <button className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm hover:opacity-80"
          style={{ background: "#2563EB", color: "white" }}>
          <Plus size={14} /> {t(lang,"Cài đặt thẻ mới","Add New Card")}
        </button>
      </div>

      <div className="rounded-lg overflow-hidden" style={{ border: "1px solid #334155" }}>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ background: "#1E293B", borderBottom: "1px solid #334155" }}>
                {["Card ID", t(lang,"Loại xe","Vehicle Type"), t(lang,"Biển số","Plate"),
                  t(lang,"Zone được phép","Allowed Zone"), t(lang,"Pallet cố định","Fixed Pallet"),
                  t(lang,"Trạng thái","Status"), t(lang,"Hành động","Action")].map(h => (
                  <th key={h} className="text-left py-2.5 px-3" style={{ color: "#64748B", fontSize: 11, fontWeight: 600 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {cards.map(card => (
                <tr key={card.id} className="border-b hover:bg-[#1E293B]" style={{ borderColor: "#334155", background: "#0F172A" }}>
                  <td className="py-3 px-3"><code style={{ color: "#60A5FA", fontSize: 12 }}>{card.id}</code></td>
                  <td className="py-3 px-3">
                    <span className="px-2 py-0.5 rounded text-xs"
                      style={{ background: card.type === "SUV" ? "#7C3AED22" : "#2563EB22",
                               color: card.type === "SUV" ? "#A78BFA" : "#60A5FA" }}>
                      {card.type}
                    </span>
                  </td>
                  <td className="py-3 px-3" style={{ color: "#CBD5E1", fontSize: 12 }}>{card.vehicle}</td>
                  <td className="py-3 px-3" style={{ color: "#94A3B8", fontSize: 12 }}>{card.zone}</td>
                  <td className="py-3 px-3" style={{ color: "#94A3B8", fontSize: 12 }}>{card.pallet}</td>
                  <td className="py-3 px-3">
                    <span className="px-2 py-0.5 rounded text-xs"
                      style={{ background: card.status === "active" ? "#16A34A22" : "#6B728022",
                               color: card.status === "active" ? "#86EFAC" : "#9CA3AF" }}>
                      {card.status === "active" ? t(lang,"Hoạt động","Active") : t(lang,"Không hoạt động","Inactive")}
                    </span>
                  </td>
                  <td className="py-3 px-3">
                    <div className="flex items-center gap-1.5">
                      <button className="p-1.5 rounded hover:bg-[#334155]"><Edit3 size={13} color="#60A5FA" /></button>
                      <button className="p-1.5 rounded hover:bg-[#334155]"><Trash2 size={13} color="#DC2626" /></button>
                    </div>
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

// ===== ZONES SCREEN =====
export function ZonesScreen({ lang, onBlockClick }: { lang: "vi" | "en"; onBlockClick: (b: Block) => void }) {
  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>
        {t(lang,"Zone / Block Monitoring","Zone / Block Monitoring")}
      </div>
      {ZONES.map(zone => (
        <div key={zone.id} className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center gap-3 mb-3">
            <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 700 }}>{zone.name}</span>
            <span className="px-2 py-0.5 rounded text-xs"
              style={{ background: "#2563EB22", color: "#60A5FA" }}>
              {zone.density}% {t(lang,"mật độ","density")}
            </span>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {zone.blocks.map(block => {
              const pct = Math.round(block.occupied / block.capacity * 100);
              const color = { normal: "#16A34A", running: "#2563EB", warning: "#F59E0B", fault: "#DC2626", maintenance: "#7C3AED", offline: "#6B7280" }[block.status] || "#6B7280";
              return (
                <button key={block.id} onClick={() => onBlockClick(block)}
                  className="rounded-lg p-4 text-left hover:scale-[1.02] transition-all"
                  style={{ background: "#0F172A", border: `1px solid ${color}44` }}>
                  <div className="flex justify-between items-center mb-2">
                    <span style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 700 }}>{block.name}</span>
                    <BlockStatusBadge status={block.status} lang={lang} />
                  </div>
                  <div className="flex gap-4 mb-2">
                    <span style={{ color: "#94A3B8", fontSize: 11 }}>SUV: <b style={{ color: "#F8FAFC" }}>{block.suv}</b></span>
                    <span style={{ color: "#94A3B8", fontSize: 11 }}>Sedan: <b style={{ color: "#F8FAFC" }}>{block.sedan}</b></span>
                    <span style={{ color: "#94A3B8", fontSize: 11 }}>{t(lang,"Trống","Free")}: <b style={{ color: "#16A34A" }}>{block.capacity - block.occupied}</b></span>
                  </div>
                  <div className="h-1.5 rounded-full mb-1" style={{ background: "#334155" }}>
                    <div className="h-1.5 rounded-full" style={{ width: `${pct}%`, background: color }} />
                  </div>
                  <div className="flex justify-between">
                    <span style={{ color: "#64748B", fontSize: 11 }}>{block.occupied}/{block.capacity}</span>
                    <span style={{ color, fontSize: 11, fontWeight: 700 }}>{pct}%</span>
                  </div>
                  {block.faults.length > 0 && (
                    <div className="mt-2 text-left" style={{ color: "#FCA5A5", fontSize: 10 }}>⚠ {block.faults[0]}</div>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}

// ===== REMOTE SUPPORT =====
export function RemoteSupport({ lang }: { lang: "vi" | "en" }) {
  const roles = [
    { name: "Nguyễn Văn A", role: "Operator", status: "online", lastSeen: "Đang online" },
    { name: "Trần Thị B", role: "Maintenance", status: "online", lastSeen: "Đang online" },
    { name: "Lê Văn C", role: "Supervisor", status: "offline", lastSeen: "08:00 hôm nay" },
    { name: "Admin System", role: "Admin", status: "online", lastSeen: "Đang online" },
  ];
  const roleColor: Record<string, string> = {
    Operator: "#2563EB", Maintenance: "#7C3AED", Supervisor: "#F59E0B", Admin: "#DC2626"
  };

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700 }}>Remote Support & {t(lang,"Phân quyền","User Roles")}</div>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center gap-2 mb-4">
            <Users size={15} color="#94A3B8" />
            <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>{t(lang,"Người dùng","Users")}</span>
          </div>
          <div className="space-y-2">
            {roles.map(u => (
              <div key={u.name} className="flex items-center justify-between p-3 rounded-lg" style={{ background: "#0F172A" }}>
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full flex items-center justify-center"
                    style={{ background: roleColor[u.role] || "#6B7280", color: "white", fontSize: 12, fontWeight: 700 }}>
                    {u.name.charAt(0)}
                  </div>
                  <div>
                    <div style={{ color: "#F8FAFC", fontSize: 13, fontWeight: 600 }}>{u.name}</div>
                    <span className="px-1.5 py-0.5 rounded"
                      style={{ background: `${roleColor[u.role] || "#6B7280"}22`, color: roleColor[u.role] || "#6B7280", fontSize: 10 }}>
                      {u.role}
                    </span>
                  </div>
                </div>
                <div className="text-right">
                  <div className="flex items-center gap-1 justify-end">
                    <span className="w-2 h-2 rounded-full" style={{ background: u.status === "online" ? "#16A34A" : "#6B7280" }} />
                    <span style={{ color: u.status === "online" ? "#86EFAC" : "#9CA3AF", fontSize: 11 }}>
                      {u.status === "online" ? "Online" : "Offline"}
                    </span>
                  </div>
                  <div style={{ color: "#64748B", fontSize: 10 }}>{u.lastSeen}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="rounded-lg p-4" style={{ background: "#1E293B", border: "1px solid #334155" }}>
          <div className="flex items-center gap-2 mb-4">
            <Shield size={15} color="#94A3B8" />
            <span style={{ color: "#F8FAFC", fontSize: 14, fontWeight: 600 }}>Audit Log</span>
          </div>
          <div className="space-y-2">
            {[
              { user: "Admin", time: "09:10:22", block: "B1A04", cmd: "Reset Alarm", result: t(lang,"Thành công","Success") },
              { user: "nguyen.van.a", time: "09:12:05", block: "B1A03", cmd: "Ack Alarm A002", result: t(lang,"Thành công","Success") },
              { user: "Admin", time: "06:05:00", block: "B2B02", cmd: t(lang,"Bắt đầu bảo trì","Start Maintenance"), result: t(lang,"Thành công","Success") },
              { user: "tran.thi.b", time: "07:30:00", block: "B1A04", cmd: "Manual Mode", result: t(lang,"Từ chối - Cần Admin","Denied - Need Admin") },
            ].map((log, i) => (
              <div key={i} className="p-2.5 rounded" style={{ background: "#0F172A" }}>
                <div className="flex justify-between items-start">
                  <div>
                    <span style={{ color: "#60A5FA", fontSize: 12, fontWeight: 600 }}>{log.user}</span>
                    <span style={{ color: "#64748B", fontSize: 11 }}> — {log.block} — </span>
                    <span style={{ color: "#CBD5E1", fontSize: 12 }}>{log.cmd}</span>
                  </div>
                  <span style={{ color: "#64748B", fontSize: 10, fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>{log.time}</span>
                </div>
                <div className="mt-0.5">
                  <span style={{ color: log.result.includes("Thành công") || log.result.includes("Success") ? "#86EFAC" : "#FCA5A5", fontSize: 11 }}>
                    → {log.result}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

// ===== SETTINGS =====
export function Settings({ lang }: { lang: "vi" | "en" }) {
  return (
    <div className="flex-1 overflow-y-auto p-4" style={{ background: "#0F172A" }}>
      <div style={{ color: "#F8FAFC", fontSize: 16, fontWeight: 700, marginBottom: 16 }}>
        {t(lang,"Cài đặt hệ thống","System Settings")}
      </div>
      <div className="rounded-lg p-6 text-center" style={{ background: "#1E293B", border: "1px solid #334155" }}>
        <Shield size={40} color="#64748B" style={{ margin: "0 auto 12px" }} />
        <div style={{ color: "#64748B", fontSize: 13 }}>
          {t(lang,"Trang cài đặt hệ thống — Cần quyền Admin để truy cập","System settings page — Admin access required")}
        </div>
      </div>
    </div>
  );
}
