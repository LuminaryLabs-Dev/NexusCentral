const fs = require('fs');
const path = require('path');

const PROVIDERS = {
  openrouter: {
    name: 'OpenRouter',
    baseUrl: 'https://openrouter.ai/api/v1',
    apiKeyRequired: true,
    apiKeyPlaceholder: 'OpenRouter API key'
  },
  openai: {
    name: 'OpenAI',
    baseUrl: 'https://api.openai.com/v1',
    apiKeyRequired: true,
    apiKeyPlaceholder: 'OpenAI API key'
  },
  google: {
    name: 'Google Gemini',
    baseUrl: 'https://generativelanguage.googleapis.com/v1beta/openai',
    apiKeyRequired: true,
    apiKeyPlaceholder: 'Gemini API key'
  },
  'nvidia-nim': {
    name: 'NVIDIA NIM',
    baseUrl: 'https://integrate.api.nvidia.com/v1',
    apiKeyRequired: true,
    apiKeyPlaceholder: 'NVIDIA API key'
  },
  'lm-studio': {
    name: 'LM Studio',
    baseUrl: 'http://127.0.0.1:1234/v1',
    apiKeyRequired: false,
    apiKeyPlaceholder: 'Optional LM Studio token'
  },
  custom: {
    name: 'OpenAI-compatible',
    baseUrl: '',
    apiKeyRequired: false,
    apiKeyPlaceholder: 'Optional bearer token'
  }
};

class RouterService {
  constructor({ registryPath, secretStore, fetchImpl = fetch }) {
    this.registryPath = registryPath;
    this.secretStore = secretStore;
    this.fetchImpl = fetchImpl;
  }

  defaultRegistry() {
    return {
      version: 2,
      activeProviderId: 'lm-studio',
      providers: [],
      state: 'inactive',
      message: 'Provider has not been tested.',
      lastTestUtc: null
    };
  }

  load() {
    if (!fs.existsSync(this.registryPath)) {
      return this.defaultRegistry();
    }
    try {
      const saved = JSON.parse(fs.readFileSync(this.registryPath, 'utf8'));
      if (saved.version !== 2) {
        return this.defaultRegistry();
      }
      return {
        ...this.defaultRegistry(),
        ...saved,
        providers: Array.isArray(saved.providers) ? saved.providers.map((item) => this.normalizeProfile(item)) : []
      };
    } catch (error) {
      return { ...this.defaultRegistry(), state: 'unhealthy', message: `Provider registry is unreadable: ${error.message}` };
    }
  }

  save(registry) {
    fs.mkdirSync(path.dirname(this.registryPath), { recursive: true });
    fs.writeFileSync(this.registryPath, JSON.stringify(registry, null, 2), 'utf8');
    return registry;
  }

  providerTemplates() {
    return Object.entries(PROVIDERS).map(([id, value]) => ({ id, ...value }));
  }

  normalizeBaseUrl(value) {
    return String(value || '').trim().replace(/\/+$/, '');
  }

  normalizeProfile(value = {}) {
    const providerType = PROVIDERS[value.providerType] ? value.providerType : 'custom';
    const template = PROVIDERS[providerType];
    return {
      id: String(value.id || providerType),
      providerType,
      name: String(value.name || template.name).trim(),
      baseUrl: this.normalizeBaseUrl(value.baseUrl || template.baseUrl),
      modelId: String(value.modelId || '').trim(),
      hasApiKey: Boolean(value.hasApiKey),
      health: String(value.health || 'inactive'),
      lastProbeUtc: value.lastProbeUtc || null
    };
  }

  publicState(registry) {
    const active = registry.providers.find((item) => item.id === registry.activeProviderId)
      || this.normalizeProfile({ providerType: registry.activeProviderId });
    return {
      state: registry.state,
      message: registry.message,
      lastTestUtc: registry.lastTestUtc,
      activeProviderId: registry.activeProviderId,
      activeProvider: active,
      providers: registry.providers,
      templates: this.providerTemplates(),
      endpointLabel: active.name
    };
  }

  async probeProfile(profile, apiKey = '') {
    if (!profile.baseUrl) {
      throw new Error('Base URL is required.');
    }
    const template = PROVIDERS[profile.providerType];
    if (template.apiKeyRequired && !apiKey) {
      throw new Error(`${template.name} requires an API key.`);
    }

    const headers = { Accept: 'application/json' };
    if (apiKey) {
      headers.Authorization = `Bearer ${apiKey}`;
    }
    const response = await this.fetchImpl(`${profile.baseUrl}/models`, {
      headers,
      signal: AbortSignal.timeout(8000)
    });
    if (!response.ok) {
      throw new Error(`Provider returned HTTP ${response.status}.`);
    }
    const body = await response.json();
    const models = Array.isArray(body.data)
      ? body.data.map((item) => ({ id: String(item.id || ''), name: String(item.name || item.id || '') })).filter((item) => item.id)
      : [];
    return models;
  }

  async saveAndTest(input = {}) {
    const registry = this.load();
    const profile = this.normalizeProfile(input);
    const suppliedKey = String(input.apiKey || '');

    try {
      if (suppliedKey) {
        this.secretStore.set(profile.id, suppliedKey);
        profile.hasApiKey = true;
      } else {
        profile.hasApiKey = this.secretStore.has(profile.id);
      }
      const apiKey = suppliedKey || this.secretStore.get(profile.id);
      const models = await this.probeProfile(profile, apiKey);
      profile.health = 'healthy';
      profile.lastProbeUtc = new Date().toISOString();
      if (!profile.modelId && models.length > 0) {
        profile.modelId = models[0].id;
      }
      registry.state = 'available';
      registry.message = `${profile.name} is ready with ${models.length} model${models.length === 1 ? '' : 's'}.`;
      registry.lastTestUtc = profile.lastProbeUtc;
      registry.models = models;
    } catch (error) {
      profile.health = 'unhealthy';
      profile.lastProbeUtc = new Date().toISOString();
      registry.state = 'unhealthy';
      registry.message = error.message;
      registry.lastTestUtc = profile.lastProbeUtc;
      registry.models = [];
    }

    registry.activeProviderId = profile.id;
    registry.providers = registry.providers.filter((item) => item.id !== profile.id);
    registry.providers.push(profile);
    this.save(registry);
    return { ...this.publicState(registry), models: registry.models || [] };
  }

  currentState() {
    const registry = this.load();
    return { ...this.publicState(registry), models: registry.models || [] };
  }

  async refresh() {
    const registry = this.load();
    const active = registry.providers.find((item) => item.id === registry.activeProviderId);
    if (!active) {
      return this.saveAndTest({ providerType: 'lm-studio', id: 'lm-studio' });
    }
    return this.saveAndTest(active);
  }

  async publishLocalModel({ baseUrl, modelId }) {
    return this.saveAndTest({
      id: 'lm-studio',
      providerType: 'lm-studio',
      name: 'LM Studio',
      baseUrl: this.normalizeBaseUrl(baseUrl).replace(/\/(?:api\/v1|v1)$/, '') + '/v1',
      modelId
    });
  }
}

module.exports = { RouterService, PROVIDERS };
