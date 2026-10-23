"use client";

import { useSyncExternalStore } from 'react';

type ConnectionSupport = 'checking' | 'available' | 'insecure' | 'unsupported';
const subscribe = () => () => {};
const serverSnapshot = (): ConnectionSupport => 'checking';
function snapshot(): ConnectionSupport {
  if (!window.isSecureContext) return 'insecure';
  return navigator.serial ? 'available' : 'unsupported';
}

export function useConnectionSupport() {
  return useSyncExternalStore(subscribe, snapshot, serverSnapshot);
}

export function connectionLabel(preview: boolean, connected: boolean, support: ConnectionSupport) {
  if (preview) return '预览模式';
  if (connected) return '串口已打开';
  if (support === 'checking') return '检查连接条件…';
  if (support === 'insecure') return '当前地址无法连接串口';
  if (support === 'unsupported') return '浏览器无法访问串口';
  return '未连接设备';
}

export function connectionHelp(support: ConnectionSupport) {
  return support === 'insecure'
    ? '当前 HTTP 地址无法访问串口。请在插着设备的电脑上，通过 localhost 地址打开此页面，或使用 HTTPS。其他设备访问网页不会共享这台电脑的串口。'
    : '当前浏览器未提供串口访问。请在连接设备的电脑上使用桌面版 Chrome 或 Edge，并检查浏览器的设备访问权限。';
}
