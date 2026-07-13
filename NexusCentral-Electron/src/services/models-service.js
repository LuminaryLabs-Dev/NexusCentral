class ModelsService {
  constructor({ getLmStudioConnection, fetchImpl = fetch }) {
    this.getLmStudioConnection = getLmStudioConnection;
    this.fetchImpl = fetchImpl;
  }

  headers(apiKey = '', json = false) {
    const headers = { Accept: 'application/json' };
    if (json) {
      headers['Content-Type'] = 'application/json';
    }
    if (apiKey) {
      headers.Authorization = `Bearer ${apiKey}`;
    }
    return headers;
  }

  async request(path, options = {}) {
    const connection = this.getLmStudioConnection();
    const baseUrl = String(connection.baseUrl || 'http://127.0.0.1:1234').replace(/\/+$/, '').replace(/\/v1$/, '');
    const response = await this.fetchImpl(`${baseUrl}${path}`, {
      ...options,
      headers: { ...this.headers(connection.apiKey, Boolean(options.body)), ...(options.headers || {}) },
      signal: AbortSignal.timeout(options.timeoutMs || 15000)
    });
    if (!response.ok) {
      throw new Error(`LM Studio returned HTTP ${response.status}.`);
    }
    return response.json();
  }

  async listInstalled() {
    try {
      const body = await this.request('/api/v1/models');
      const source = Array.isArray(body.models) ? body.models : (Array.isArray(body.data) ? body.data : []);
      const models = source.map((item) => ({
        id: String(item.id || item.model || ''),
        name: String(item.display_name || item.name || item.id || item.model || ''),
        type: String(item.type || 'llm'),
        state: String(item.state || item.status || 'downloaded'),
        instanceId: String(item.instance_id || '')
      })).filter((item) => item.id);
      return { ok: true, models, message: `${models.length} local model${models.length === 1 ? '' : 's'}.` };
    } catch (error) {
      return { ok: false, models: [], message: error.message };
    }
  }

  async search(query) {
    const value = String(query || '').trim();
    if (!value) {
      return { ok: false, models: [], message: 'Enter a model name.' };
    }
    try {
      const params = new URLSearchParams({ search: value, filter: 'gguf', sort: 'downloads', direction: '-1', limit: '12' });
      const response = await this.fetchImpl(`https://huggingface.co/api/models?${params}`, {
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(10000)
      });
      if (!response.ok) {
        throw new Error(`Hugging Face returned HTTP ${response.status}.`);
      }
      const body = await response.json();
      const models = (Array.isArray(body) ? body : []).map((item) => ({
        id: String(item.modelId || item.id || ''),
        name: String(item.modelId || item.id || ''),
        downloads: Number(item.downloads || 0),
        likes: Number(item.likes || 0)
      })).filter((item) => item.id);
      return { ok: true, models, message: `${models.length} downloadable model${models.length === 1 ? '' : 's'}.` };
    } catch (error) {
      return { ok: false, models: [], message: error.message };
    }
  }

  async download(modelId) {
    try {
      const body = await this.request('/api/v1/models/download', {
        method: 'POST',
        body: JSON.stringify({ model: `https://huggingface.co/${String(modelId || '').trim()}` }),
        timeoutMs: 30000
      });
      return { ok: true, jobId: body.job_id || '', message: body.job_id ? `Download started: ${body.job_id}` : 'Download started.' };
    } catch (error) {
      return { ok: false, message: error.message };
    }
  }

  async load(modelId, contextLength = 16000) {
    try {
      const body = await this.request('/api/v1/models/load', {
        method: 'POST',
        body: JSON.stringify({ model: modelId, context_length: Number(contextLength) || 16000 }),
        timeoutMs: 5 * 60 * 1000
      });
      return { ok: true, modelId, instanceId: body.instance_id || modelId, message: `${modelId} loaded.` };
    } catch (error) {
      return { ok: false, message: error.message };
    }
  }

  async unload(instanceId) {
    try {
      await this.request('/api/v1/models/unload', {
        method: 'POST',
        body: JSON.stringify({ instance_id: instanceId })
      });
      return { ok: true, message: `${instanceId} unloaded.` };
    } catch (error) {
      return { ok: false, message: error.message };
    }
  }
}

module.exports = { ModelsService };
