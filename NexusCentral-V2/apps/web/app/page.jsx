"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CentralClient, ElectronIpcTransport, HttpTransport } from "@nexuscentral/central-client";

import Workbench from "./workbench.jsx";
import "./workbench.css";
const providerDefaults = {
  "lm-studio": ["LM Studio", "http://127.0.0.1:1234/v1"], ollama: ["Ollama", "http://127.0.0.1:11434/v1"], llamacpp: ["llama.cpp", "http://127.0.0.1:8080/v1"], vllm: ["vLLM", "http://127.0.0.1:8000/v1"], localai: ["LocalAI", "http://127.0.0.1:8080/v1"],
  openrouter: ["OpenRouter", "https://openrouter.ai/api/v1"], openai: ["OpenAI", "https://api.openai.com/v1"], google: ["Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai"], "google-native": ["Google Gemini native", "https://generativelanguage.googleapis.com/v1beta"], vertex: ["Google Vertex AI", ""],
  "nvidia-nim": ["NVIDIA NIM", "https://integrate.api.nvidia.com/v1"], anthropic: ["Anthropic", "https://api.anthropic.com/v1"], "azure-openai": ["Azure OpenAI", ""], "aws-bedrock": ["AWS Bedrock", "us-east-1"],
  groq: ["Groq", "https://api.groq.com/openai/v1"], together: ["Together AI", "https://api.together.xyz/v1"], fireworks: ["Fireworks AI", "https://api.fireworks.ai/inference/v1"], mistral: ["Mistral AI", "https://api.mistral.ai/v1"], cerebras: ["Cerebras", "https://api.cerebras.ai/v1"], deepinfra: ["DeepInfra", "https://api.deepinfra.com/v1/openai"], custom: ["OpenAI-compatible", ""]
};

function useCentral() {
  const client = useMemo(() => new CentralClient(typeof window !== "undefined" && window.nexusCentral
    ? new ElectronIpcTransport(window.nexusCentral)
    : new HttpTransport(typeof window === "undefined" ? "http://127.0.0.1" : window.location.origin)), []);
  const [state, setState] = useState(null);
  const [error, setError] = useState("");
  const refresh = async () => { try { setState(await client.getState()); setError(""); return true; } catch (value) { setError(value.message); return false; } };
  useEffect(() => {
    let active = true;
    let unsubscribe = () => {};
    refresh().then((authorized) => {
      if (!active || !authorized) return;
      unsubscribe = client.subscribe((event) => { if ((event.type === "state.changed" && event.data?.command !== "system.state") || event.type.startsWith("job.") || event.type.startsWith("experiment.") || event.type.startsWith("workflow.")) refresh(); });
    });
    return () => { active = false; unsubscribe(); };
  }, []);
  return { client, state, error, refresh };
}

export default function Home() {
  const { client, state, error, refresh } = useCentral();
  if (!state && error) return <PairScreen />;
  return <Workbench client={client} state={state} refresh={refresh} advanced={<>
    <details className="wb-advanced"><summary>AI connections</summary><Router client={client} state={state} refresh={refresh}/></details>
    <details className="wb-advanced"><summary>Models</summary><Models client={client} state={state} refresh={refresh}/></details>
    <details className="wb-advanced"><summary>Devices, access, and runtime</summary><Settings client={client} state={state} refresh={refresh}/></details>
    <details className="wb-advanced"><summary>Existing coding experiments</summary><Lab client={client} state={state} refresh={refresh}/><Review client={client} state={state} refresh={refresh}/></details>
  </>}/>;
}

function PairScreen() {
  const [code, setCode] = useState("");
  const [clientName, setClientName] = useState("");
  const [error, setError] = useState("");
  const pair = async () => {
    const response = await fetch("/api/v1/pair", { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify({ code, clientName: clientName || "LAN browser" }) });
    const body = await response.json();
    if (!response.ok || body.ok === false) { setError(body.error?.message || "Pairing failed."); return; }
    location.reload();
  };
  return <main className="pair-screen"><section><small>NEXUS CENTRAL WEB</small><h1>Join this workspace.</h1><p>Enter the short-lived code shown by the host.</p><label>Device name<input value={clientName} onChange={(event) => setClientName(event.target.value)} placeholder="My computer" /></label><label>Pairing code<input value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))} inputMode="numeric" placeholder="000000" /></label>{error && <div className="notice error">{error}</div>}<button className="primary" disabled={code.length !== 6} onClick={pair}>Join Nexus Central Web</button></section></main>;
}

