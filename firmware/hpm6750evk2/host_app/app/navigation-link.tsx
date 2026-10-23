"use client";

import { useSyncExternalStore, type AnchorHTMLAttributes } from 'react';
import Link from 'next/link';

const subscribe = () => () => {};
const clientNavigation = () => typeof globalThis.crypto?.subtle?.digest === 'function';
const serverNavigation = () => false;

export default function NavigationLink(props: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) {
  // LAN HTTP has no crypto.subtle, which vinext needs for RSC navigation.
  // Native links retain normal navigation and the caller's unsaved-change guards.
  const enhanced = useSyncExternalStore(subscribe, clientNavigation, serverNavigation);
  return enhanced ? <Link {...props}/> : <a {...props}/>;
}
