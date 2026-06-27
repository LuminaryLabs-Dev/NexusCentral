Rails.application.routes.draw do
  get "/health", to: "health#show"

  namespace :api do
    post "connect/start", to: "connect#start"
    get "connect/status", to: "connect#status"
    match "lab/*lab_path", to: "lab_proxy#forward", via: [:get, :post]
  end
end
