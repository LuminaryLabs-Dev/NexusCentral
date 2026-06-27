require "openssl"
require "securerandom"
require "time"

class ConnectAttemptStore
  @attempts = {}
  @mutex = Mutex.new

  class << self
    def start!(client:)
      id = SecureRandom.uuid
      token = issue_token(id)
      expires_at = Time.now.utc + attempt_ttl_seconds
      attempt = {
        id: id,
        token: token,
        status: "pending",
        message: "Establishing secure connection to NexusCentral…",
        safe_details: {
          attempt_expires_at: expires_at.iso8601
        },
        expires_at: expires_at
      }

      @mutex.synchronize { @attempts[id] = attempt }

      Thread.new do
        begin
          status = client.desktop_status
          bootstrap = client.lab_bootstrap
          update_attempt(id,
            status: "connected",
            message: "Secure connection established.",
            safe_details: {
              desktop_status: status.dig("data", "status_text") || "online",
              lab_url: bootstrap.dig("data", "web_lab", "advertised_url") || bootstrap.dig("data", "web_lab", "local_url"),
              connected_at: Time.now.utc.iso8601,
              attempt_expires_at: expires_at.iso8601
            })
        rescue StandardError
          update_attempt(id,
            status: "failed",
            message: "NexusCentral desktop could not be reached.",
            safe_details: {
              attempt_expires_at: expires_at.iso8601
            })
        end
      end

      attempt
    end

    def fetch(token)
      id, signature = token.to_s.split(".", 2)
      return nil if id.blank? || signature.blank?
      return nil unless secure_compare(signature, sign(id))

      attempt = @mutex.synchronize { @attempts[id] }
      return nil if attempt.nil?
      return nil if attempt[:expires_at] < Time.now.utc

      attempt
    end

    private

    def update_attempt(id, status:, message:, safe_details:)
      @mutex.synchronize do
        attempt = @attempts[id]
        return if attempt.nil?

        attempt[:status] = status
        attempt[:message] = message
        attempt[:safe_details] = safe_details
      end
    end

    def issue_token(id)
      "#{id}.#{sign(id)}"
    end

    def sign(id)
      secret = ENV.fetch("NEXUS_WEB_LAB_SESSION_SECRET", "change-me")
      OpenSSL::HMAC.hexdigest("SHA256", secret, id)
    end

    def secure_compare(a, b)
      return false unless a.bytesize == b.bytesize

      ActiveSupport::SecurityUtils.secure_compare(a, b)
    end

    def attempt_ttl_seconds
      ENV.fetch("NEXUS_WEB_LAB_ATTEMPT_TTL_SECONDS", "300").to_i
    end
  end
end
