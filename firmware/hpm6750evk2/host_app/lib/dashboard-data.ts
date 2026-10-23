import { decodeThreeInOneFrame } from './three-in-one-protocol.mjs';

export type Scenario = 'working' | 'standby' | 'warning';
export type Tone = 'good' | 'muted' | 'warning' | 'danger';
export interface FunctionState { key: string; name: string; state: string; tone: Tone; value: number | null; unit: string; detail: string; }
export interface DriveState { rpm: number | null; current: number | null; temperature: number | null; faults: number | null; }
export interface DashboardData {
  soc: number | null; voltage: number | null; current: number | null;
  mode: string; running: string; emergency: string; tone: Tone;
  left: DriveState; right: DriveState; functions: FunctionState[];
  alerts: { title: string; source: string; detail: string; tone: Tone }[];
  trend: number[];
}

const idleDrive = (): DriveState => ({ rpm: null, current: null, temperature: null, faults: null });
export const offlineData: DashboardData = {
  soc: null, voltage: null, current: null, mode: '未获取', running: '未获取', emergency: '未获取', tone: 'muted',
  left: idleDrive(), right: idleDrive(), trend: [], alerts: [],
  functions: [
    { key: 'fan', name: '植保风机', state: '未获取', tone: 'muted', value: null, unit: '档', detail: '' },
    { key: 'pump', name: '供液水泵', state: '未获取', tone: 'muted', value: null, unit: '档', detail: '' },
    { key: 'shredder', name: '碎枝机', state: '未获取', tone: 'muted', value: null, unit: 'rpm', detail: '' },
    { key: 'light', name: '作业照明', state: '未获取', tone: 'muted', value: null, unit: '', detail: '' },
  ],
};

function feedback(id: number, raw: number[]) {
  const bytes = new Uint8Array(8); bytes.set(raw);
  bytes[7] = bytes.subarray(0, 7).reduce((a, b) => a ^ b, 0);
  return decodeThreeInOneFrame(id, bytes).values as Record<string, number | null>;
}

// These are explicitly labelled browser-only fixtures, never hardware measurements.
export function previewData(scenario: Scenario): DashboardData {
  const working = scenario !== 'standby';
  const warning = scenario === 'warning';
  const battery = feedback(0x650, warning ? [0xe0, 1, 0xf8, 0, 0xb9, 0, 2] : working ? [8, 2, 0xf8, 0, 0x0c, 3, 2] : [0x18, 2, 8, 0, 0x0c, 3, 2]);
  const vehicle = feedback(0x532, [working ? 9 : 0, working ? 4 : 0, 8, 2, 0, 0, 2]);
  const left = feedback(0x533, [0, working ? 0xf6 : 0, working ? 4 : 0, 52, working ? 8 : 0, working ? 83 : 66, 2]);
  const right = feedback(0x534, [0, working ? 0xf6 : 0, working ? 4 : 0, 52, working ? 8 : 0, working ? 81 : 65, 2]);
  const fan = feedback(0x5b2, [working ? 4 : 0, working ? 8 : 0, working ? 7 : 0, 52, working ? 5 : 0, 0, 2]);
  const pump = feedback(0x5b3, [(working ? 3 : 0) | (warning ? 64 : 0), working ? 0xdc : 0, working ? 5 : 0, 52, working ? 3 : 0, 0, 2]);
  const work = feedback(0x5b0, [0, working ? 8 : 0, 0, 0, 0, 0, 2]);
  return {
    soc: battery.soc, voltage: battery.voltage, current: battery.current,
    mode: vehicle.mode === 1 ? '自动作业' : '遥控待机', running: vehicle.moving ? '行走中' : '已停稳', emergency: vehicle.emergency ? '已触发' : '未触发', tone: warning ? 'warning' : 'good',
    left: { rpm: left.rpm, current: left.current, temperature: left.temperature, faults: left.faults },
    right: { rpm: right.rpm, current: right.current, temperature: right.temperature, faults: right.faults },
    functions: [
      { key: 'fan', name: '植保风机', state: work.plantRunning ? '运行中' : '已关闭', tone: working ? 'good' : 'muted', value: fan.gear, unit: '档', detail: `${fan.rpm} rpm` },
      { key: 'pump', name: '供液水泵', state: warning ? '液位偏低' : working ? '运行中' : '已关闭', tone: warning ? 'warning' : working ? 'good' : 'muted', value: pump.gear, unit: '档', detail: warning ? '建议补充药液' : `${pump.rpm} rpm` },
      { key: 'shredder', name: '碎枝机', state: '已关闭', tone: 'muted', value: 0, unit: 'rpm', detail: '' },
      { key: 'light', name: '作业照明', state: vehicle.light ? '已开启' : '已关闭', tone: vehicle.light ? 'good' : 'muted', value: vehicle.light, unit: '', detail: '' },
    ],
    alerts: warning ? [
      { title: '药液液位偏低', source: '供液水泵', detail: '停车后检查液位并补充药液。', tone: 'warning' },
    ] : [],
    trend: working ? [51.5, 51.8, 51.6, 52, 52.1, 51.9, 52.2, 52, 51.8, 52.1, 52, 52.2, 52.1, 52, 51.9, 52.1, 52].map(value => warning ? value - 4 : value) : [53.3, 53.4, 53.4, 53.5, 53.4, 53.5, 53.6, 53.5, 53.5, 53.6],
  };
}
