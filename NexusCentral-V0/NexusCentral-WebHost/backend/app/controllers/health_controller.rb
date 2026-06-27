class HealthController < ApplicationController
  def show
    render json: {
      ok: true,
      service: "nexus-web-lab-backend",
      timestamp: Time.now.utc.iso8601
    }
  end
end
