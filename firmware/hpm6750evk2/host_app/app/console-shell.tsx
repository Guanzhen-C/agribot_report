"use client";

import { createContext, useContext, useEffect, useState, useSyncExternalStore, type ReactNode } from 'react';
import Link from './navigation-link';
import { usePathname, useSearchParams } from 'next/navigation';
import { exposeConfiguratorApi, fieldGateConfigurator } from '../lib/vcu-configurator';
import { offlineData, previewData, type DashboardData, type Scenario, type Tone } from '../lib/dashboard-data';
import { GlassEffectsSwitch, GlassOptics } from './glass-optics';
import { connectionHelp, connectionLabel, useConnectionSupport } from './connection-support';

export function Icon({ name, size = 20 }: { name: string; size?: number }) {
  const paths: Record<string, ReactNode> = {
    overview: <><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></>,
    launchpad: <><rect x="3" y="3" width="7" height="7" rx="2"/><rect x="14" y="3" width="7" height="7" rx="2"/><rect x="3" y="14" width="7" height="7" rx="2"/><rect x="14" y="14" width="7" height="7" rx="2"/></>,
    gauge: <><path d="M4 19a10 10 0 1 1 16 0M12 4v3M5 8l2 2m12-2-2 2M3 15h3m12 0h3M12 15l4-5"/><circle cx="12" cy="15" r="2"/></>,
    preview: <><path d="m12 2 10 5-10 5L2 7l10-5Zm-10 9 10 5 10-5M2 16l10 5 10-5"/></>,
    reset: <><path d="M3 4v6h6M3 10a9 9 0 1 1 1 9"/></>,
    expand: <><path d="M3 8V3h5m8 0h5v5M3 16v5h5m8 0h5v-5"/></>,
    devices: <><rect x="8" y="3" width="8" height="6" rx="1"/><path d="M12 9v5M4 17v-3h16v3"/><rect x="1" y="17" width="6" height="4" rx="1"/><rect x="9" y="17" width="6" height="4" rx="1"/><rect x="17" y="17" width="6" height="4" rx="1"/></>,
    diagnostics: <><path d="M3 12h4l3-8 4 16 3-8h4"/></>,
    settings: <><path d="M4 6h16M4 12h16M4 18h16"/><circle cx="8" cy="6" r="2"/><circle cx="16" cy="12" r="2"/><circle cx="10" cy="18" r="2"/></>,
    link: <><path d="M9 15l6-6m-8 4-2 2a4 4 0 0 0 6 6l3-3M17 11l2-2a4 4 0 0 0-6-6l-3 3"/></>,
    arrow: <path d="M4 12h15m-5-5 5 5-5 5"/>,
    chevron: <path d="m9 5 7 7-7 7"/>,
    battery: <><rect x="2" y="6" width="18" height="12" rx="2"/><path d="M23 10v4M6 9v6m4-6v6m4-6v6"/></>,
    fan: <><circle cx="12" cy="12" r="2"/><path d="M10 10C0 8 7-2 12 4v6m2 0c2-10 12-3 6 2h-6m0 2c10 2 3 12-2 6v-6m-2 0C8 24-2 17 4 12h6"/></>,
    pump: <><path d="M12 2C9 7 5 10 5 15a7 7 0 0 0 14 0c0-5-4-8-7-13Z"/><path d="M8 15a4 4 0 0 0 4 4"/></>,
    shredder: <><circle cx="12" cy="12" r="7"/><path d="M12 2v4m0 12v4M2 12h4m12 0h4M5 5l3 3m8 8 3 3M5 19l3-3m8-8 3-3M9 12h6"/></>,
    light: <><path d="M13 5c-10-2-10 16 0 14V5Zm4 1 4-2m-4 7h5m-5 5 4 2"/></>,
    check: <path d="m5 12 4 4L19 6"/>,
    close: <path d="m6 6 12 12M18 6 6 18"/>,
    download: <><path d="M12 3v12m-5-5 5 5 5-5M4 15v6h16v-6"/></>,
    eye: <><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/></>,
    warning: <><path d="m12 3 10 18H2L12 3Z"/><path d="M12 9v5m0 3v.5"/></>,
    cpu: <><rect x="6" y="6" width="12" height="12" rx="2"/><rect x="9" y="9" width="6" height="6" rx="1"/><path d="M9 2v4m6-4v4M9 18v4m6-4v4M2 9h4m-4 6h4m12-6h4m-4 6h4"/></>,
    help: <><circle cx="12" cy="12" r="9"/><path d="M9 8a3 3 0 0 1 6 0c0 2-3 2-3 5m0 3v.5"/></>,
  };
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name] ?? paths.devices}</svg>;
}