function Lab({ client, state, refresh }) {
  const [task, setTask] = useState("");
  const [workspacePath, setWorkspacePath] = useState("");
  const [busy, setBusy] = useState(false);
  const jobs = state?.jobs?.jobs || [];
  const canOperate = state?.actor?.scopes?.some((scope) => scope === "admin" || scope === "workspace.operate");
  const send = async () => {
    if (!task.trim()) return;
    setBusy(true);
    try {
      if (canOperate && state?.workspace?.selected) await client.submitLabRun({ task });
      else await client.submitInference({ messages: [{ role: "user", content: task }], contextLength: 16000, maxOutputTokens: 16000 });
      setTask(""); await refresh();
    }
    finally { setBusy(false); }
  };
  const keyDown = (event) => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); send(); } };
  const openWorkspace = async () => {
    const selected = await client.chooseWorkspace();
    const path = selected || workspacePath;
    if (path) await client.openWorkspace({ path }).then(refresh);
  };
  return <section className="workbench">
    <aside><div className="section-title"><h2>Workspace</h2><span>{state?.workspace?.selected?.name || "No folder"}</span></div>
      {canOperate ? <div className="inline"><input value={workspacePath} onChange={(event) => setWorkspacePath(event.target.value)} placeholder="Host folder path" /><button onClick={openWorkspace}>Open</button></div> : <p className="muted">Host approval is required for workspace operations.</p>}
      <div className="file-empty">Files remain on the host machine.</div>
    </aside>
    <article className="conversation"><div className="session-line"><span>Session</span><b>local-host</b><span>{state?.providers?.activeProviderId ? `Route: ${state.providers.activeProviderId}` : "Route setup needed"}</span></div>
      <div className="transcript">{jobs.length ? jobs.slice(-6).map((job) => <div className="message" key={job.id}><span>{job.status}</span><p>{job.payload?.messages?.at(-1)?.content || job.kind}</p>{job.progress?.length > 0 && <p className="answer">{job.progress.map((part) => part.content).join("")}</p>}</div>) : <div className="empty"><small>HOST-ROUTED WORKSPACE</small><h1>Build in the Lab.</h1><p>Ask for a change. Nexus Central routes the work, preserves the workspace, and records the result.</p></div>}</div>
      <div className="composer"><button title="Add attachment" aria-label="Add attachment">+</button><textarea value={task} onChange={(event) => setTask(event.target.value)} onKeyDown={keyDown} placeholder="Describe what to build or ask..." /><button className="send" disabled={busy || !task.trim()} onClick={send} title="Send" aria-label="Send">↑</button></div>
    </article>
  </section>;
}

function Router({ client, state, refresh }) {
  const [form, setForm] = useState({ id: "lm-studio", kind: "lm-studio", name: "LM Studio", baseUrl: "http://127.0.0.1:1234/v1", modelId: "", sharePolicy: { enabled: false, concurrencyLimit: 1, tokensPerMinute: 16000 } });
  const [status, setStatus] = useState("");
  const save = async () => { try { setStatus("Testing connection..."); await client.saveProvider(form); await client.testProvider(form.id); await refresh(); setStatus("Provider is ready."); } catch (error) { setStatus(error.message); } };
  const admin = state?.actor?.scopes?.includes("admin");
  const choose = (kind) => setForm({ id: kind, kind, name: providerDefaults[kind][0], baseUrl: providerDefaults[kind][1], modelId: "", sharePolicy: { enabled: false, concurrencyLimit: 1, tokensPerMinute: 16000 } });
  const rationale = state?.routing?.decisions?.at(-1)?.rationale;
  const route = state?.routing?.decisions?.at(-1)?.selected;
  return <Surface title="Router" intro="Choose where host-controlled inference runs.">{admin && <><div className="form-grid"><label>Provider<select value={form.kind} onChange={(event) => choose(event.target.value)}>{Object.entries(providerDefaults).map(([id, value]) => <option key={id} value={id}>{value[0]}</option>)}</select></label><label>{form.kind === "aws-bedrock" ? "Region" : "Endpoint"}<input value={form.baseUrl} onChange={(event) => setForm({ ...form, baseUrl: event.target.value })} /></label><label>Model<input value={form.modelId} onChange={(event) => setForm({ ...form, modelId: event.target.value })} placeholder="Select after testing" /></label><label>Credential<input type="password" value={form.apiKey || ""} onChange={(event) => setForm({ ...form, apiKey: event.target.value })} placeholder={form.kind === "aws-bedrock" ? "AWS credential JSON" : "Stored by the host"} /></label><label className="check-line"><input type="checkbox" checked={form.sharePolicy.enabled} onChange={(event) => setForm({ ...form, sharePolicy: { ...form.sharePolicy, enabled: event.target.checked } })} />Share this provider with approved peers</label>{form.sharePolicy.enabled && <><label>Peer concurrency<input type="number" min="1" max="16" value={form.sharePolicy.concurrencyLimit} onChange={(event) => setForm({ ...form, sharePolicy: { ...form.sharePolicy, concurrencyLimit: Number(event.target.value) } })} /></label><label>Peer tokens per minute<input type="number" min="1000" max="1000000" step="1000" value={form.sharePolicy.tokensPerMinute} onChange={(event) => setForm({ ...form, sharePolicy: { ...form.sharePolicy, tokensPerMinute: Number(event.target.value) } })} /></label></>}</div><button className="primary" onClick={save}>Save and test</button>{status && <p className="muted option-status">{status}</p>}</>}<List items={state?.providers?.profiles} empty="No providers configured." />{route && <p className="muted rationale">Current route: {route.id}. {rationale}</p>}</Surface>;
}

