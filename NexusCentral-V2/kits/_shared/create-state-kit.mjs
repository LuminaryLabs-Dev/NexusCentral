import { defineDomainServiceKit, defineResource } from "nexusengine";

function clone(value) {
  return structuredClone(value);
}

export function createStateKit(config) {
  const State = defineResource(`nexuscentral.v2.${config.apiName}.state`);
  const initial = () => ({
    ...clone(config.initialState),
    revision: 0,
    processedRequestIds: []
  });

  return defineDomainServiceKit({
    id: config.id,
    domain: config.domain,
    domainPath: config.domainPath,
    parentDomainPath: "n:central",
    apiName: config.apiName,
    stability: "experimental",
    version: "0.1.0",
    services: [...Object.keys(config.transitions), "snapshot", "reset"],
    requires: config.requires,
    resources: { State },
    inputs: [...Object.keys(config.transitions).map((name) => `${config.domain}.${name}`), `${config.domain}.reset`],
    outputs: config.outputs,
    metadata: {
      name: config.name,
      summary: config.summary,
      resetPolicy: config.resetPolicy,
      snapshotPolicy: "serializable domain state without host resources or secrets"
    },
    initWorld({ world }) {
      world.setResource(State, initial());
    },
    createApi({ world, engine }) {
      const apply = (name, payload = {}) => {
        const state = world.getResource(State);
        const requestId = String(payload.requestId || payload.idempotencyKey || "");
        if (requestId && state.processedRequestIds.includes(requestId)) return clone(state);
        const outcome = config.transitions[name](clone(state), clone(payload), { engine }) || {};
        const next = {
          ...(outcome.state || state),
          revision: state.revision + 1,
          processedRequestIds: requestId
            ? [...state.processedRequestIds, requestId]
            : state.processedRequestIds
        };
        world.setResource(State, next);
        return clone(outcome.result ?? next);
      };
      const methods = Object.fromEntries(
        Object.keys(config.transitions).map((name) => [name, (payload) => apply(name, payload)])
      );
      return Object.freeze({
        ...methods,
        getSnapshot() {
          return clone(world.getResource(State));
        },
        loadSnapshot(snapshot) {
          const next = { ...initial(), ...clone(snapshot) };
          world.setResource(State, next);
          return clone(next);
        },
        reset() {
          const current = world.getResource(State);
          const next = config.reset ? config.reset(clone(current), initial()) : initial();
          world.setResource(State, next);
          return clone(next);
        }
      });
    }
  });
}
