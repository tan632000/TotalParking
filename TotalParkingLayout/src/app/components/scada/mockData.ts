export type BlockStatus = "normal" | "running" | "warning" | "fault" | "maintenance" | "offline";
export type AlarmSeverity = "critical" | "major" | "minor" | "info";
export type AlarmStatus = "active" | "acknowledged" | "cleared";

export interface Pallet {
  id: string;
  hasVehicle: boolean;
  locked: boolean;
  fault: boolean;
  vehicleType?: "SUV" | "SEDAN";
  entryTime?: string;
}

export interface Block {
  id: string;
  name: string;
  zone: string;
  status: BlockStatus;
  capacity: number;
  occupied: number;
  pallets: Pallet[];
  suv: number;
  sedan: number;
  cycles: number;
  motorRuntime: number;
  lastActivity: string;
  plcConnected: boolean;
  hmiConnected: boolean;
  operatingMode: "card" | "auto" | "manual" | "forced";
  faults: string[];
}

export interface Alarm {
  id: string;
  time: string;
  severity: AlarmSeverity;
  zone: string;
  block: string;
  device: string;
  code: string;
  message: string;
  suggestedAction: string;
  status: AlarmStatus;
  ackUser?: string;
  recoveryTime?: string;
}

export interface Zone {
  id: string;
  name: string;
  blocks: Block[];
  density: number;
}

export const ZONES: Zone[] = [
  {
    id: "Z1", name: "Zone A - Tầng B1", density: 78,
    blocks: [
      { id: "B1A01", name: "Block A01", zone: "Z1", status: "normal", capacity: 20, occupied: 16, suv: 6, sedan: 10, cycles: 4820, motorRuntime: 1240, lastActivity: "09:12:34", plcConnected: true, hmiConnected: true, operatingMode: "auto", faults: [], pallets: Array.from({length:20}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 16, locked: false, fault: false, vehicleType: i < 6 ? "SUV" : "SEDAN", entryTime: i < 16 ? "08:30" : undefined })) },
      { id: "B1A02", name: "Block A02", zone: "Z1", status: "running", capacity: 20, occupied: 18, suv: 8, sedan: 10, cycles: 5210, motorRuntime: 1380, lastActivity: "09:15:22", plcConnected: true, hmiConnected: true, operatingMode: "auto", faults: [], pallets: Array.from({length:20}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 18, locked: false, fault: false, vehicleType: i < 8 ? "SUV" : "SEDAN", entryTime: i < 18 ? "08:45" : undefined })) },
      { id: "B1A03", name: "Block A03", zone: "Z1", status: "warning", capacity: 20, occupied: 14, suv: 5, sedan: 9, cycles: 3900, motorRuntime: 980, lastActivity: "08:55:10", plcConnected: true, hmiConnected: false, operatingMode: "auto", faults: ["HMI kết nối thất bại"], pallets: Array.from({length:20}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 14, locked: false, fault: i === 5, vehicleType: i < 5 ? "SUV" : "SEDAN", entryTime: i < 14 ? "07:30" : undefined })) },
      { id: "B1A04", name: "Block A04", zone: "Z1", status: "fault", capacity: 20, occupied: 10, suv: 4, sedan: 6, cycles: 2100, motorRuntime: 540, lastActivity: "07:30:05", plcConnected: true, hmiConnected: true, operatingMode: "manual", faults: ["Lỗi chùng xích nâng", "Công tắc hành trình lỗi"], pallets: Array.from({length:20}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 10, locked: i === 3, fault: i === 3 || i === 7, vehicleType: i < 4 ? "SUV" : "SEDAN", entryTime: i < 10 ? "06:00" : undefined })) },
    ]
  },
  {
    id: "Z2", name: "Zone B - Tầng B2", density: 55,
    blocks: [
      { id: "B2B01", name: "Block B01", zone: "Z2", status: "normal", capacity: 24, occupied: 13, suv: 5, sedan: 8, cycles: 6100, motorRuntime: 1600, lastActivity: "09:10:00", plcConnected: true, hmiConnected: true, operatingMode: "auto", faults: [], pallets: Array.from({length:24}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 13, locked: false, fault: false, vehicleType: i < 5 ? "SUV" : "SEDAN", entryTime: i < 13 ? "08:00" : undefined })) },
      { id: "B2B02", name: "Block B02", zone: "Z2", status: "maintenance", capacity: 24, occupied: 0, suv: 0, sedan: 0, cycles: 9800, motorRuntime: 2400, lastActivity: "06:00:00", plcConnected: true, hmiConnected: true, operatingMode: "manual", faults: [], pallets: Array.from({length:24}, (_,i) => ({ id: `P${i+1}`, hasVehicle: false, locked: true, fault: false })) },
      { id: "B2B03", name: "Block B03", zone: "Z2", status: "normal", capacity: 24, occupied: 15, suv: 7, sedan: 8, cycles: 5400, motorRuntime: 1450, lastActivity: "09:05:33", plcConnected: true, hmiConnected: true, operatingMode: "auto", faults: [], pallets: Array.from({length:24}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 15, locked: false, fault: false, vehicleType: i < 7 ? "SUV" : "SEDAN", entryTime: i < 15 ? "08:20" : undefined })) },
    ]
  },
  {
    id: "Z3", name: "Zone C - Tầng B3", density: 32,
    blocks: [
      { id: "B3C01", name: "Block C01", zone: "Z3", status: "offline", capacity: 16, occupied: 0, suv: 0, sedan: 0, cycles: 1200, motorRuntime: 300, lastActivity: "02:00:00", plcConnected: false, hmiConnected: false, operatingMode: "manual", faults: ["Mất kết nối PLC", "Không phản hồi"], pallets: Array.from({length:16}, (_,i) => ({ id: `P${i+1}`, hasVehicle: false, locked: false, fault: false })) },
      { id: "B3C02", name: "Block C02", zone: "Z3", status: "normal", capacity: 16, occupied: 5, suv: 2, sedan: 3, cycles: 800, motorRuntime: 210, lastActivity: "08:40:15", plcConnected: true, hmiConnected: true, operatingMode: "auto", faults: [], pallets: Array.from({length:16}, (_,i) => ({ id: `P${i+1}`, hasVehicle: i < 5, locked: false, fault: false, vehicleType: i < 2 ? "SUV" : "SEDAN", entryTime: i < 5 ? "08:00" : undefined })) },
    ]
  },
];

