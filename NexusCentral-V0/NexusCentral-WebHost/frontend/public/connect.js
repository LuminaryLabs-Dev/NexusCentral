(function () {
  const init = () => {
    const stateElement = document.getElementById("connectPageState");
    const detailsElement = document.getElementById("connectPageDetails");
    const params = new URLSearchParams(window.location.search);
    const attemptId = params.get("attempt_id");
    const explicitError = params.get("error");

    if (explicitError) {
      renderState("error", "The secure connection could not be started.", "");
      return;
    }

    if (!attemptId) {
      renderState("error", "No connection attempt was provided.", "");
      return;
    }

    const timer = window.setInterval(async () => {
      const response = await fetchJson(`/api/connect/status?attempt_id=${encodeURIComponent(attemptId)}`);
      if (!response.ok) {
        renderState("error", response.error?.message ?? "The secure connection failed.", "");
        window.clearInterval(timer);
        return;
      }

      const status = response.data?.status ?? "pending";
      const message = response.data?.message ?? "Connecting…";
      const safeDetails = response.data?.safe_details ?? {};

      renderState(status === "connected" ? "success" : status === "failed" ? "error" : "loading", message, formatDetails(safeDetails));

      if (status === "connected" || status === "failed") {
        window.clearInterval(timer);
      }
    }, 1000);
  };

  function renderState(level, message, details) {
    const stateElement = document.getElementById("connectPageState");
    const detailsElement = document.getElementById("connectPageDetails");
    if (stateElement) {
      stateElement.className = `connect-state ${level}`;
      stateElement.textContent = message;
    }
    if (detailsElement) {
      detailsElement.textContent = details || "";
    }
  }

  function formatDetails(details) {
    const lines = [];
    if (details.desktop_status) {
      lines.push(`desktop_status=${details.desktop_status}`);
    }
    if (details.lab_url) {
      lines.push(`lab_url=${details.lab_url}`);
    }
    if (details.connected_at) {
      lines.push(`connected_at=${details.connected_at}`);
    }
    if (details.attempt_expires_at) {
      lines.push(`expires_at=${details.attempt_expires_at}`);
    }
    return lines.join("\n");
  }

  async function fetchJson(url) {
    try {
      const response = await fetch(url);
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

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init, { once: true });
  } else {
    init();
  }
})();
