module Api
  class LabProxyController < ApplicationController
    ROUTE_MAP = {
      "bootstrap" => "/nexuscentral/get_lab_bootstrap",
      "state" => "/nexuscentral/get_lab_state",
      "workspaces" => "/nexuscentral/list_workspaces",
      "workspaces/open" => "/nexuscentral/open_workspace",
      "workspaces/select" => "/nexuscentral/select_workspace",
      "workspaces/tree" => "/nexuscentral/get_workspace_tree",
      "workspaces/file" => "/nexuscentral/get_workspace_file",
      "workspaces/node/select" => "/nexuscentral/select_workspace_node",
      "chat/send" => "/nexuscentral/chat_send",
      "agent/select" => "/nexuscentral/select_lab_agent",
      "model/select" => "/nexuscentral/select_lab_model",
      "sandbox/file/save" => "/nexuscentral/save_sandbox_file",
      "sandbox/path/delete" => "/nexuscentral/delete_sandbox_path",
      "sandbox/path/rename" => "/nexuscentral/rename_sandbox_path",
      "sandbox/directory/create" => "/nexuscentral/create_sandbox_directory",
      "sandbox/export" => "/nexuscentral/save_sandbox_to_directory"
    }.freeze

    def forward
      mapped_route = ROUTE_MAP[params[:lab_path].to_s]
      if mapped_route.nil?
        render json: {
          ok: false,
          error: {
            code: "LAB_ROUTE_NOT_FOUND",
            message: "The requested Nexus Web Lab route is not available."
          }
        }, status: :not_found
        return
      end

      client = NexusCentralClient.new
      payload = request.post? ? parsed_request_body : request.query_parameters
      response = client.request(
        method: request.get? ? :get : :post,
        path: mapped_route,
        query: request.get? ? payload : {},
        payload: request.post? ? payload : nil
      )
      render json: response, status: :ok
    rescue StandardError
      render json: {
        ok: false,
        error: {
          code: "DESKTOP_UNAVAILABLE",
          message: "NexusCentral desktop is unavailable."
        }
      }, status: :bad_gateway
    end

    private

    def parsed_request_body
      request.request_parameters.presence || {}
    end
  end
end
