"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Icon, useConsole } from "./console-shell";
import { BUILTIN_COMPUTE_INTERFACES } from "../lib/config-catalog";
import { exposeConfiguratorApi, fieldGateConfigurator, type WriteSelectionResult } from "../lib/vcu-configurator";
import { connectionHelp, connectionLabel, useConnectionSupport } from "./connection-support";

const subscribeConnection = (listener: () => void) => fieldGateConfigurator.on("connection", listener);
const connectionSnapshot = () => fieldGateConfigurator.getConnection() !== null;
const serverConnection = () => false;

function friendlyError(cause: unknown, action: "connect" | "save" | "disconnect") {
  const name = cause instanceof Error ? cause.name : "";
  const message = cause instanceof Error ? cause.message : "";
  if (name === "NotFoundError") return "没有选择设备。连接线插好后，再试一次。";
  if (name === "SecurityError" || name === "NotAllowedError") return "未获得设备访问权限。请在浏览器中允许连接。";
  if (name === "NetworkError" || name === "InvalidStateError") return "设备无法连接。请关闭其他使用该设备的软件，再重新连接。";
  if (/ACK_|超时|确认前关闭/.test(message)) return "未收到农机确认。请检查电源和连接线，重新连接后再保存。";
  if (/不支持所选|不支持当前|不兼容|UNSUPPORTED/.test(message)) return "当前设备不支持这项设置。请核对农机型号，或联系安装人员更新设备。";
  if (/Flash|校验失败/.test(message)) return "农机未能保存设置。请保持电源稳定后重试。";
  if (/停车|待机/.test(message)) return "请先停止农机及作业设备，再保存设置。";
  return action === "connect" ? "连接未完成。请检查连接线并重试。"
    : action === "disconnect" ? "暂时无法断开。请等待保存结束后重试。"
      : "设置未保存。请重新连接农机后再试。";
}

