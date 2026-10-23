import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

async function render(path = '/') {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);

  return worker.fetch(
    new Request(`http://localhost${path}`, { headers: { accept: "text/html" } }),
    {
      ASSETS: {
        fetch: async () => new Response("Not found", { status: 404 }),
      },
    },
    {
      waitUntil() {},
      passThroughOnException() {},
    },
  );
}

test("seven FieldGate pages preserve authorship and separate operating information from settings", async () => {
  for (const [path, heading] of [['/', '运行总览'], ['/devices', '设备与功能'], ['/diagnostics', '设备诊断'], ['/settings', '设备设置'], ['/launchpad','工作空间'], ['/preview','车辆预览'], ['/robot','算力与感知']]) {
    const response = await render(path + '?preview=1');
    assert.equal(response.status, 200, path);
    const html = await response.text();
    assert.doesNotMatch(html, /Switched to client rendering because|internal error; reference\s*=/, path);
    assert.match(html, /<title>[^<]*FieldGate/);
    const visible = html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '')
      .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, '').replace(/<[^>]+>/g, '');
    assert.ok(visible.includes(heading), path);
    assert.match(visible, /刘泽禹/);
    assert.match(visible, /示例数据，不连接或控制设备/);
    assert.doesNotMatch(visible, /禾川|stm32|小车|底盘|地盘/i);
    if (path === '/') {
      assert.match(visible, /剩余电量/);
      assert.match(visible, /能量趋势/);
      assert.doesNotMatch(visible, /UART|SocketCAN|FGCP|VCFG|Flash|CRC|J11|0x514/i);
    }
    if (path === '/settings') {
      assert.match(visible, /农机已停稳，作业设备已关闭/);
      assert.match(visible, /三合一 CAN 协议/);
      assert.match(visible, /保存设置/);
    }
  }
});

test("documents the program, configuration, and runtime command APIs", async () => {
  const [api, programDocs, serialDocs, runtimeDocs] = await Promise.all([
    readFile(new URL("../lib/vcu-configurator.ts", import.meta.url), "utf8"),
    readFile(new URL("../docs/PROGRAM_API.md", import.meta.url), "utf8"),
    readFile(new URL("../docs/SERIAL_CONFIG_PROTOCOL.md", import.meta.url), "utf8"),
    readFile(new URL("../docs/RUNTIME_COMMAND_PROTOCOL.md", import.meta.url), "utf8"),
  ]);

  for (const method of [
    "connect",
    "disconnect",
    "listMachines",
    "listProtocols",
    "listComputeInterfaces",
    "previewSelection",
    "writeSelection",
    "registerMachine",
    "registerProtocol",
    "registerComputeInterface",
    "registerTransport",
  ]) {
    assert.match(api, new RegExp(`${method}\\(`));
    assert.match(programDocs, new RegExp(method));
  }
  assert.match(api, /window\.fieldGateConfigurator/);
  assert.match(serialDocs, /VCFG\/1/);
  assert.match(runtimeDocs, /FGCP\/1/);
  assert.match(runtimeDocs, /geometry_msgs\/msg\/Twist/);
  assert.match(runtimeDocs, /J11\[6\]/);
  assert.match(runtimeDocs, /J11\[7\]/);
  assert.match(runtimeDocs, /SocketCAN/);
  assert.match(runtimeDocs, /0x514/);
  assert.doesNotMatch(api, /fetch\(|HttpVcuClient|\/api\/v1/);
});

test("task-focused copy retains configuration scope, export limits, and unknown device states", async () => {
  for (const path of ['/preview', '/robot', '/devices', '/diagnostics', '/settings']) {
    const html = await (await render(path)).text();
    const heading = html.match(/<div class="fg-page-heading">([\s\S]*?)<\/h1>([\s\S]*?)<\/div>/);
    assert.ok(heading, path);
    assert.doesNotMatch(heading[2], /<p\b/, `${path}: no empty or redundant subtitle`);
    const visible = html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '')
      .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, '').replace(/<[^>]+>/g, '');
    assert.doesNotMatch(visible, /只关注执行机构|无需逐个关联|不采用网页缩放|分类仅影响模型管理/);
    if (path === '/preview') {
      assert.match(visible, /本机草稿 · 未写入设备/);
      assert.match(visible, /配置包默认禁止输出，需完成实机验证后启用/);
      assert.match(visible, /保存草稿/);
    }
    if (path === '/robot') {
      assert.match(html, /aria-describedby="urdf-export-note"/);
      assert.match(visible, /未配置运动的关节按固定安装导出；不含惯量与控制驱动/);
      assert.match(visible, /本机草稿/);
    }
    if (path === '/diagnostics') {
      assert.match(visible, /等待有效设备反馈/);
      assert.match(visible, /请检查连接与设备电源/);
      assert.doesNotMatch(visible, /当前无告警/);
      assert.match(visible, /0x532/);
    }
    if (path === '/settings') {
      assert.match(visible, /农机已停稳，作业设备已关闭/);
      assert.match(visible, /请先连接农机/);
      assert.doesNotMatch(html, /class="farm-feedback is-success"/);
    }
  }
});