export function Status({ children, tone = 'muted' }: { children: ReactNode; tone?: Tone }) {
  return <span className={`fg-status tone-${tone}`}>{tone === 'warning' || tone === 'danger' ? <Icon name="warning" size={14}/> : <i aria-hidden="true"/>}{children}</span>;
}

interface ConsoleState {
  preview: boolean; scenario: Scenario; data: DashboardData; connected: boolean;
  saving: boolean; setSaving: (value: boolean) => void;
  draftDirty: boolean; setDraftDirty: (value: boolean) => void;
}
const ConsoleContext = createContext<ConsoleState | null>(null);
export function useConsole() {
  const value = useContext(ConsoleContext);
  if (!value) throw new Error('Console provider required');
  return value;
}
export function ConsoleLink({ href, children, className }: { href: string; children: ReactNode; className?: string }) {
  const { preview, saving, draftDirty } = useConsole();
  return <Link className={className} href={href + (preview ? '?preview=1' : '')} aria-disabled={saving || undefined} onClick={event => { if (saving || (draftDirty && !window.confirm('配置草稿有未保存的修改。要离开此页面吗？'))) event.preventDefault(); }}>{children}</Link>;
}

const subscribe = (listener: () => void) => fieldGateConfigurator.on('connection', listener);
const connectionSnapshot = () => fieldGateConfigurator.getConnection() !== null;
export const consoleApps = [ ['/', 'gauge', '总览', 'overview'], ['/preview', 'preview', '车辆预览', 'preview'], ['/devices', 'devices', '设备', 'devices'], ['/diagnostics', 'diagnostics', '诊断', 'diagnostics'], ['/settings', 'settings', '设置', 'settings'], ['/robot','cpu','算力与感知','settings'] ];
const nav = [['/launchpad', 'launchpad', '桌面', 'launchpad'], ...consoleApps.filter(([href])=>href!=='/robot')];

export function AppIcon({ icon, color, small = false }: { icon: string; color: string; small?: boolean }) {
  return <span className={`fg-app-icon app-${icon === 'cpu' ? 'robot' : color}${small ? ' is-small' : ''}`}><Icon name={icon} size={small ? 24 : 38}/></span>;
}

