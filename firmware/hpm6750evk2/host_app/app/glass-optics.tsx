"use client";

import { useState, useSyncExternalStore } from 'react';
import LiquidGlass from 'simple-liquid-glass';

const preferenceQueries = ['(prefers-reduced-motion: reduce)', '(prefers-reduced-transparency: reduce)', '(prefers-contrast: more)', '(forced-colors: active)'];

function subscribePreferences(changed: () => void) {
  const queries = preferenceQueries.map(query => window.matchMedia(query));
  queries.forEach(query => query.addEventListener('change', changed));
  return () => queries.forEach(query => query.removeEventListener('change', changed));
}

function readPreferences() {
  if (!/Chrome\//.test(navigator.userAgent) || !CSS.supports('backdrop-filter', 'blur(1px)')) return 'opaque';
  const [motion, ...opaque] = preferenceQueries.map(query => window.matchMedia(query).matches);
  return opaque.some(Boolean) ? 'opaque' : motion ? 'still' : 'live';
}

const serverPreferences = () => 'opaque';
const effectsKey = 'fieldgate.glass-effects.v1';
const effectsEvent = 'fieldgate:glass-effects';
let sessionChoice: boolean | undefined;

function readEffects() {
  if (sessionChoice !== undefined) return sessionChoice;
  try { return localStorage.getItem(effectsKey) !== 'off'; }
  catch { return true; }
}

function subscribeEffects(changed: () => void) {
  const storageChanged = (event: StorageEvent) => {
    if (event.key === effectsKey || event.key === null) { sessionChoice = undefined; changed(); }
  };
  window.addEventListener(effectsEvent, changed);
  window.addEventListener('storage', storageChanged);
  return () => {
    window.removeEventListener(effectsEvent, changed);
    window.removeEventListener('storage', storageChanged);
  };
}

const serverEffects = () => false;

export function GlassEffectsSwitch() {
  const enabled = useSyncExternalStore(subscribeEffects, readEffects, serverEffects);
  const [temporary, setTemporary] = useState(false);
  function change(next: boolean) {
    sessionChoice = next;
    try { localStorage.setItem(effectsKey, next ? 'on' : 'off'); setTemporary(false); }
    catch { setTemporary(true); }
    window.dispatchEvent(new Event(effectsEvent));
  }
  return <div className="fg-effects-control">
    <label className="fg-effects-switch" title="仅影响顶栏和程序坞；系统的减少透明度设置优先">
      <input type="checkbox" role="switch" aria-label="玻璃特效" checked={enabled} onChange={event => change(event.target.checked)}/>
      <span className="fg-switch-track" aria-hidden="true"/><span>玻璃特效</span>
    </label>
    {temporary && <span className="fg-effects-note" role="status">此设置仅在本次使用中生效</span>}
  </div>;
}

// The upstream library owns all refraction and highlights. This adapter only
// mounts it after hydration and keeps the original navigation in the foreground.
export function GlassOptics({ id }: { id: string }) {
  const enabled = useSyncExternalStore(subscribeEffects, readEffects, serverEffects);
  const preferences = useSyncExternalStore(subscribePreferences, readPreferences, serverPreferences);
  const active = enabled && preferences !== 'opaque';
  return <div id={id} className="fg-glass-optics" aria-hidden="true" data-glass-provider={active ? 'simple-liquid-glass' : undefined}>
    {active && <LiquidGlass
      className="fg-glass-vendor"
      style={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }}
      radius={id === 'fg-dock-lens' ? 28 : 24}
      material="clear"
      renderer="svg"
      refraction="lens"
      quality="high"
      blur={6}
      saturation={115}
      glassColor="rgba(234,245,255,0.24)"
      liquid={false}
    >{null}</LiquidGlass>}
  </div>;
}
