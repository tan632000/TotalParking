import {
  LayoutDashboard, Map, Grid3X3, Navigation, BellRing,
  Wrench, FileBarChart2, CreditCard, Settings, HeadphonesIcon,
  ChevronRight
} from "lucide-react";

export type Screen =
  | "dashboard" | "floorplan" | "zones" | "routing"
  | "alarms" | "maintenance" | "reports" | "cards"
  | "settings" | "remote";

interface SidebarProps {
  active: Screen;
  onChange: (s: Screen) => void;
  lang: "vi" | "en";
  activeAlarms: number;
}

const ITEMS: { id: Screen; icon: React.ElementType; vi: string; en: string; badge?: boolean }[] = [
  { id: "dashboard",   icon: LayoutDashboard,  vi: "Tổng quan",       en: "Dashboard" },
  { id: "floorplan",   icon: Map,              vi: "Mặt bằng",        en: "Floor Plan" },
  { id: "zones",       icon: Grid3X3,          vi: "Zone / Block",    en: "Zone / Block" },
  { id: "routing",     icon: Navigation,       vi: "Điều hướng xe",   en: "Vehicle Routing" },
  { id: "alarms",      icon: BellRing,         vi: "Alarm & Event",   en: "Alarm & Event", badge: true },
  { id: "maintenance", icon: Wrench,           vi: "Bảo trì",         en: "Maintenance" },
  { id: "reports",     icon: FileBarChart2,    vi: "Báo cáo",         en: "Reports" },
  { id: "cards",       icon: CreditCard,       vi: "Quản lý thẻ",    en: "Card Mgmt" },
  { id: "settings",    icon: Settings,         vi: "Cài đặt",         en: "Settings" },
  { id: "remote",      icon: HeadphonesIcon,   vi: "Remote Support",  en: "Remote Support" },
];

const SCREEN_URLS: Record<Screen, string> = {
  dashboard: "/Home/Index",
  floorplan: "/Home/FloorPlan",
  zones: "/Home/Zones",
  routing: "/Home/Routing",
  alarms: "/Home/Alarms",
  maintenance: "/Home/Maintenance",
  reports: "/Home/Reports",
  cards: "/Home/Cards",
  settings: "/Home/Settings",
  remote: "/Home/Remote",
};

export function Sidebar({ active, onChange, lang, activeAlarms }: SidebarProps) {
  return (
    <aside className="w-14 lg:w-52 flex flex-col border-r border-[#334155] shrink-0"
      style={{ background: "#1E293B" }}>
      <nav className="flex-1 py-2 overflow-y-auto">
        {ITEMS.map(item => {
          const Icon = item.icon;
          const isActive = active === item.id;
          return (
            <button key={item.id}
              onClick={() => {
                if (window.location.origin.includes("localhost:5173")) {
                  onChange(item.id);
                } else {
                  window.location.href = SCREEN_URLS[item.id];
                }
              }}
              className="w-full flex items-center gap-3 px-3 py-2.5 mx-0 relative transition-colors hover:bg-[#0F172A] group"
              style={{
                background: isActive ? "#0F172A" : "transparent",
                borderLeft: isActive ? "3px solid #2563EB" : "3px solid transparent",
              }}>
              <Icon size={18} color={isActive ? "#60A5FA" : "#94A3B8"}
                className="shrink-0 group-hover:text-[#60A5FA] transition-colors" />
              <span className="hidden lg:block truncate"
                style={{ color: isActive ? "#F8FAFC" : "#94A3B8", fontSize: 13, fontWeight: isActive ? 600 : 400 }}>
                {lang === "vi" ? item.vi : item.en}
              </span>
              {item.badge && activeAlarms > 0 && (
                <span className="hidden lg:flex ml-auto min-w-[20px] h-5 rounded-full items-center justify-center text-xs font-bold"
                  style={{ background: "#DC2626", color: "white", fontSize: 10, padding: "0 5px" }}>
                  {activeAlarms}
                </span>
              )}
              {item.badge && activeAlarms > 0 && (
                <span className="lg:hidden absolute top-1.5 right-1.5 w-2 h-2 rounded-full"
                  style={{ background: "#DC2626" }} />
              )}
              {isActive && (
                <ChevronRight size={14} color="#2563EB" className="hidden lg:block ml-auto shrink-0" />
              )}
            </button>
          );
        })}
      </nav>
      <div className="p-3 border-t border-[#334155] hidden lg:block">
        <div style={{ color: "#475569", fontSize: 10, textAlign: "center" }}>
          SCADA v2.4.1 | © TotalParking
        </div>
      </div>
    </aside>
  );
}