export function VcuConsole() {
  const { preview, setSaving } = useConsole();
  const [machineKey, setMachineKey] = useState(preview ? "tracked_three_in_one" : "");
  const [computeInterfaceKey, setComputeInterfaceKey] = useState(BUILTIN_COMPUTE_INTERFACES[0].key);
  const connection = useSyncExternalStore(subscribeConnection, connectionSnapshot, serverConnection);
  const support = useConnectionSupport();
  const [busy, setBusy] = useState<"connect" | "save" | "disconnect" | null>(null);
  const [stopped, setStopped] = useState(false);
  const [result, setResult] = useState<WriteSelectionResult | null>(null);
  const [error, setError] = useState<{ message: string; action: "connect" | "save" | "disconnect" } | null>(null);
  const [help, setHelp] = useState(false);
  const pending = useRef(false);
  const helpDialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const api = exposeConfiguratorApi();
    return api.on("connection", () => {
      setResult(null);
      setStopped(false);
    });
  }, []);

  useEffect(() => {
    if (help) helpDialog.current?.showModal();
    else helpDialog.current?.close();
  }, [help]);

  const machines = fieldGateConfigurator.listMachines();
  const machine = machines.find((item) => item.key === machineKey);
  const protocol = fieldGateConfigurator.listProtocols(machineKey)[0];
  const compute = fieldGateConfigurator.listComputeInterfaces().find((item) => item.key === computeInterfaceKey);
  const dirty = !result || result.machine.key !== machineKey || result.computeInterface.key !== computeInterfaceKey;
  const canSave = Boolean(!preview && connection && machine && protocol && compute && stopped && dirty && !busy);
  const saveHint = preview ? "预览模式下无法保存。" : !connection ? "请先连接农机。"
    : !machine ? "请选择农机型号。"
      : !dirty ? "本次设置已保存，重启农机后生效。"
      : !stopped ? "请确认农机已停稳。" : "保存期间请保持电源和连接线接通。";

  function clearSaved() {
    setResult(null);
    setError(null);
    setStopped(false);
  }

  async function run(action: "connect" | "save" | "disconnect") {
    if (preview || pending.current || (action === "save" && !canSave)) return;
    pending.current = true;
    setBusy(action);
    setSaving(true);
    setError(null);
    if (action !== "disconnect") setResult(null);
    try {
      if (action === "connect") {
        await fieldGateConfigurator.connect({ baudRate: 115200 });
      } else if (action === "disconnect") {
        await fieldGateConfigurator.disconnect();
      } else if (protocol) {
        const saved = await fieldGateConfigurator.writeSelection({
          machineKey, protocolKey: protocol.key, computeInterfaceKey,
        });
        setResult(saved);
        setStopped(false);
      }
    } catch (cause) {
      setError({ message: friendlyError(cause, action), action });
    } finally {
      pending.current = false;
      setBusy(null);
      setSaving(false);
    }
  }

  return (
    <div className="farm-app">
      <header className="farm-header">
        <span className="fg-subtle">更改配置前，请先停止农机及作业设备。</span>
        <button className="fg-button fg-quiet" onClick={() => setHelp(true)}><Icon name="help" size={18}/>使用帮助</button>
      </header>

      <div id="farm-settings" className="farm-main">
        <div className="farm-workspace">
          <div className="farm-settings">
            <section className="farm-connection" aria-labelledby="connection-heading">
              <div className="farm-section-heading">
                <h2 id="connection-heading">设备连接</h2>
                <span className={connection ? "farm-status is-connected" : "farm-status"} aria-live="polite">
                  {connectionLabel(preview, connection, support)}
                </span>
              </div>
              {!preview && !connection && support === 'available' && <p>用数据线连接电脑与农机，并接通农机电源。</p>}
              {!preview && connection && <p>串口已打开，等待状态反馈。尚未确认设备运行状态。</p>}
              <div className="farm-connect-row">
                <button
                  className="farm-button farm-button-outline"
                  disabled={preview || Boolean(busy) || (!connection && support !== 'available')}
                  onClick={() => void run(connection ? "disconnect" : "connect")}
                  aria-busy={busy === "connect" || busy === "disconnect"}
                >
                  {busy === "connect" ? "正在连接…" : busy === "disconnect" ? "正在断开…" : connection ? "断开连接" : "连接农机"}
                </button>
                {!preview && !connection && support === 'available' && <span>在弹出的窗口中选择设备。</span>}
              </div>
              {!preview && !connection && (support === 'insecure' || support === 'unsupported') && (
                <p className="farm-browser-note" role="status">
                  {connectionHelp(support)}
                </p>
              )}
              {error && error.action !== "save" && <div className="farm-feedback is-error" role="alert">
                <strong>{error.action === "connect" ? "连接未完成" : "断开未完成"}</strong><p>{error.message}</p>
              </div>}
            </section>

            <section className="farm-model" aria-labelledby="model-heading">
              <h2 id="model-heading">农机型号</h2>
              <p id="model-description">请按农机铭牌或安装人员提供的型号选择。</p>
              <label className="farm-field" htmlFor="machine-choice">选择型号</label>
              <select
                id="machine-choice" className="farm-select" value={machineKey}
                aria-describedby={machine ? "model-description machine-description" : "model-description"}
                disabled={busy === "save"}
                onChange={(event) => { setMachineKey(event.target.value); clearSaved(); }}
              >
                <option value="" disabled>请选择农机型号</option>
                {machines.map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
              </select>
              {machine && <p className="farm-field-note" id="machine-description">{machine.description}</p>}

              <details className="farm-options">
                <summary>连接选项</summary>
                <div className="farm-options-body">
                  <label className="farm-field" htmlFor="connection-choice">控制设备连接方式</label>
                  <select
                    id="connection-choice" className="farm-select" value={computeInterfaceKey}
                    disabled={busy === "save"} aria-describedby="connection-description"
                    onChange={(event) => { setComputeInterfaceKey(event.target.value); clearSaved(); }}
                  >
                    {fieldGateConfigurator.listComputeInterfaces().map((item) =>
                      <option key={item.key} value={item.key}>{item.label}</option>)}
                  </select>
                  <p className="farm-field-note" id="connection-description">{compute?.description}</p>
                </div>
              </details>
            </section>

            <section className="farm-save" aria-labelledby="save-heading">
              <h2 id="save-heading">保存设置</h2>
              <label className="farm-stop-check">
                <input type="checkbox" checked={stopped} disabled={Boolean(busy) || !dirty}
                  onChange={(event) => setStopped(event.target.checked)} />
                <span>农机已停稳，作业设备已关闭</span>
              </label>
              <div className="farm-save-row">
                <button
                  className="farm-button farm-button-primary"
                  disabled={!canSave} aria-describedby="save-hint"
                  aria-busy={busy === "save"} onClick={() => void run("save")}
                >
                  {busy === "save" ? "正在保存…" : result && !dirty ? "已保存" : "保存设置"}
                </button>
                <p id="save-hint">{saveHint}</p>
              </div>
              {error?.action === "save" && <div className="farm-feedback is-error" role="alert"><strong>设置未保存</strong><p>{error.message}</p></div>}
              {result && !dirty && <div className="farm-feedback is-success" role="status">
                <strong>设置已保存</strong>
                <p>{result.machine.label}。请重启农机，使新设置生效。</p>
              </div>}
            </section>
          </div>

          <aside className="farm-overview" aria-labelledby="overview-heading">
            <h2 id="overview-heading">本次设置</h2>
            <div className="farm-selected-model">
              <span>农机型号</span>
              <strong>{machine?.label ?? "尚未选择"}</strong>
            </div>
            <dl>
              <div><dt>连接方式</dt><dd>{compute?.label}</dd></div>
              <div><dt>执行器协议</dt><dd>{protocol?.label ?? "选择型号后匹配"}</dd></div>
              <div><dt>保存状态</dt><dd>{result && !dirty ? "已保存" : "尚未保存"}</dd></div>
              <div><dt>生效时间</dt><dd>农机重启后</dd></div>
            </dl>
          </aside>
        </div>
      </div>

      <dialog className="farm-help-dialog" ref={helpDialog} onClose={() => setHelp(false)}
        onClick={(event) => { if (event.target === event.currentTarget) setHelp(false); }}
        aria-labelledby="help-heading">
        <h2 id="help-heading">连接与保存</h2>
        <ol>
          <li>接通农机电源，用数据线连接电脑。</li>
          <li>选择“连接农机”，在弹出的窗口中选择对应设备。</li>
          <li>核对农机型号，确认农机停稳后保存。</li>
          <li>看到“设置已保存”后，重启农机。</li>
        </ol>
        <h3>列表里没有设备？</h3>
        <p>重新插拔数据线，检查电源，并关闭其他正在使用设备的软件。仍无法连接时，请联系安装人员。</p>
        <button className="farm-button farm-button-outline" onClick={() => setHelp(false)} autoFocus>知道了</button>
      </dialog>
    </div>
  );
}
