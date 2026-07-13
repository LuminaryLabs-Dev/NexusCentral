# Providers And PeerJS Rooms

## Provider Shape

Provider names are presets, not separate routing systems.

```text
OpenAI-compatible
|-- OpenAI, OpenRouter, NIM, LM Studio, Ollama
|-- llama.cpp, vLLM, LocalAI, Groq, Together
|-- Fireworks, Mistral, Cerebras, DeepInfra, custom

Specialized protocols
|-- Anthropic Messages
|-- Google Generate and Vertex
|-- Azure OpenAI
`-- AWS Bedrock
```

Router stores secret-free profiles. Credentials stay in host secure storage.
Every request still follows policy -> durable job -> route -> adapter -> audit.
The 16,000-token requirement is never silently reduced.

## Sharing Rules

- providers are private by default;
- the owner explicitly enables sharing and sets concurrency/token budgets;
- the receiving host explicitly grants `mesh.inference` to a discovered peer;
- remote peers request one named shared provider and cannot access credentials;
- remote work is re-enqueued by the provider owner and runs under its policy;
- fallback is allowed only before the first visible token.

## Transport Split

```text
same LAN code -> libp2p TCP + mDNS
remote room code -> PeerJS signaling + direct WebRTC data channel
browser pairing code -> Nexus Central Web session
```

PeerJS is transport only. The self-hosted broker coordinates discovery and
WebRTC setup; it does not receive inference payloads after the direct channel is
established. Room codes derive a private network ID and message-authentication
key. Raw codes, TURN credentials, and provider secrets are not persisted.

Start a broker:

```bash
npm run dev:peer-broker
```

Production brokers should use TLS with `NEXUS_PEER_TLS_CERT` and
`NEXUS_PEER_TLS_KEY`. Configure TURN under the folded Network options only when
direct NAT traversal requires it.

## Current Proof Boundary

- mocked OpenAI-compatible, Anthropic, and Google streams pass;
- live LM Studio at `10.0.0.137:1234/v1` passes with `qwen3.5-4b` at 16K;
- two PeerJS nodes stream through a local broker and a different room is hidden;
- 50 nodes pass domain-state churn, route, queue, and policy simulation;
- live TURN, hostile WAN, cloud credentials, and 50 physical peers are release
  gates, not completed proof.
