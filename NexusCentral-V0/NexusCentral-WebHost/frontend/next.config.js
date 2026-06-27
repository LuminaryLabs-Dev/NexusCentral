/** @type {import('next').NextConfig} */
const desktopOrigin =
  process.env.NEXUSCENTRAL_INTEGRATION_BASE_URL ||
  process.env.NEXUS_WEB_LAB_API_ORIGIN ||
  "http://127.0.0.1:3005";

const labRoutes = [
  ["/api/lab/bootstrap", "/nexuscentral/get_lab_bootstrap"],
  ["/api/lab/state", "/nexuscentral/get_lab_state"],
  ["/api/lab/workspaces", "/nexuscentral/list_workspaces"],
  ["/api/lab/workspaces/open", "/nexuscentral/open_workspace"],
  ["/api/lab/workspaces/select", "/nexuscentral/select_workspace"],
  ["/api/lab/workspaces/tree", "/nexuscentral/get_workspace_tree"],
  ["/api/lab/workspaces/file", "/nexuscentral/get_workspace_file"],
  ["/api/lab/workspaces/node/select", "/nexuscentral/select_workspace_node"],
  ["/api/lab/chat/send", "/nexuscentral/chat_send"],
  ["/api/lab/agent/select", "/nexuscentral/select_lab_agent"],
  ["/api/lab/model/select", "/nexuscentral/select_lab_model"],
  ["/api/lab/sandbox/file/save", "/nexuscentral/save_sandbox_file"],
  ["/api/lab/sandbox/path/delete", "/nexuscentral/delete_sandbox_path"],
  ["/api/lab/sandbox/path/rename", "/nexuscentral/rename_sandbox_path"],
  ["/api/lab/sandbox/directory/create", "/nexuscentral/create_sandbox_directory"],
  ["/api/lab/sandbox/export", "/nexuscentral/save_sandbox_to_directory"],
  ["/api/lab/suite/projects", "/nexuscentral/get_lab_suite_projects"],
  ["/api/lab/suite/run", "/nexuscentral/run_lab_suite_project"],
  ["/api/lab/human-view/open", "/nexuscentral/open_human_view_preview"],
  ["/api/lab/human-view/action", "/nexuscentral/human_view_action"],
  ["/api/lab/human-view/snapshot", "/nexuscentral/human_view_snapshot"],
  ["/api/lab/human-view/console", "/nexuscentral/human_view_console"],
  ["/api/lab/human-view/cursor", "/nexuscentral/human_view_cursor"],
];

const nextConfig = {
  async rewrites() {
    return [
      ...labRoutes.map(([source, destination]) => ({
        source,
        destination: `${desktopOrigin}${destination}`,
      })),
      {
        source: "/health",
        destination: `${desktopOrigin}/nexuscentral/get_lab_state`,
      },
    ];
  },
};

module.exports = nextConfig;
