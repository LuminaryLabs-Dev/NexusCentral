import Head from "next/head";
import Script from "next/script";

export default function HomePage() {
  return (
    <>
      <Head>
        <title>Nexus Web Lab</title>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </Head>
      <div id="app">
        <header className="topbar">
          <div className="header-controls">
            <label className="compact-control workspace-picker">
              <span>Workspace</span>
              <select id="workspaceSelect"></select>
            </label>
            <label className="compact-control">
              <span>Agent</span>
              <select id="agentSelect"></select>
            </label>
          </div>
          <div className="topbar-status-rail">
            <div className="topbar-status" id="connectStatus">
              Ready to establish a secure NexusCentral browser session.
            </div>
            <div className="topbar-status" id="webLabStatus">
              Connecting…
            </div>
          </div>
          <div className="topbar-actions">
            <button id="mobileFilesButton" type="button" className="rail-icon-button mobile-only" aria-label="Files" data-tooltip="Files">
              ☰
            </button>
            <button id="showMobileQrButton" type="button" className="rail-icon-button" aria-label="Mobile QR" data-tooltip="Mobile QR">
              ▣
            </button>
            <button id="connectSecurelyButton" type="button" className="rail-icon-button" aria-label="Connect securely" data-tooltip="Connect securely">
              ⛨
            </button>
          </div>
        </header>

        <main className="layout">
          <aside className="pane explorer-pane">
            <div className="pane-header explorer-header">
              <div>
                <div className="pane-title">Explorer</div>
                <div className="pane-subtitle" id="workspacePathSummary">
                  Select a workspace to browse files.
                </div>
              </div>
              <div className="pane-actions compact-actions">
                <button id="refreshTreeButton" type="button" className="icon-button" title="Refresh tree">
                  ↻
                </button>
                <button id="newFileButton" type="button" className="icon-button" title="New file">
                  +F
                </button>
                <button id="newFolderButton" type="button" className="icon-button" title="New folder">
                  +D
                </button>
                <button id="renamePathButton" type="button" className="icon-button" title="Rename">
                  Ren
                </button>
                <button id="deletePathButton" type="button" className="icon-button" title="Delete">
                  Del
                </button>
              </div>
            </div>
            <div id="workspaceTree" className="workspace-tree"></div>
            <div className="pane-footer explorer-footer">
              <div className="pane-subtitle">Sandbox export</div>
              <div className="inline-input">
                <input id="sandboxExportInput" type="text" placeholder="/target/directory" />
                <button id="exportSandboxButton" type="button">
                  Export
                </button>
              </div>
            </div>
          </aside>

          <section className="pane editor-pane middle-pane">
            <div className="main-tabs" role="tablist" aria-label="Workspace body">
              <button id="codeTabButton" type="button" className="main-tab active" role="tab" aria-selected="true" aria-controls="codeTabPanel">
                Code
              </button>
              <button id="previewTabButton" type="button" className="main-tab" role="tab" aria-selected="false" aria-controls="previewTabPanel">
                Preview
              </button>
            </div>
            <div id="codeTabPanel" className="main-tab-panel active" role="tabpanel" aria-labelledby="codeTabButton">
              <div className="editor-region">
                <div className="pane-header editor-header">
                  <div>
                    <div className="pane-title" id="editorFileName">No file selected</div>
                    <div className="pane-subtitle" id="editorMeta">
                      Select a file from the explorer.
                    </div>
                  </div>
                  <div className="pane-actions">
                    <button id="saveFileButton" type="button">
                      Save
                    </button>
                  </div>
                </div>
                <textarea id="fileEditor" spellCheck="false" placeholder="Select a file to view it."></textarea>
              </div>
            </div>
            <section id="previewTabPanel" className="main-tab-panel interaction-zone" role="tabpanel" aria-labelledby="previewTabButton" aria-label="Preview">
              <div className="interaction-header">
                <div>
                  <div className="pane-title">Preview</div>
                  <div className="pane-subtitle">Generated app preview with virtual cursor. Activity is logged in Agent Chat.</div>
                </div>
                <div className="interaction-actions">
                  <input id="humanViewPreviewInput" type="text" placeholder="http://127.0.0.1:5173 or local preview URL" />
                  <button id="openHumanViewButton" type="button">Open Preview</button>
                  <button id="snapshotHumanViewButton" type="button">Snapshot</button>
                </div>
              </div>
              <div className="interaction-body">
                <div id="humanViewPreviewShell" className="preview-shell">
                  <iframe id="humanViewFrame" title="Generated app preview" sandbox="allow-scripts allow-same-origin allow-forms"></iframe>
                  <div id="humanViewCursor" className="virtual-cursor" aria-hidden="true"></div>
                </div>
                <pre id="humanViewState" hidden></pre>
              </div>
            </section>
          </section>

          <aside className="pane chat-pane">
            <div className="pane-header chat-header">
              <div>
                <div className="pane-title">Agent Chat</div>
                <div className="pane-subtitle">Conversation and live execution feed for the active workspace.</div>
              </div>
            </div>
            <div className="chat-stream">
              <div id="transcript" className="transcript"></div>
            </div>
            <div className="composer">
              <textarea id="chatInput" placeholder="Ask anything or tell the agent what to do..."></textarea>
              <button id="sendButton" type="button">
                Send
              </button>
            </div>
          </aside>
        </main>
        <div id="mobileOverlayScrim" className="mobile-overlay-scrim"></div>
        <section id="mobileQrPanel" className="mobile-qr-panel" aria-label="Mobile Web Lab QR code">
          <div className="pane-header">
            <div>
              <div className="pane-title">Mobile Web Lab</div>
              <div className="pane-subtitle">Scan on a phone on the same LAN.</div>
            </div>
            <button id="closeMobileQrButton" type="button">Close</button>
          </div>
          <div className="mobile-qr-body">
            <canvas id="mobileQrCanvas" width="232" height="232"></canvas>
            <div id="mobileQrUrl" className="mobile-qr-url"></div>
            <div className="mobile-qr-note">
              LAN mode uses the advertised Web Lab URL from NexusCentral. If this machine is not reachable, enable LAN exposure and launch Web Lab from NexusCentral.
            </div>
          </div>
        </section>
      </div>
      <Script src="/app.js" strategy="afterInteractive" />
    </>
  );
}