export const ALARMS: Alarm[] = [
  { id: "A001", time: "09:14:22", severity: "critical", zone: "Z1", block: "B1A04", device: "Motor Lift 1", code: "ERR_CHAIN_SLACK", message: "Chùng xích nâng pallet P04 - Block A04", suggestedAction: "Dừng block, kiểm tra căng xích, liên hệ bảo trì", status: "active", ackUser: undefined, recoveryTime: undefined },
  { id: "A002", time: "09:12:05", severity: "major", zone: "Z1", block: "B1A03", device: "HMI Panel", code: "COMM_HMI_LOST", message: "Mất kết nối HMI Block A03", suggestedAction: "Kiểm tra cáp mạng HMI, restart HMI panel", status: "acknowledged", ackUser: "nguyen.van.a", recoveryTime: undefined },
  { id: "A003", time: "08:45:11", severity: "minor", zone: "Z2", block: "B2B01", device: "Sensor LS01", code: "WARN_LIMIT_SW", message: "Công tắc hành trình LS01 phản hồi chậm", suggestedAction: "Kiểm tra cảm biến, bôi trơn cơ cấu", status: "acknowledged", ackUser: "tran.thi.b", recoveryTime: undefined },
  { id: "A004", time: "07:30:01", severity: "critical", zone: "Z1", block: "B1A04", device: "Safety PLC", code: "ERR_TRAVEL_SW", message: "Lỗi công tắc hành trình cuối hành trình Block A04", suggestedAction: "Kiểm tra ngay cảm biến giới hạn, không vận hành cho đến khi khắc phục", status: "active" },
  { id: "A005", time: "06:10:33", severity: "major", zone: "Z3", block: "B3C01", device: "PLC Block", code: "COMM_PLC_LOST", message: "Mất kết nối PLC Block C01 - Zone C", suggestedAction: "Kiểm tra nguồn PLC, cáp mạng Ethernet, restart PLC", status: "active" },
  { id: "A006", time: "05:55:18", severity: "info", zone: "Z2", block: "B2B02", device: "System", code: "INFO_MAINT_START", message: "Bắt đầu bảo trì định kỳ Block B02", suggestedAction: "Theo dõi tiến độ bảo trì", status: "cleared", ackUser: "admin", recoveryTime: "06:00:00" },
];

export const DENSITY_HISTORY = [
  { time: "06:00", Z1: 45, Z2: 30, Z3: 10 },
  { time: "07:00", Z1: 58, Z2: 42, Z3: 15 },
  { time: "08:00", Z1: 70, Z2: 52, Z3: 28 },
  { time: "09:00", Z1: 78, Z2: 55, Z3: 32 },
  { time: "10:00", Z1: 82, Z2: 60, Z3: 35 },
  { time: "11:00", Z1: 85, Z2: 65, Z3: 40 },
  { time: "12:00", Z1: 80, Z2: 70, Z3: 45 },
];

export const CONNECTION_STATUS = {
  plcBlock: true,
  hmiBlock: true,
  aiVds: true,
  ledPgs: true,
  scadaServer: true,
  lastAiVdsUpdate: "09:15:30",
};

export function getTotalStats(zones: Zone[]) {
  const allBlocks = zones.flatMap(z => z.blocks);
  const allPallets = allBlocks.flatMap(b => b.pallets);
  return {
    totalBlocks: allBlocks.length,
    totalPallets: allPallets.length,
    occupied: allPallets.filter(p => p.hasVehicle).length,
    empty: allPallets.filter(p => !p.hasVehicle && !p.fault && !p.locked).length,
    suv: allBlocks.reduce((s, b) => s + b.suv, 0),
    sedan: allBlocks.reduce((s, b) => s + b.sedan, 0),
    faultBlocks: allBlocks.filter(b => b.status === "fault" || b.status === "offline").length,
    activeAlarms: ALARMS.filter(a => a.status === "active").length,
  };
}
