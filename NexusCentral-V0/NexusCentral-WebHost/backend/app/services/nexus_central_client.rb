require "json"
require "net/http"
require "uri"

class NexusCentralClient
  def initialize(base_url: ENV.fetch("NEXUSCENTRAL_INTEGRATION_BASE_URL", "http://127.0.0.1:3005"))
    @base_url = base_url.chomp("/")
  end

  def request(method:, path:, query: {}, payload: nil)
    uri = URI.parse("#{@base_url}#{path}")
    if query.present?
      uri.query = URI.encode_www_form(query)
    end

    request = build_request(method, uri, payload)
    Net::HTTP.start(uri.hostname, uri.port, use_ssl: uri.scheme == "https") do |http|
      response = http.request(request)
      JSON.parse(response.body)
    end
  end

  def desktop_status
    request(method: :get, path: "/nexuscentral/status")
  end

  def lab_bootstrap
    request(method: :get, path: "/nexuscentral/get_lab_bootstrap")
  end

  private

  def build_request(method, uri, payload)
    request = case method.to_sym
              when :post
                Net::HTTP::Post.new(uri)
              else
                Net::HTTP::Get.new(uri)
              end
    if payload.present?
      request["Content-Type"] = "application/json"
      request.body = JSON.generate(payload)
    end
    request
  end
end
