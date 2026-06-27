require_relative "boot"

require "rails"
require "active_model/railtie"
require "active_job/railtie"
require "action_controller/railtie"
require "action_view/railtie"

Bundler.require(*Rails.groups)

module NexusCentralWebLabBackend
  class Application < Rails::Application
    config.load_defaults 6.1
    config.api_only = true
    config.hosts.clear
    config.secret_key_base = ENV.fetch("NEXUS_WEB_LAB_SESSION_SECRET", "change-me")
  end
end