function Models({ client, state, refresh }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [status, setStatus] = useState("");
  const admin = state?.actor?.scopes?.includes("admin");
  const run = async (label, action) => { setStatus(label); try { await action(); await refresh(); setStatus(`${label} complete.`); } catch (error) { setStatus(error.message); } };
  const search = async () => { setStatus("Searching Hugging Face..."); try { setResults(await client.searchModels(query)); setStatus(""); } catch (error) { setStatus(error.message); } };
  return <Surface title="Models" intro="Find models, then let LM Studio own download and runtime state.">
    <div className="action-line"><input value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={(event) => event.key === "Enter" && search()} placeholder="Search Hugging Face GGUF models" /><button onClick={search}>Search</button>{admin && <button onClick={() => run("Refreshing installed models", () => client.listInstalledModels())}>Refresh installed</button>}</div>
    {status && <p className="muted">{status}</p>}
    {results.length > 0 && <div className="rows">{results.map((model) => <div className="row" key={model.id}><div><b>{model.name}</b><span>{model.downloads.toLocaleString()} downloads</span></div>{admin && <button onClick={() => run(`Downloading ${model.name}`, () => client.downloadModel(model.id))}>Download</button>}</div>)}</div>}
    <div className="divider" /><h3>Local catalog</h3>
    {(state?.models?.models || []).length ? <div className="rows">{state.models.models.map((model) => <div className="row" key={model.id}><div><b>{model.name}</b><span>{model.availability} · {model.contextLength.toLocaleString()} context</span></div>{admin && model.availability !== "loaded" && <button onClick={() => run(`Loading ${model.name}`, () => client.loadModel(model.id))}>Load</button>}</div>)}</div> : <p className="muted">No local models registered.</p>}
    <div className="divider" /><h3>Connected runtimes</h3>
    {(state?.runtimes?.runtimes || []).length ? <div className="rows">{state.runtimes.runtimes.map((runtime) => <div className="row" key={runtime.id}><div><b>{runtime.modelId}</b><span>{runtime.kind} · {runtime.executionProvider}</span></div>{admin && runtime.kind === "lm-studio" && <button onClick={() => run(`Unloading ${runtime.modelId}`, () => client.unloadModel(runtime.id.replace("lm-studio:", "")))}>Unload</button>}</div>)}</div> : <p className="muted">No host or browser runtimes available.</p>}
  </Surface>;
}

function Review({ client, state, refresh }) {
  return <Surface title="Review" intro="Inspect disposable runs and promote only approved changes."><h3>Experiments</h3><ExperimentList client={client} experiments={state?.experiments?.experiments} refresh={refresh} admin={state?.actor?.scopes?.includes("admin")} /><div className="divider" /><h3>Runs</h3><List items={state?.runs?.runs} empty="No work has run." /><div className="divider" /><h3>Artifacts</h3><ArtifactList artifacts={state?.artifacts?.artifacts} /><div className="divider" /><h3>Proof</h3><List items={state?.proof?.records} empty="No proof recorded." /><div className="divider" /><h3>Audit receipts</h3><List items={state?.audit?.entries?.slice(-20)} empty="No command receipts recorded." /></Surface>;
}