export function ConsoleShell({ children }: { children: ReactNode }) {
  const path = usePathname();
  const search = useSearchParams();
  const preview = search.get('preview') === '1';
  const connected = useSyncExternalStore(subscribe, connectionSnapshot, () => false);
  const support = useConnectionSupport();
  const [scenario, setScenario] = useState<Scenario>('working');
  const [saving, setSaving] = useState(false);
  const [draftDirty, setDraftDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => { exposeConfiguratorApi(); }, []);
  const data = preview ? previewData(scenario) : offlineData;

  async function connectionAction() {
    if (preview || saving || busy) return;
    setBusy(true); setError('');
    try {
      if (connected) await fieldGateConfigurator.disconnect();
      else await fieldGateConfigurator.connect({ baudRate: 115200 });
    } catch (cause) {
      setError(cause instanceof Error && cause.name === 'NotFoundError'
        ? '未选择设备。连接线插好后，可重新连接。'
        : '连接未完成。请检查电源和数据线，并关闭占用串口的软件。');
    } finally { setBusy(false); }
  }

  return <ConsoleContext.Provider value={{ preview, scenario, data, connected, saving, setSaving, draftDirty, setDraftDirty }}>
    <div className={`fg-app fg-desktop${path === '/launchpad' ? ' is-launchpad' : ''}`}>
      <svg className="fg-wallpaper" viewBox="0 0 1440 1000" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">
        <defs>
          <linearGradient id="fg-wave-light" x1="0" y1="0" x2="1" y2="1"><stop stopColor="var(--color-wave-ice)"/><stop offset="1" stopColor="var(--color-wave-cyan)"/></linearGradient>
          <linearGradient id="fg-wave-blue" x1="0" y1="0" x2="1" y2="1"><stop stopColor="var(--color-wave-sky)"/><stop offset="1" stopColor="var(--color-wave-blue)"/></linearGradient>
        </defs>
        <path fill="url(#fg-wave-light)" d="M-200-100H720C390 205 652 503 1610 878V1200C402 975 320 406-200 351Z"/>
        <path fill="url(#fg-wave-blue)" d="M-180-160H365C330 161 778 380 1700 779V1100C860 737 364 360-180 222Z"/>
        <path className="fg-wave-edge" d="M-200 350C320 406 402 975 1610 1130"/>
      </svg>
      <a className="fg-skip" href="#fg-main">跳到主要内容</a>
      <div className="fg-workspace">
        <header className="fg-topbar">
          <GlassOptics id="fg-topbar-lens"/>
          <ConsoleLink href="/launchpad" className="fg-desktop-brand"><span className="fg-desktop-mark" aria-hidden="true">F</span><span>FieldGate <small>VCU</small></span></ConsoleLink>
          <div className="fg-toolbar"><GlassEffectsSwitch/><Status tone={preview ? 'warning' : 'muted'}>{connectionLabel(preview, connected, support)}</Status>
            <Link className="fg-button fg-quiet" href={preview ? path : path + '?preview=1'} aria-disabled={saving || undefined} onClick={e => { if (saving || (draftDirty && !window.confirm('配置草稿有未保存的修改。要切换模式吗？'))) e.preventDefault(); }}><Icon name={preview ? 'close' : 'eye'}/>{preview ? '退出预览' : '界面预览'}</Link>
            {!preview && <button className="fg-button fg-primary" onClick={() => void connectionAction()} disabled={busy || saving || (!connected && support !== 'available')} aria-busy={busy}><Icon name="link"/>{busy ? '连接处理中…' : connected ? '断开连接' : '连接设备'}</button>}
          </div>
        </header>
        {preview && <div className="fg-preview-bar"><div><Icon name="eye" size={18}/><strong>界面预览</strong><span>示例数据，不连接或控制设备</span></div><label>场景<select aria-label="预览场景" value={scenario} onChange={e => setScenario(e.target.value as Scenario)}><option value="working">植保作业</option><option value="standby">停车待机</option><option value="warning">液位预警</option></select></label></div>}
        {!preview && !connected && path !== '/settings' && (support === 'insecure' || support === 'unsupported') && <div className="fg-notice" role="status">{connectionHelp(support)}</div>}
        {error && <div className="fg-notice tone-danger" role="alert"><Icon name="warning"/>{error}<button className="fg-button fg-quiet" onClick={() => setError('')}>关闭提示</button></div>}
        <main key={path} id="fg-main" className="fg-content" tabIndex={-1}>{children}</main>
        <footer className="fg-desktop-credit">FieldGate VCU <span>·</span> 刘泽禹</footer>
      </div>
      <nav className="fg-dock" aria-label="主导航"><GlassOptics id="fg-dock-lens"/>{nav.map(([href, icon, name, color]) => <Link key={href} href={href + (preview ? '?preview=1' : '')} className={path === href ? 'fg-dock-link is-current' : 'fg-dock-link'} aria-current={path === href ? 'page' : undefined} aria-disabled={saving || undefined} onClick={event => { if (saving || (draftDirty && path!==href && !window.confirm('配置草稿有未保存的修改。要离开此页面吗？'))) event.preventDefault(); }}><AppIcon icon={icon} color={color} small/><span>{name === '车辆预览' ? '预览' : name}</span>{name === '诊断' && data.alerts.length > 0 && <b className="fg-dock-count">{data.alerts.length}</b>}</Link>)}</nav>
    </div>
  </ConsoleContext.Provider>;
}

export function PageHeading({ title, description, children }: { title: string; description?: string; children?: ReactNode }) {
  return <div className="fg-page-heading"><div><h1>{title}</h1>{description && <p>{description}</p>}</div>{children}</div>;
}
export function PanelHeading({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="fg-panel-heading"><h2>{title}</h2>{children}</div>;
}
export function number(value: number | null, digits = 0) {
  return value === null ? '—' : new Intl.NumberFormat('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}
