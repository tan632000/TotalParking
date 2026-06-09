import { useState } from "react";
import { Header } from "./components/scada/Header";
import { Sidebar, type Screen } from "./components/scada/Sidebar";
import { Dashboard } from "./components/scada/Dashboard";
import { FloorPlan } from "./components/scada/FloorPlan";
import { AlarmPanel } from "./components/scada/AlarmPanel";
import { Maintenance } from "./components/scada/Maintenance";
import { VehicleRouting } from "./components/scada/VehicleRouting";
import { Reports, CardManagement, RemoteSupport, ZonesScreen, Settings } from "./components/scada/OtherScreens";
import { BlockDetail } from "./components/scada/BlockDetail";
import { ALARMS } from "./components/scada/mockData";
import type { Block } from "./components/scada/mockData";

function getScreenFromPath(): Screen {
  const path = window.location.pathname.toLowerCase();
  if (path.includes("/floorplan")) return "floorplan";
  if (path.includes("/zones")) return "zones";
  if (path.includes("/routing")) return "routing";
  if (path.includes("/alarms")) return "alarms";
  if (path.includes("/maintenance")) return "maintenance";
  if (path.includes("/reports")) return "reports";
  if (path.includes("/cards")) return "cards";
  if (path.includes("/settings")) return "settings";
  if (path.includes("/remote")) return "remote";
  return "dashboard";
}

export default function App() {
  const [screen, setScreen] = useState<Screen>(getScreenFromPath());
  const [lang, setLang] = useState<"vi" | "en">(() => {
    try {
      const saved = localStorage.getItem("scada_lang");
      return (saved === "vi" || saved === "en") ? saved : "vi";
    } catch (e) {
      return "vi";
    }
  });
  const [selectedBlock, setSelectedBlock] = useState<Block | null>(null);

  const activeAlarms = ALARMS.filter(a => a.status === "active").length;
  const systemStatus = activeAlarms > 0 ? "fault" as const : "normal" as const;

  function renderScreen() {
    switch (screen) {
      case "dashboard":   return <Dashboard lang={lang} onBlockClick={setSelectedBlock} />;
      case "floorplan":  return <FloorPlan lang={lang} onBlockClick={setSelectedBlock} />;
      case "zones":      return <ZonesScreen lang={lang} onBlockClick={setSelectedBlock} />;
      case "routing":    return <VehicleRouting lang={lang} />;
      case "alarms":     return <AlarmPanel lang={lang} />;
      case "maintenance":return <Maintenance lang={lang} />;
      case "reports":    return <Reports lang={lang} />;
      case "cards":      return <CardManagement lang={lang} />;
      case "settings":   return <Settings lang={lang} />;
      case "remote":     return <RemoteSupport lang={lang} />;
      default:           return <Dashboard lang={lang} onBlockClick={setSelectedBlock} />;
    }
  }

  return (
    <div className="flex flex-col h-screen overflow-hidden"
      style={{ background: "#0F172A", color: "#F8FAFC", fontFamily: "'Inter', 'IBM Plex Sans', sans-serif" }}>
      <Header
        systemStatus={systemStatus}
        lang={lang}
        onLangToggle={() => {
          setLang(current => {
            const next = current === "vi" ? "en" : "vi";
            try {
              localStorage.setItem("scada_lang", next);
            } catch (e) {}
            return next;
          });
        }}
        activeAlarms={activeAlarms}
      />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar active={screen} onChange={setScreen} lang={lang} activeAlarms={activeAlarms} />
        {renderScreen()}
      </div>
      {selectedBlock && (
        <BlockDetail block={selectedBlock} onClose={() => setSelectedBlock(null)} lang={lang} />
      )}
    </div>
  );
}