function ArtifactList({ artifacts = [] }) {
  if (!artifacts.length) return <p className="muted">No artifacts recorded.</p>;
  return <div className="rows">{[...artifacts].reverse().map((artifact) => <div className="row" key={artifact.id}><div><b>{artifact.path.split("/").at(-1)}</b><span>{artifact.kind} · {artifact.bytes.toLocaleString()} bytes · {artifact.hash.slice(0, 12)}</span></div><a className="button-link" href={`/api/v1/artifacts/${encodeURIComponent(artifact.id)}`}>Download</a></div>)}</div>;
}

function ExperimentList({ client, experiments = [], refresh, admin }) {
  const [status, setStatus] = useState("");
  const action = async (label, callback) => { try { setStatus(label); await callback(); await refresh(); setStatus(""); } catch (error) { setStatus(error.message); } };
  if (!experiments?.length) return <p className="muted">No disposable harness runs.</p>;
  return <>{status && <p className="muted">{status}</p>}<div className="rows">{[...experiments].reverse().map((run) => <div className="experiment" key={run.id}><div><b>{run.task}</b><span>{run.harnessId} · {run.status} · attempt {run.attempt}/{run.maxAttempts}</span>{run.diff && <span>{run.diff.files.length} changed files · {run.validation?.passed ? "validation passed" : "validation not passed"}</span>}</div>{admin && <div className="row-actions">{run.status === "review" && !run.diff?.empty && <button onClick={() => action("Approving evidence...", () => client.approveExperiment(run.id))}>Approve</button>}{run.status === "approved" && <button className="primary" onClick={() => action("Applying approved diff...", () => client.promoteExperiment(run.id))}>Apply</button>}</div>}</div>)}</div></>;
}

function Settings({ client, state, refresh }) {
  const [pairing, setPairing] = useState(null);
  const admin = state?.actor?.scopes?.includes("admin");
  return <Surface title="Settings" intro="Host access, connected browsers, and contributed compute.">
    {admin && <>
      <VersionSettings />
      <div className="divider" />
      <ServiceSettings client={client} service={state?.service} refresh={refresh} />
      <div className="divider" />
      <MeshSettings client={client} mesh={state?.peers} policy={state?.policy} refresh={refresh} />
      <div className="divider" />
      <HarnessSettings client={client} harnesses={state?.harnesses} refresh={refresh} />
      <div className="divider" />
      <div className="settings-row"><div><h3>Nexus Central Web</h3><p>Create a short-lived code for a browser on this LAN.</p></div><button onClick={() => client.createPairing().then(setPairing)}>Create pairing code</button></div>
      {pairing && <div className="pairing"><b>{pairing.code}</b><span>Expires in five minutes</span></div>}
      <div className="divider" />
    </>}
    <BrowserCompute client={client} refresh={refresh} />
    {admin && <><div className="divider" /><h3>Connected sessions</h3>{(state?.sessions?.sessions || []).length ? <div className="rows">{state.sessions.sessions.map((session) => <div className="row" key={session.id}><div><b>{session.clientName}</b><span>{session.scopes.join(" · ")}</span></div><button onClick={() => client.revokeSession(session.id).then(refresh)}>Revoke</button></div>)}</div> : <p className="muted">No paired browsers.</p>}</>}
  </Surface>;
}

