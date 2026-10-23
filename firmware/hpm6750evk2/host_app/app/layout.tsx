import type { Metadata } from "next";
import "./globals.css";
import "../tokens.css";
import "./console.css";
import "./desktop.css";
import { ConsoleShell } from "./console-shell";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "FieldGate · 农机控制台",
  description: "FieldGate VCU 农机状态监测、设备诊断与配置。作者：刘泽禹。",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body><Suspense fallback={<div className="fg-empty">正在打开 FieldGate…</div>}><ConsoleShell>{children}</ConsoleShell></Suspense></body>
    </html>
  );
}
