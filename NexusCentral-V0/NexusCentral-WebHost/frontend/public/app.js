(function () {
  const appState = {
    snapshot: null,
    expandedDirectories: new Set(),
    workspaceTrees: new Map(),
    pollingHandle: null,
    editorDirty: false,
    editorFileKey: "",
    mobileTreeOpen: false,
    mobileMode: new URLSearchParams(window.location.search).get("mobile") === "1",
    activeMainTab: "code",
    localPreviewEvents: [],
  };

  const init = () => {
    document.body.classList.toggle("mobile-lab", appState.mobileMode);
    const elements = {
      webLabStatus: document.getElementById("webLabStatus"),
      connectStatus: document.getElementById("connectStatus"),
      showMobileQrButton: document.getElementById("showMobileQrButton"),
      closeMobileQrButton: document.getElementById("closeMobileQrButton"),
      mobileQrPanel: document.getElementById("mobileQrPanel"),
      mobileQrCanvas: document.getElementById("mobileQrCanvas"),
      mobileQrUrl: document.getElementById("mobileQrUrl"),
      mobileOverlayScrim: document.getElementById("mobileOverlayScrim"),
      mobileFilesButton: document.getElementById("mobileFilesButton"),
      connectSecurelyButton: document.getElementById("connectSecurelyButton"),
      workspaceSelect: document.getElementById("workspaceSelect"),
      agentSelect: document.getElementById("agentSelect"),
      codeTabButton: document.getElementById("codeTabButton"),
      previewTabButton: document.getElementById("previewTabButton"),
      codeTabPanel: document.getElementById("codeTabPanel"),
      previewTabPanel: document.getElementById("previewTabPanel"),
      refreshTreeButton: document.getElementById("refreshTreeButton"),
      workspacePathSummary: document.getElementById("workspacePathSummary"),
      workspaceTree: document.getElementById("workspaceTree"),
      editorFileName: document.getElementById("editorFileName"),
      editorMeta: document.getElementById("editorMeta"),
      fileEditor: document.getElementById("fileEditor"),
      newFileButton: document.getElementById("newFileButton"),
      newFolderButton: document.getElementById("newFolderButton"),
      renamePathButton: document.getElementById("renamePathButton"),
      deletePathButton: document.getElementById("deletePathButton"),
      saveFileButton: document.getElementById("saveFileButton"),
      humanViewPreviewInput: document.getElementById("humanViewPreviewInput"),
      openHumanViewButton: document.getElementById("openHumanViewButton"),
      snapshotHumanViewButton: document.getElementById("snapshotHumanViewButton"),
      humanViewPreviewShell: document.getElementById("humanViewPreviewShell"),
      humanViewFrame: document.getElementById("humanViewFrame"),
      humanViewCursor: document.getElementById("humanViewCursor"),
      humanViewState: document.getElementById("humanViewState"),
      sandboxExportInput: document.getElementById("sandboxExportInput"),
      exportSandboxButton: document.getElementById("exportSandboxButton"),
      transcript: document.getElementById("transcript"),
      chatInput: document.getElementById("chatInput"),
      sendButton: document.getElementById("sendButton"),
    };

    bindEvents(elements);
    bootstrap(elements);
  };

  function bindEvents(elements) {
    elements.workspaceSelect.addEventListener("change", () => selectWorkspace(elements));
    elements.codeTabButton.addEventListener("click", () => switchMainTab(elements, "code"));
    elements.previewTabButton.addEventListener("click", () => switchMainTab(elements, "preview"));
    elements.refreshTreeButton.addEventListener("click", () => refreshWorkspaceTree(elements));
    elements.agentSelect.addEventListener("change", () => selectAgent(elements));
    elements.sendButton.addEventListener("click", () => sendChat(elements));
    elements.chatInput.addEventListener("keydown", event => {
      if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        sendChat(elements);
      }
    });
    elements.saveFileButton.addEventListener("click", () => saveCurrentFile(elements));
    elements.fileEditor.addEventListener("input", () => {
      appState.editorDirty = true;
      updateEditorSaveState(elements);
    });
    elements.newFileButton.addEventListener("click", () => createSandboxFile(elements));
    elements.newFolderButton.addEventListener("click", () => createSandboxDirectory(elements));
    elements.renamePathButton.addEventListener("click", () => renameCurrentPath(elements));
    elements.deletePathButton.addEventListener("click", () => deleteCurrentPath(elements));
    elements.exportSandboxButton.addEventListener("click", () => exportSandbox(elements));
    elements.connectSecurelyButton.addEventListener("click", () => connectSecurely(elements));
    elements.showMobileQrButton.addEventListener("click", () => showMobileQr(elements));
    elements.closeMobileQrButton.addEventListener("click", () => hideMobileQr(elements));
    elements.mobileOverlayScrim.addEventListener("click", () => closeMobilePanels(elements));
    elements.mobileFilesButton.addEventListener("click", () => toggleMobileFiles(elements));
    elements.openHumanViewButton.addEventListener("click", () => openHumanViewPreview(elements));
    elements.snapshotHumanViewButton.addEventListener("click", () => snapshotHumanView(elements));
    elements.humanViewPreviewShell.addEventListener("mousemove", event => {
      const bounds = elements.humanViewPreviewShell.getBoundingClientRect();
      moveHumanViewCursor(elements, event.clientX - bounds.left, event.clientY - bounds.top);
    });
  }

  async function bootstrap(elements) {
    const response = await fetchJson("/api/lab/bootstrap");
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to load Nexus Web Lab.");
      return;
    }

    applySnapshot(elements, response.data, true);
    startPolling(elements);
  }

  function startPolling(elements) {
    if (appState.pollingHandle) {
      window.clearInterval(appState.pollingHandle);
    }

    appState.pollingHandle = window.setInterval(() => {
      refreshState(elements);
    }, 1200);
  }

  function applySnapshot(elements, snapshot, resetSelectors) {
    appState.snapshot = snapshot;
    setStatus(elements, snapshot.web_lab?.status_text ?? "Nexus Web Lab");
    renderHeaderControls(elements, resetSelectors);
    syncSelectedWorkspaceTree(elements);
    renderExplorer(elements);
    renderEditor(elements);
    renderTranscript(elements);
    syncMainTabs(elements);
    syncMobileFileOverlay(elements);
  }

  function renderHeaderControls(elements, resetSelectors) {
    const options = appState.snapshot?.options;
    const workspaces = appState.snapshot?.workspaces ?? [];

    renderSelect(
      elements.workspaceSelect,
      workspaces.map(workspace => ({
        value: workspace.id,
        label: workspace.caption,
        disabled: false,
      })),
      appState.snapshot?.lab?.selected_workspace_id ?? ""
    );

    if (!options && !resetSelectors) {
      if (appState.snapshot?.lab?.selected_agent_id) {
        elements.agentSelect.value = appState.snapshot.lab.selected_agent_id;
      }
      elements.sandboxExportInput.value = appState.snapshot?.lab?.sandbox_export_path_input ?? "";
      return;
    }

    renderSelect(
      elements.agentSelect,
      (options?.agents ?? []).map(agent => ({
        value: agent.id,
        label: agent.available ? formatAgentLabel(agent) : `${formatAgentLabel(agent)} (unavailable)`,
        disabled: !agent.available,
      })),
      appState.snapshot?.lab?.selected_agent_id ?? ""
    );

    elements.sandboxExportInput.value = appState.snapshot?.lab?.sandbox_export_path_input ?? "";
  }

  function renderSelect(select, options, selectedValue) {
    if (!select) {
      return;
    }
    const currentValue = select.value;
    const nextValue = selectedValue || currentValue;
    select.innerHTML = "";
    for (const option of options) {
      const element = document.createElement("option");
      element.value = option.value;
      element.textContent = option.label;
      element.disabled = Boolean(option.disabled);
      if (option.value === nextValue) {
        element.selected = true;
      }
      select.appendChild(element);
    }
  }

  function formatAgentLabel(agent) {
    if (agent?.id === "direct_llm") {
      return "Direct";
    }
    return agent?.name || agent?.id || "";
  }

  function switchMainTab(elements, tabName) {
    appState.activeMainTab = tabName === "preview" ? "preview" : "code";
    syncMainTabs(elements);
  }

  function syncMainTabs(elements) {
    const previewActive = appState.activeMainTab === "preview";
    elements.codeTabButton.classList.toggle("active", !previewActive);
    elements.previewTabButton.classList.toggle("active", previewActive);
    elements.codeTabButton.setAttribute("aria-selected", String(!previewActive));
    elements.previewTabButton.setAttribute("aria-selected", String(previewActive));
    elements.codeTabPanel.classList.toggle("active", !previewActive);
    elements.previewTabPanel.classList.toggle("active", previewActive);
  }

  function syncSelectedWorkspaceTree(elements) {
    const selectedWorkspace = getSelectedWorkspace();
    if (!selectedWorkspace) {
      elements.workspaceTree.innerHTML = "";
      return;
    }

    if (!appState.workspaceTrees.has(selectedWorkspace.id)) {
      void ensureWorkspaceTreeLoaded(elements, selectedWorkspace.id, "");
    }
  }

  function renderExplorer(elements) {
    const selectedWorkspace = getSelectedWorkspace();
    const container = elements.workspaceTree;
    container.innerHTML = "";

    if (!selectedWorkspace) {
      elements.workspacePathSummary.textContent = "Select a workspace to browse files.";
      return;
    }

    elements.workspacePathSummary.textContent = selectedWorkspace.path_summary;

    const rootNodes = appState.workspaceTrees.get(selectedWorkspace.id) ?? [];
    if (rootNodes.length === 0) {
      const empty = document.createElement("div");
      empty.className = "explorer-empty";
      empty.textContent = "No files loaded yet.";
      container.appendChild(empty);
    } else {
      rootNodes.forEach(node => container.appendChild(renderTreeNode(elements, selectedWorkspace, node, 0)));
    }

    const workspaceEditable = Boolean(selectedWorkspace.is_sandbox);
    elements.newFileButton.disabled = !workspaceEditable;
    elements.newFolderButton.disabled = !workspaceEditable;
    elements.renamePathButton.disabled = !workspaceEditable || !appState.snapshot?.lab?.selected_node_path;
    elements.deletePathButton.disabled = !workspaceEditable || !appState.snapshot?.lab?.selected_node_path;
    elements.exportSandboxButton.disabled = !workspaceEditable;
  }

  function renderTreeNode(elements, workspace, node, depth) {
    const wrapper = document.createElement("div");
    const row = document.createElement("div");
    row.className = `node-row${node.full_path === appState.snapshot?.lab?.selected_node_path ? " selected" : ""}`;
    row.style.paddingLeft = `${Math.max(0, depth) * 12}px`;

    const toggleButton = document.createElement("button");
    toggleButton.type = "button";
    toggleButton.className = "tree-toggle";
    toggleButton.textContent = node.is_directory ? (appState.expandedDirectories.has(node.full_path) ? "▾" : "▸") : "";
    toggleButton.disabled = !node.is_directory;
    toggleButton.addEventListener("click", async event => {
      event.stopPropagation();
      if (!node.is_directory) {
        return;
      }

      const expanded = appState.expandedDirectories.has(node.full_path);
      if (expanded) {
        appState.expandedDirectories.delete(node.full_path);
        renderExplorer(elements);
        return;
      }

      await ensureWorkspaceTreeLoaded(elements, workspace.id, node.full_path);
      appState.expandedDirectories.add(node.full_path);
      await postJson("/api/lab/workspaces/node/select", { workspace_id: workspace.id, path: node.full_path });
      await refreshState(elements);
    });

    const kind = document.createElement("div");
    kind.textContent = node.is_directory ? "⌂" : "·";
    kind.className = "tree-kind";

    const label = document.createElement("div");
    label.className = "node-label";
    label.textContent = node.name || node.relative_path || node.full_path;
    label.addEventListener("click", async () => {
      if (!node.is_directory && !canDiscardEditorChanges()) {
        return;
      }
      await postJson("/api/lab/workspaces/node/select", { workspace_id: workspace.id, path: node.full_path });
      if (!node.is_directory) {
        await fetchWorkspaceFile(elements, workspace.id, node.full_path);
      }
      await refreshState(elements);
    });

    row.append(toggleButton, kind, label);
    wrapper.appendChild(row);

    if (node.is_directory && appState.expandedDirectories.has(node.full_path)) {
      const children = node.children ?? [];
      for (const child of children) {
        wrapper.appendChild(renderTreeNode(elements, workspace, child, depth + 1));
      }
    }

    return wrapper;
  }

  async function ensureWorkspaceTreeLoaded(elements, workspaceId, directoryPath) {
    const query = directoryPath
      ? `?workspace_id=${encodeURIComponent(workspaceId)}&directory_path=${encodeURIComponent(directoryPath)}`
      : `?workspace_id=${encodeURIComponent(workspaceId)}`;
    const response = await fetchJson(`/api/lab/workspaces/tree${query}`);
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to load workspace tree.");
      return;
    }

    const nodes = response.data.nodes.map(node => ({ ...node, children: node.children ?? [] }));
    if (!directoryPath) {
      appState.workspaceTrees.set(workspaceId, nodes);
      if (workspaceId === appState.snapshot?.lab?.selected_workspace_id) {
        renderExplorer(elements);
      }
      return;
    }

    const roots = appState.workspaceTrees.get(workspaceId) ?? [];
    const target = findNode(roots, directoryPath);
    if (target) {
      target.children = nodes;
    }
    appState.workspaceTrees.set(workspaceId, roots);
    if (workspaceId === appState.snapshot?.lab?.selected_workspace_id) {
      renderExplorer(elements);
    }
  }

  function findNode(nodes, fullPath) {
    for (const node of nodes) {
      if (node.full_path === fullPath) {
        return node;
      }
      const nested = node.children ? findNode(node.children, fullPath) : null;
      if (nested) {
        return nested;
      }
    }
    return null;
  }

  async function fetchWorkspaceFile(elements, workspaceId, path) {
    const response = await fetchJson(`/api/lab/workspaces/file?workspace_id=${encodeURIComponent(workspaceId)}&path=${encodeURIComponent(path)}`);
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to load workspace file.");
      return null;
    }

    return response.data;
  }

  function renderEditor(elements) {
    const currentFile = appState.snapshot?.current_file;
    const selectedWorkspace = getSelectedWorkspace();

    if (!currentFile) {
      appState.editorDirty = false;
      appState.editorFileKey = "";
      elements.editorFileName.textContent = "No file selected";
      elements.editorMeta.textContent = selectedWorkspace
        ? `${selectedWorkspace.caption} · ${selectedWorkspace.path_summary}`
        : "Select a file from the explorer.";
      elements.fileEditor.value = "";
      elements.fileEditor.disabled = true;
      updateEditorSaveState(elements);
      return;
    }

    const fileKey = `${currentFile.workspace_id || ""}:${currentFile.path || currentFile.relative_path || currentFile.name || ""}`;
    const fileChanged = fileKey !== appState.editorFileKey;
    if (fileChanged) {
      appState.editorDirty = false;
    }
    elements.editorFileName.textContent = currentFile.name || currentFile.relative_path || currentFile.path;
    elements.editorMeta.textContent = currentFile.editable
      ? appState.editorDirty
        ? "Editable file · unsaved changes"
        : "Editable file"
      : "Read-only file";
    elements.fileEditor.disabled = !currentFile.editable;
    if (fileChanged) {
      appState.editorFileKey = fileKey;
      elements.fileEditor.value = currentFile.content ?? "";
    } else if (!appState.editorDirty && elements.fileEditor.value !== (currentFile.content ?? "")) {
      elements.fileEditor.value = currentFile.content ?? "";
    }
    updateEditorSaveState(elements);
  }

  function renderTranscript(elements) {
    const transcript = elements.transcript;
    const previousScrollTop = transcript.scrollTop;
    const distanceFromBottom = transcript.scrollHeight - transcript.scrollTop - transcript.clientHeight;
    const shouldFollow = distanceFromBottom < 80;
    transcript.innerHTML = "";

    for (const message of appState.snapshot?.messages ?? []) {
      const row = document.createElement("article");
      const rowKind = message.is_execution_feed
        ? "feed-row"
        : message.role === "user"
        ? "user-row"
        : message.role === "error"
        ? "error-row"
        : "ai-row";
      row.className = `transcript-row ${rowKind}`;

      const meta = document.createElement("div");
      meta.className = "transcript-meta";
      const roleLabel = message.is_execution_feed
        ? "Feed"
        : message.role === "user"
        ? "User"
        : message.role === "error"
        ? "Error"
        : "Agent";
      meta.textContent = message.status ? `${roleLabel} · ${message.status}` : roleLabel;
      row.appendChild(meta);

      if (message.is_execution_feed) {
        const group = document.createElement("div");
        group.className = "feed-group";
        for (const chain of message.execution_feed_chains ?? []) {
          group.appendChild(renderFeedChain(chain, 0));
        }
        for (const entry of message.execution_feed_entries ?? []) {
          if (entry.show_section_header) {
            const section = document.createElement("div");
            section.className = "feed-section";
            section.textContent = entry.section_label;
            group.appendChild(section);
          }

          const line = document.createElement("div");
          line.className = `feed-line ${entry.level || "info"}`;
          line.innerHTML = `
            <time>${escapeHtml(entry.display_timestamp || "")}</time>
            <div class="feed-text">${escapeHtml(entry.text || "")}</div>
          `;

          if (entry.artifact_path) {
            const pill = document.createElement("div");
            pill.className = "artifact-pill";
            pill.textContent = `${entry.artifact_label || "Artifact"}: ${entry.artifact_path}`;
            line.querySelector(".feed-text").appendChild(pill);
          }
          appendStatePills(line.querySelector(".feed-text"), entry);

          group.appendChild(line);
        }
        row.appendChild(group);
      } else {
        const content = document.createElement("pre");
        content.className = "transcript-content";
        content.textContent = message.text || message.live_answer_text || message.live_thinking_text || "";
        row.appendChild(content);
      }

      for (const artifact of message.artifacts ?? []) {
        const artifactPill = document.createElement("div");
        artifactPill.className = "artifact-pill";
        artifactPill.textContent = `${artifact.label}: ${artifact.path}`;
        row.appendChild(artifactPill);
      }

      transcript.appendChild(row);
    }

    for (const event of appState.localPreviewEvents) {
      const row = document.createElement("article");
      row.className = "transcript-row feed-row preview-event-row";

      const meta = document.createElement("div");
      meta.className = "transcript-meta";
      meta.textContent = "Preview · local";
      row.appendChild(meta);

      const content = document.createElement("pre");
      content.className = "transcript-content";
      content.textContent = `${event.timestamp} · ${event.text}`;
      row.appendChild(content);

      transcript.appendChild(row);
    }

    transcript.scrollTop = shouldFollow ? transcript.scrollHeight : previousScrollTop;
  }

  function renderFeedChain(chain, depth) {
    const wrapper = document.createElement("details");
    wrapper.className = `feed-chain ${chain.level || "info"}`;
    wrapper.open = chain.is_expanded !== false || chain.is_running;
    wrapper.style.marginLeft = `${Math.min(Math.max(depth, 0), 6) * 10}px`;

    const summary = document.createElement("summary");
    summary.className = "feed-chain-summary";
    const title = chain.title || chain.phase || chain.chain_id || "Phase";
    const meta = [chain.step_id, chain.status].filter(Boolean).join(" · ");
    summary.innerHTML = `
      <span>${escapeHtml(title)}</span>
      <small>${escapeHtml(meta)}</small>
    `;
    wrapper.appendChild(summary);

    if (chain.streaming_text) {
      const stream = document.createElement("pre");
      stream.className = "feed-chain-stream";
      stream.textContent = chain.streaming_text;
      wrapper.appendChild(stream);
    }

    for (const entry of chain.entries ?? []) {
      const line = document.createElement("div");
      line.className = `feed-line ${entry.level || "info"}`;
      line.innerHTML = `
        <time>${escapeHtml(entry.display_timestamp || "")}</time>
        <div class="feed-text">${escapeHtml(entry.text || "")}</div>
      `;
      appendStatePills(line.querySelector(".feed-text"), entry);
      wrapper.appendChild(line);
    }

    for (const child of chain.children ?? []) {
      wrapper.appendChild(renderFeedChain(child, depth + 1));
    }

    return wrapper;
  }

  function appendStatePills(container, entry) {
    if (!container || !entry) {
      return;
    }
    const pills = [
      ["Validation", entry.validation_status],
      ["Approval", entry.approval_id],
      ["Tool", entry.tool_name],
      ["Memory", entry.memory_id],
    ].filter(([, value]) => value);
    for (const [label, value] of pills) {
      const pill = document.createElement("div");
      pill.className = "artifact-pill state-pill";
      pill.textContent = `${label}: ${value}`;
      container.appendChild(pill);
    }
  }

  function updateEditorSaveState(elements) {
    const currentFile = appState.snapshot?.current_file;
    const canSave = Boolean(currentFile?.editable);
    elements.saveFileButton.disabled = !canSave;
    elements.saveFileButton.textContent = appState.editorDirty && canSave ? "Save *" : "Save";
  }

  async function refreshState(elements) {
    const response = await fetchJson("/api/lab/state");
    if (response.ok) {
      applySnapshot(elements, response.data, false);
    }
  }

  async function selectWorkspace(elements) {
    const workspaceId = elements.workspaceSelect.value;
    if (!workspaceId) {
      return;
    }
    if (!canDiscardEditorChanges()) {
      renderHeaderControls(elements, true);
      return;
    }

    await postJson("/api/lab/workspaces/select", { workspace_id: workspaceId });
    await refreshState(elements);
  }

  async function refreshWorkspaceTree(elements) {
    const workspace = getSelectedWorkspace();
    if (!workspace) {
      return;
    }

    await ensureWorkspaceTreeLoaded(elements, workspace.id, "");
    renderExplorer(elements);
  }

  async function selectAgent(elements) {
    const agentId = elements.agentSelect.value;
    await postJson("/api/lab/agent/select", { agent_id: agentId });
    await refreshState(elements);
  }

  async function sendChat(elements) {
    const text = elements.chatInput.value.trim();
    if (!text) {
      return;
    }

    const response = await postJson("/api/lab/chat/send", { text });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to send chat message.");
      return;
    }

    elements.chatInput.value = "";
    await refreshState(elements);
  }

  function buildMobileWebLabUrl() {
    const mobileUrl = appState.snapshot?.web_lab?.mobile_url || "";
    if (mobileUrl) {
      return mobileUrl;
    }
    const advertisedUrl = appState.snapshot?.web_lab?.advertised_url || "";
    const webLabRunning = Boolean(appState.snapshot?.web_lab?.is_running);
    let base = `${window.location.protocol}//${window.location.host}`;
    if (advertisedUrl && !advertisedUrl.includes("127.0.0.1") && !advertisedUrl.includes("localhost")) {
      const advertised = new URL(advertisedUrl);
      base = webLabRunning
        ? advertisedUrl
        : `${window.location.protocol}//${advertised.hostname}:${window.location.port || advertised.port}`;
    }
    const url = new URL(base);
    url.searchParams.set("mobile", "1");
    return url.toString();
  }

  function showMobileQr(elements) {
    const url = buildMobileWebLabUrl();
    elements.mobileQrUrl.textContent = url;
    try {
      drawQr(elements.mobileQrCanvas, url);
    } catch (error) {
      elements.mobileQrUrl.textContent = `${url}\n\nQR generation failed: ${error.message}`;
    }
    elements.mobileQrPanel.classList.add("open");
    elements.mobileOverlayScrim.classList.add("open");
  }

  function hideMobileQr(elements) {
    elements.mobileQrPanel.classList.remove("open");
    if (!appState.mobileTreeOpen) {
      elements.mobileOverlayScrim.classList.remove("open");
    }
  }

  function toggleMobileFiles(elements) {
    appState.mobileTreeOpen = !appState.mobileTreeOpen;
    syncMobileFileOverlay(elements);
  }

  function closeMobilePanels(elements) {
    appState.mobileTreeOpen = false;
    elements.mobileQrPanel.classList.remove("open");
    syncMobileFileOverlay(elements);
  }

  function syncMobileFileOverlay(elements) {
    document.body.classList.toggle("mobile-tree-open", appState.mobileTreeOpen);
    const qrOpen = elements.mobileQrPanel?.classList.contains("open");
    elements.mobileOverlayScrim?.classList.toggle("open", Boolean(qrOpen || appState.mobileTreeOpen));
  }

  async function openHumanViewPreview(elements) {
    const selectedWorkspace = getSelectedWorkspace();
    const previewUrl = elements.humanViewPreviewInput.value.trim();
    const response = await postJson("/api/lab/human-view/open", {
      project_path: selectedWorkspace?.path_summary ?? "",
      preview_url: previewUrl,
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to open human-view preview.");
      return;
    }

    const nextUrl = response.data?.preview_url || previewUrl;
    if (nextUrl) {
      elements.humanViewFrame.src = nextUrl;
      appendPreviewEvent(elements, `Opened preview: ${nextUrl}`);
    } else {
      appendPreviewEvent(elements, "Preview registered without URL; start the app and enter its local URL.");
    }
    switchMainTab(elements, "preview");
    await snapshotHumanView(elements);
  }

  async function snapshotHumanView(elements) {
    const response = await fetchJson("/api/lab/human-view/snapshot");
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to capture human-view snapshot.");
      return;
    }

    const snapshotText = JSON.stringify(response.data ?? {}, null, 2);
    elements.humanViewState.textContent = snapshotText;
    appendPreviewEvent(elements, `Snapshot captured:\n${snapshotText.slice(0, 1200)}`);
    const consoleResponse = await fetchJson("/api/lab/human-view/console");
    if (consoleResponse.ok && consoleResponse.data?.entries?.length) {
      appendPreviewEvent(elements, `Console entries: ${consoleResponse.data.entries.length}`);
    }
  }

  async function moveHumanViewCursor(elements, x, y) {
    const safeX = Math.max(0, Math.round(x));
    const safeY = Math.max(0, Math.round(y));
    elements.humanViewCursor.style.left = `${safeX}px`;
    elements.humanViewCursor.style.top = `${safeY}px`;
    await postJson("/api/lab/human-view/cursor", { x: safeX, y: safeY });
  }

  function appendPreviewEvent(elements, message) {
    appState.localPreviewEvents.push({
      timestamp: new Date().toLocaleTimeString(),
      text: message,
    });
    if (appState.localPreviewEvents.length > 40) {
      appState.localPreviewEvents.splice(0, appState.localPreviewEvents.length - 40);
    }
    renderTranscript(elements);
  }

  async function saveCurrentFile(elements) {
    const currentFile = appState.snapshot?.current_file;
    if (!currentFile?.editable) {
      return;
    }

    const response = await postJson("/api/lab/sandbox/file/save", {
      workspace_id: currentFile.workspace_id,
      path: currentFile.path,
      text: elements.fileEditor.value,
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to save file.");
      return;
    }

    appState.editorDirty = false;
    updateEditorSaveState(elements);
    await refreshState(elements);
  }

  async function createSandboxFile(elements) {
    const workspace = getSelectedWorkspace();
    if (!workspace?.is_sandbox) {
      return;
    }

    const relativePath = window.prompt("New sandbox file path", "notes.txt");
    if (!relativePath) {
      return;
    }

    const response = await postJson("/api/lab/sandbox/file/save", {
      workspace_id: workspace.id,
      path: relativePath,
      text: "",
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to create file.");
      return;
    }

    await ensureWorkspaceTreeLoaded(elements, workspace.id, "");
    await postJson("/api/lab/workspaces/node/select", { workspace_id: workspace.id, path: relativePath });
    await refreshState(elements);
  }

  async function createSandboxDirectory(elements) {
    const workspace = getSelectedWorkspace();
    if (!workspace?.is_sandbox) {
      return;
    }

    const relativePath = window.prompt("New sandbox directory path", "folder");
    if (!relativePath) {
      return;
    }

    const response = await postJson("/api/lab/sandbox/directory/create", {
      workspace_id: workspace.id,
      path: relativePath,
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to create directory.");
      return;
    }

    await ensureWorkspaceTreeLoaded(elements, workspace.id, "");
    await postJson("/api/lab/workspaces/node/select", { workspace_id: workspace.id, path: relativePath });
    await refreshState(elements);
  }

  async function renameCurrentPath(elements) {
    const workspace = getSelectedWorkspace();
    const path = appState.snapshot?.lab?.selected_node_path;
    if (!workspace?.is_sandbox || !path) {
      return;
    }

    const nextPath = window.prompt("Rename path", path);
    if (!nextPath || nextPath === path) {
      return;
    }

    const response = await postJson("/api/lab/sandbox/path/rename", {
      workspace_id: workspace.id,
      path,
      new_path: nextPath,
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to rename path.");
      return;
    }

    await refreshState(elements);
  }

  async function deleteCurrentPath(elements) {
    const workspace = getSelectedWorkspace();
    const path = appState.snapshot?.lab?.selected_node_path;
    if (!workspace?.is_sandbox || !path) {
      return;
    }

    if (!window.confirm(`Delete ${path}?`)) {
      return;
    }

    const response = await postJson("/api/lab/sandbox/path/delete", {
      workspace_id: workspace.id,
      path,
    });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to delete path.");
      return;
    }

    await refreshState(elements);
  }

  async function exportSandbox(elements) {
    const workspace = getSelectedWorkspace();
    if (!workspace?.is_sandbox) {
      return;
    }

    const targetDirectory = elements.sandboxExportInput.value.trim();
    if (!targetDirectory) {
      setStatus(elements, "Sandbox export directory required.");
      return;
    }

    const response = await postJson("/api/lab/sandbox/export", { target_directory: targetDirectory });
    if (!response.ok) {
      setStatus(elements, response.error?.message ?? "Failed to export sandbox.");
      return;
    }

    await refreshState(elements);
  }

  async function connectSecurely(elements) {
    const connectWindow = window.open("/connect?pending=1", "_blank");
    if (!connectWindow) {
      setConnectStatus(elements, "Popup was blocked. Allow popups for Nexus Web Lab and try again.", "error");
      return;
    }

    setConnectStatus(elements, "Starting secure connection…", "loading");
    const response = await postJson("/api/connect/start", {});
    if (!response.ok || !response.data?.attempt_id) {
      setConnectStatus(elements, response.error?.message ?? "Failed to start secure connection.", "error");
      connectWindow.location.replace("/connect?error=start_failed");
      return;
    }

    const attemptId = encodeURIComponent(response.data.attempt_id);
    connectWindow.location.replace(`/connect?attempt_id=${attemptId}`);
    setConnectStatus(elements, "Secure connection launched in a new tab.", "success");
  }

  function setStatus(elements, message) {
    if (elements.webLabStatus) {
      elements.webLabStatus.textContent = message;
    }
  }

  function setConnectStatus(elements, message, level) {
    if (!elements.connectStatus) {
      return;
    }

    elements.connectStatus.textContent = message;
    elements.connectStatus.className = `topbar-status ${level || ""}`.trim();
  }

  function getSelectedWorkspace() {
    return (appState.snapshot?.workspaces ?? []).find(workspace => workspace.is_selected);
  }

  function canDiscardEditorChanges() {
    return !appState.editorDirty || window.confirm("Discard unsaved editor changes?");
  }

  async function fetchJson(url, options) {
    try {
      const response = await fetch(url, options);
      return await response.json();
    } catch (error) {
      return {
        ok: false,
        error: {
          message: "Nexus Web Lab could not reach its backend services.",
        },
      };
    }
  }

  function postJson(url, payload) {
    return fetchJson(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload || {}),
    });
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll("\"", "&quot;")
      .replaceAll("'", "&#39;");
  }

  function drawQr(canvas, text) {
    const qr = createQrMatrix(text);
    const context = canvas.getContext("2d");
    const size = qr.length;
    const scale = Math.floor(canvas.width / (size + 8));
    const offset = Math.floor((canvas.width - size * scale) / 2);
    context.fillStyle = "#f6f8f9";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.fillStyle = "#111518";
    for (let y = 0; y < size; y += 1) {
      for (let x = 0; x < size; x += 1) {
        if (qr[y][x]) {
          context.fillRect(offset + x * scale, offset + y * scale, scale, scale);
        }
      }
    }
  }

  function createQrMatrix(text) {
    const version = 4;
    const size = 33;
    const dataCodewords = 80;
    const ecCodewords = 20;
    const bytes = Array.from(new TextEncoder().encode(text));
    if (bytes.length > 78) {
      throw new Error("Mobile URL is too long for the local QR generator.");
    }
    const bits = [0, 1, 0, 0];
    appendBits(bits, bytes.length, 8);
    bytes.forEach(byte => appendBits(bits, byte, 8));
    bits.push(0, 0, 0, 0);
    while (bits.length % 8) bits.push(0);
    const codewords = [];
    for (let i = 0; i < bits.length; i += 8) {
      codewords.push(parseInt(bits.slice(i, i + 8).join(""), 2));
    }
    for (let pad = 0; codewords.length < dataCodewords; pad += 1) {
      codewords.push(pad % 2 === 0 ? 0xec : 0x11);
    }
    const ec = reedSolomon(codewords, ecCodewords);
    const matrix = Array.from({ length: size }, () => Array(size).fill(false));
    const reserved = Array.from({ length: size }, () => Array(size).fill(false));
    addFinder(matrix, reserved, 0, 0);
    addFinder(matrix, reserved, size - 7, 0);
    addFinder(matrix, reserved, 0, size - 7);
    addTiming(matrix, reserved);
    addAlignment(matrix, reserved, 26, 26);
    reserveFormat(reserved);
    placeData(matrix, reserved, codewords.concat(ec), 0);
    addFormatBits(matrix, reserved, 0);
    return matrix;
  }

  function appendBits(bits, value, length) {
    for (let i = length - 1; i >= 0; i -= 1) {
      bits.push((value >>> i) & 1);
    }
  }

  function addFinder(matrix, reserved, x, y) {
    for (let dy = -1; dy <= 7; dy += 1) {
      for (let dx = -1; dx <= 7; dx += 1) {
        const xx = x + dx;
        const yy = y + dy;
        if (!matrix[yy] || matrix[yy][xx] === undefined) continue;
        const inCore = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6;
        const dark = inCore && (dx === 0 || dx === 6 || dy === 0 || dy === 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
        matrix[yy][xx] = dark;
        reserved[yy][xx] = true;
      }
    }
  }

  function addTiming(matrix, reserved) {
    for (let i = 8; i < matrix.length - 8; i += 1) {
      matrix[6][i] = i % 2 === 0;
      matrix[i][6] = i % 2 === 0;
      reserved[6][i] = true;
      reserved[i][6] = true;
    }
  }

  function addAlignment(matrix, reserved, x, y) {
    for (let dy = -2; dy <= 2; dy += 1) {
      for (let dx = -2; dx <= 2; dx += 1) {
        const dark = Math.max(Math.abs(dx), Math.abs(dy)) !== 1;
        matrix[y + dy][x + dx] = dark;
        reserved[y + dy][x + dx] = true;
      }
    }
  }

  function reserveFormat(reserved) {
    const size = reserved.length;
    for (let i = 0; i < 9; i += 1) {
      if (i !== 6) {
        reserved[8][i] = true;
        reserved[i][8] = true;
      }
      reserved[8][size - 1 - i] = true;
      reserved[size - 1 - i][8] = true;
    }
    reserved[size - 8][8] = true;
  }

  function addFormatBits(matrix, reserved, mask) {
    const size = matrix.length;
    const bits = formatBits(1, mask);
    const a = [[8,0],[8,1],[8,2],[8,3],[8,4],[8,5],[8,7],[8,8],[7,8],[5,8],[4,8],[3,8],[2,8],[1,8],[0,8]];
    const b = [[size-1,8],[size-2,8],[size-3,8],[size-4,8],[size-5,8],[size-6,8],[size-7,8],[8,size-8],[8,size-7],[8,size-6],[8,size-5],[8,size-4],[8,size-3],[8,size-2],[8,size-1]];
    for (let i = 0; i < 15; i += 1) {
      matrix[a[i][1]][a[i][0]] = Boolean((bits >>> i) & 1);
      matrix[b[i][1]][b[i][0]] = Boolean((bits >>> i) & 1);
      reserved[a[i][1]][a[i][0]] = true;
      reserved[b[i][1]][b[i][0]] = true;
    }
    matrix[size - 8][8] = true;
    reserved[size - 8][8] = true;
  }

  function formatBits(errorLevel, mask) {
    let data = (errorLevel << 3) | mask;
    let value = data << 10;
    const generator = 0x537;
    for (let i = 14; i >= 10; i -= 1) {
      if ((value >>> i) & 1) {
        value ^= generator << (i - 10);
      }
    }
    return (((data << 10) | value) ^ 0x5412) & 0x7fff;
  }

  function placeData(matrix, reserved, codewords, mask) {
    const bits = [];
    codewords.forEach(codeword => appendBits(bits, codeword, 8));
    let bitIndex = 0;
    let upward = true;
    for (let x = matrix.length - 1; x > 0; x -= 2) {
      if (x === 6) x -= 1;
      for (let i = 0; i < matrix.length; i += 1) {
        const y = upward ? matrix.length - 1 - i : i;
        for (let dx = 0; dx < 2; dx += 1) {
          const xx = x - dx;
          if (reserved[y][xx]) continue;
          let dark = Boolean(bits[bitIndex] || 0);
          if (mask === 0 && (xx + y) % 2 === 0) dark = !dark;
          matrix[y][xx] = dark;
          bitIndex += 1;
        }
      }
      upward = !upward;
    }
  }

  function reedSolomon(data, count) {
    const generator = rsGenerator(count);
    const result = Array(count).fill(0);
    for (const byte of data) {
      const factor = byte ^ result.shift();
      result.push(0);
      for (let i = 0; i < count; i += 1) {
        result[i] ^= gfMul(generator[i], factor);
      }
    }
    return result;
  }

  function rsGenerator(degree) {
    let poly = [1];
    for (let i = 0; i < degree; i += 1) {
      const next = Array(poly.length + 1).fill(0);
      for (let j = 0; j < poly.length; j += 1) {
        next[j] ^= gfMul(poly[j], 1);
        next[j + 1] ^= gfMul(poly[j], gfPow(2, i));
      }
      poly = next;
    }
    return poly.slice(1);
  }

  function gfPow(value, power) {
    let result = 1;
    for (let i = 0; i < power; i += 1) {
      result = gfMul(result, value);
    }
    return result;
  }

  function gfMul(a, b) {
    let result = 0;
    while (b) {
      if (b & 1) result ^= a;
      a <<= 1;
      if (a & 0x100) a ^= 0x11d;
      b >>= 1;
    }
    return result;
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init, { once: true });
  } else {
    init();
  }
})();
