module Api
  class ConnectController < ApplicationController
    def start
      attempt = ConnectAttemptStore.start!(client: NexusCentralClient.new)
      render json: {
        ok: true,
        data: {
          attempt_id: attempt[:token],
          status: attempt[:status],
          status_page_url: "/connect?attempt_id=#{ERB::Util.url_encode(attempt[:token])}"
        }
      }
    rescue StandardError
      render json: {
        ok: false,
        error: {
          code: "CONNECT_START_FAILED",
          message: "Secure connection could not be started."
        }
      }, status: :bad_gateway
    end

    def status
      token = params[:attempt_id].to_s
      attempt = ConnectAttemptStore.fetch(token)
      if attempt.nil?
        render json: {
          ok: false,
          error: {
            code: "ATTEMPT_NOT_FOUND",
            message: "The secure connection attempt could not be found."
          }
        }, status: :not_found
        return
      end

      render json: {
        ok: true,
        data: {
          status: attempt[:status],
          message: attempt[:message],
          safe_details: attempt[:safe_details]
        }
      }
    end
  end
end