function VersionSettings() {
  const [catalog, setCatalog] = useState(null);
  const [selected, setSelected] = useState("");
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!window.nexusCentral?.getVersions) return;
    window.nexusCentral.getVersions().then((value) => { setCatalog(value); setSelected(value.active || value.recommended); }).catch((error) => setStatus(error.message));
  }, []);
  if (typeof window === "undefined" || !window.nexusCentral?.getVersions) return null;
  const entry = catalog?.versions?.find((item) => item.version === selected);
  const install = async () => {
    if (!entry) return;
    setBusy(true);
    setStatus(`${selected === "main" ? "Refreshing" : "Installing"} ${selected}...`);
    try { await window.nexusCentral.installVersion(selected); setStatus("Restarting NexusCentral..."); }
    catch (error) { setStatus(error.message); setBusy(false); }
  };
  return <div className="settings-row"><div><h3>NexusCentral version</h3><p>Install a registered ESM runtime from a frozen version branch or follow latest main.</p><span className="muted">{status || (catalog ? `Running ${catalog.active} · ${catalog.channel}` : "Loading registered versions...")}</span></div><div className="compact-action"><select className="compact-select" value={selected} onChange={(event) => setSelected(event.target.value)}>{(catalog?.versions || []).map((item) => <option key={item.version} value={item.version} disabled={!item.installable}>{item.version === "main" ? "Latest Main" : item.version} — {item.status}{item.recommended ? " · Recommended" : ""}</option>)}</select><button className="primary" disabled={busy || !catalog?.canInstall || !entry?.installable || (selected === catalog?.active && selected !== "main")} onClick={install}>{selected === "main" && selected === catalog?.active ? "Refresh" : "Install"}</button></div></div>;
}

function ServiceSettings({ client, service, refresh }) {
  const [status, setStatus] = useState("");
  const action = async (label, callback) => { try { setStatus(label); await callback(); await refresh(); setStatus(""); } catch (error) { setStatus(error.message); } };
  const description = service?.running ? "LaunchAgent loaded" : service?.installed ? "Installed, not loaded" : service?.supported ? "Not installed" : "macOS LaunchAgent unavailable";
  return <div className="settings-row"><div><h3>Always-running server</h3><p>Start Nexus Node at login and restart it after failures.</p><span className="muted">{status || description}</span></div>{service?.supported && <div className="compact-action">{service.installed ? <button onClick={() => action("Removing service...", () => client.uninstallService())}>Uninstall</button> : <button className="primary" onClick={() => action("Installing service...", () => client.installService())}>Install</button>}</div>}</div>;
}

function MeshSettings({ client, mesh, policy, refresh }) {
  const peers = mesh?.peers || [];
  const [lanCode, setLanCode] = useState(""); const [roomCode, setRoomCode] = useState(""); const [brokerUrl, setBrokerUrl] = useState("http://127.0.0.1:47900/peer");
  const [stunUrl, setStunUrl] = useState("stun:stun.l.google.com:19302"); const [turn, setTurn] = useState({ url: "", username: "", credential: "" });
  const [status, setStatus] = useState("");
  const connect = async (transport, code, payload = {}) => { try { setStatus(`Connecting ${transport}...`); await client.connectMesh({ transport, code, ...payload }); if (transport === "lan") setLanCode(""); else setRoomCode(""); setStatus(`${transport === "lan" ? "LAN mesh" : "Remote room"} active.`); await refresh(); } catch (error) { setStatus(error.message); } };
  const disconnect = async (transport) => { try { await client.disconnectMesh(transport); setStatus(`${transport === "lan" ? "LAN mesh" : "Remote room"} disconnected.`); await refresh(); } catch (error) { setStatus(error.message); } };
  const connectRoom = () => {
    const iceServers = [...(stunUrl ? [{ urls: stunUrl }] : []), ...(turn.url ? [{ urls: turn.url, username: turn.username, credential: turn.credential }] : [])];
    return connect("peerjs", roomCode, { brokerUrl, iceServers });
  };
  const grantId = (peer) => `mesh:${peer.transport}:${peer.nodeId}:inference`; const allowed = (peer) => policy?.grants?.some((grant) => grant.grantId === grantId(peer));
  const permit = async (peer) => { if (allowed(peer)) await client.revokePeerGrant(grantId(peer)); else await client.grantPeerInference(peer.nodeId, grantId(peer)); await refresh(); };
  const lanActive = mesh?.transports?.lan?.running; const roomActive = mesh?.transports?.peerjs?.running;
  return <>
    <div className="settings-row"><div><h3>Local LAN mesh</h3><p>Discover nearby NexusCentral hosts with the same private code.</p><span className="muted">{lanActive ? "Connected" : "Not connected"}</span></div><div className="compact-action">{lanActive ? <button onClick={() => disconnect("lan")}>Disconnect</button> : <><input type="password" value={lanCode} onChange={(event) => setLanCode(event.target.value)} placeholder="LAN code" /><button disabled={!lanCode} onClick={() => connect("lan", lanCode)}>Connect</button></>}</div></div>
    <div className="divider" />
    <div className="settings-row"><div><h3>Remote model room</h3><p>Reach approved local models through direct PeerJS connections.</p><span className="muted">{status || (roomActive ? "Room connected" : "Not connected")}</span></div><div className="room-connect">{roomActive ? <button onClick={() => disconnect("peerjs")}>Disconnect</button> : <><input value={brokerUrl} onChange={(event) => setBrokerUrl(event.target.value)} placeholder="Peer broker URL" /><div className="compact-action"><input type="password" value={roomCode} onChange={(event) => setRoomCode(event.target.value)} placeholder="Room code" /><button disabled={!roomCode || !brokerUrl} onClick={connectRoom}>Connect</button></div><details><summary>Network options</summary><div className="advanced-fields"><label>STUN server<input value={stunUrl} onChange={(event) => setStunUrl(event.target.value)} /></label><label>TURN server<input value={turn.url} onChange={(event) => setTurn({ ...turn, url: event.target.value })} placeholder="turn:host:3478" /></label><label>TURN username<input value={turn.username} onChange={(event) => setTurn({ ...turn, username: event.target.value })} /></label><label>TURN password<input type="password" value={turn.credential} onChange={(event) => setTurn({ ...turn, credential: event.target.value })} /></label></div></details></>}</div></div>
    <div className="divider" /><h3>Connected peers</h3>{peers.length ? <div className="rows">{peers.map((peer) => <div className="row" key={`${peer.transport}:${peer.nodeId}`}><div><b>{peer.displayName || peer.nodeId}</b><span>{peer.transport} · {(peer.capabilities || []).filter((item) => item.action === "mesh.inference").length} shared models</span></div><button onClick={() => permit(peer)}>{allowed(peer) ? "Block" : "Allow inference"}</button></div>)}</div> : <p className="muted">No mesh peers discovered.</p>}
  </>;
}

