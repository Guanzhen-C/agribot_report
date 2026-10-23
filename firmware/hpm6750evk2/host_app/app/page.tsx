import type { Metadata } from "next";
import { Overview } from "./overview";

export const metadata: Metadata = {
  title: "运行总览 · FieldGate",
  description: "农机电量、运行状态与作业功能总览。",
};

export default function Home() {
  return <Overview />;
}