function HarnessSettings({ client, harnesses, refresh }) {
  const selected = harnesses?.selectedHarnessId || "conversation-harness";
  const profile = harnesses?.profiles?.find((item) => item.id === selected);
  const saved = harnesses?.configurations?.[selected] || {};
  const [options, setOptions] = useState(null);
  const [form, setForm] = useState(saved);
  const [status, setStatus] = useState("");
  useEffect(() => { client.discoverHarnessOptions().then(setOptions).catch((error) => setStatus(error.message)); }, []);
  useEffect(() => { setForm(harnesses?.configurations?.[selected] || {}); }, [selected]);
  const choose = async (id) => { await client.selectHarness(id); await refresh(); };
  const save = async () => { try { await client.configureHarness({ id: selected, ...form }); await refresh(); setStatus("Harness options saved."); } catch (error) { setStatus(error.message); } };
  const validationPreset = JSON.stringify(form.validationCommands || []);
  return <div><div className="settings-row"><div><h3>Assistant connection</h3><p>Every coding run happens in a disposable SimSpace before approval.</p><span className="muted">{options?.codex?.version || options?.codex?.error || status || "Discovering Codex CLI..."}</span></div><select className="compact-select" value={selected} onChange={(event) => choose(event.target.value)}>{(harnesses?.profiles || []).map((harness) => <option key={harness.id} value={harness.id}>{harness.name}</option>)}</select></div><div className="form-grid harness-options"><label>Model<input list="codex-models" value={form.model || ""} onChange={(event) => setForm({ ...form, model: event.target.value })} placeholder="Provider default" /><datalist id="codex-models">{(options?.models || []).map((model) => <option key={model} value={model} />)}</datalist></label><label>Codex profile<input value={form.profile || ""} onChange={(event) => setForm({ ...form, profile: event.target.value })} placeholder="Default profile" /></label><label>Sandbox<select value={form.sandbox || profile?.defaultSandbox || "read-only"} onChange={(event) => setForm({ ...form, sandbox: event.target.value })}>{(profile?.allowedSandboxes || ["read-only"]).map((sandbox) => <option key={sandbox} value={sandbox}>{sandbox}</option>)}</select></label><label>Validation<select value={validationPreset} onChange={(event) => setForm({ ...form, validationCommands: JSON.parse(event.target.value) })}><option value="[]">No command</option><option value='[{"command":"npm","args":["run","check"]}]'>npm run check</option><option value='[{"command":"npm","args":["test"]}]'>npm test</option><option value='[{"command":"dotnet","args":["test"]}]'>dotnet test</option></select></label><label className="check-line"><input type="checkbox" checked={Boolean(form.oss)} onChange={(event) => setForm({ ...form, oss: event.target.checked, localProvider: event.target.checked ? form.localProvider : "" })} />Use local Codex provider</label>{form.oss && <label>Local provider<select value={form.localProvider || "lmstudio"} onChange={(event) => setForm({ ...form, localProvider: event.target.value })}><option value="lmstudio">LM Studio</option><option value="ollama">Ollama</option></select></label>}</div><details><summary>Advanced proof options</summary><div className="form-grid details-grid"><label>NexusSimulator target<input value={form.simulatorTarget || ""} onChange={(event) => setForm({ ...form, simulatorTarget: event.target.value })} placeholder="Relative app path" /></label><label className="check-line"><input type="checkbox" checked={Boolean(form.strictConfig)} onChange={(event) => setForm({ ...form, strictConfig: event.target.checked })} />Reject unknown Codex config</label></div></details><button onClick={save}>Save harness options</button>{status && <p className="muted option-status">{status}</p>}</div>;
}

function BrowserCompute({ client, refresh }) {
  const worker = useRef(null);
  const socket = useRef(null);
  const heartbeat = useRef(null);
  const [status, setStatus] = useState("Not sharing compute");
  const load = async (event) => {
    const [manifestFile, modelFile] = [...event.target.files].sort((a) => a.name.endsWith(".json") ? -1 : 1);
    if (!manifestFile || !modelFile) { setStatus("Choose one manifest JSON and one ONNX/ORT model."); return; }
    const manifest = JSON.parse(await manifestFile.text());
    worker.current ||= new Worker(new URL("../workers/onnx-worker.mjs", import.meta.url), { type: "module" });
    worker.current.onmessage = async ({ data }) => {
      if (data.type === "loaded") {
        const registration = { id: `browser:${data.fingerprint}`, modelId: manifest.id, taskType: manifest.taskType || "tensor", fingerprint: data.fingerprint, executionProvider: data.executionProvider, contextLength: manifest.contextLength || 16000, concurrency: 1, smokePassed: true };
        await client.command("browser.register-runtime", registration);
        clearInterval(heartbeat.current);
        heartbeat.current = setInterval(() => client.command("browser.register-runtime", registration).catch(() => clearInterval(heartbeat.current)), 15000);
        setStatus(`${manifest.name || manifest.id} is available to the host queue.`); await refresh();
      }
      if (data.type === "result" && socket.current?.readyState === WebSocket.OPEN) {
        socket.current.send(JSON.stringify({ type: "worker.chunk", jobId: data.jobId, content: JSON.stringify(data.outputs) }));
        socket.current.send(JSON.stringify({ type: "worker.complete", jobId: data.jobId }));
      }
      if (data.type === "error") setStatus(data.error);
    };
    socket.current ||= new WebSocket(`${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/api/v1/events`);
    socket.current.onmessage = ({ data }) => {
      const message = JSON.parse(data);
      if (message.type === "worker.offer") worker.current?.postMessage({ type: "run", jobId: message.data.jobId, feeds: message.data.request.feeds });
      if (message.type === "worker.cancel") setStatus("Host cancelled the active inference job.");
    };
    setStatus("Loading and testing model...");
    worker.current.postMessage({ type: "load", manifest, bytes: await modelFile.arrayBuffer() }, []);
  };
  return <div className="settings-row"><div><h3>Share browser inference</h3><p>The model stays in this browser. The host decides when to route work here.</p><span className="muted">{status}</span></div><label className="file-button">Choose model package<input type="file" multiple accept=".json,.onnx,.ort" onChange={load} /></label></div>;
}

function Surface({ title, intro, children }) { return <section className="surface"><div className="surface-heading"><h1>{title}</h1><p>{intro}</p></div>{children}</section>; }
function List({ items = [], empty }) { return items?.length ? <div className="rows">{items.map((item, index) => <div className="row" key={item.id || item.jobId || item.nodeId || item.cursor || index}><div><b>{item.name || item.command || item.id || item.jobId || item.nodeId || `Receipt ${item.cursor}`}</b><span>{item.status || item.outcome || item.health || item.kind || item.format || "available"}</span></div></div>)}</div> : <p className="muted">{empty}</p>; }
