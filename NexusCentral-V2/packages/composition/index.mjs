import { createCentralWorkflowsKit } from '../../kits/workflows-kit/index.mjs';
import { createEngine } from "nexusengine";
import { createCentralKit } from "../../kits/central-kit/index.mjs";
import { createCentralNodeKit } from "../../kits/node-kit/index.mjs";
import { createCentralAuditKit } from "../../kits/audit-kit/index.mjs";
import { createCentralPolicyKit } from "../../kits/policy-kit/index.mjs";
import { createCentralWorkspaceKit } from "../../kits/workspace-kit/index.mjs";
import { createCentralArtifactsKit } from "../../kits/artifacts-kit/index.mjs";
import { createCentralProvidersKit } from "../../kits/providers-kit/index.mjs";
import { createCentralModelCatalogKit } from "../../kits/model-catalog-kit/index.mjs";
import { createCentralModelRuntimeKit } from "../../kits/model-runtime-kit/index.mjs";
import { createCentralMeshMembershipKit } from "../../kits/mesh-membership-kit/index.mjs";
import { createCentralRoutingKit } from "../../kits/routing-kit/index.mjs";
import { createCentralHarnessesKit } from "../../kits/harnesses-kit/index.mjs";
import { createCentralJobsKit } from "../../kits/jobs-kit/index.mjs";
import { createCentralInferenceKit } from "../../kits/inference-kit/index.mjs";
import { createCentralWorkspaceTransferKit } from "../../kits/workspace-transfer-kit/index.mjs";
import { createCentralProofKit } from "../../kits/proof-kit/index.mjs";
import { createCentralLabKit } from "../../kits/lab-kit/index.mjs";
import { createCentralLanAccessKit } from "../../kits/lan-access-kit/index.mjs";
import { createCentralExperimentsKit } from "../../kits/experiments-kit/index.mjs";

export function createCentralRuntime(config = {}) {
  const kits = [
    createCentralKit(),
    createCentralNodeKit(config.node),
    createCentralAuditKit(),
    createCentralPolicyKit(),
    createCentralWorkspaceKit(),
    createCentralArtifactsKit(),
    createCentralProvidersKit(),
    createCentralModelCatalogKit(),
    createCentralModelRuntimeKit(),
    createCentralMeshMembershipKit(),
    createCentralRoutingKit(),
    createCentralHarnessesKit(),
    createCentralJobsKit(),
    createCentralInferenceKit(),
    createCentralWorkspaceTransferKit(),
    createCentralProofKit(),
    createCentralLabKit(),
    createCentralExperimentsKit(),
    createCentralLanAccessKit(),
    createCentralWorkflowsKit()
  ];
  const engine = createEngine({ kits });
  engine.n.central.setReadiness("ready", "central-ready");
  engine.n.centralNode.advertiseCapability("mesh.ping", "node-capability-mesh-ping");
  return engine;
}

export const createCentralSimulation = createCentralRuntime;

export function getCentralGraphSnapshot(engine) {
  return {
    paths: engine.n.paths().filter((entry) => entry.path.startsWith("n:central")),
    apis: engine.n.apis().filter((entry) => entry.domainPath?.startsWith("n:central")),
    central: engine.n.central.getSnapshot(),
    node: engine.n.centralNode.getSnapshot(),
    audit: engine.n.centralAudit.getSnapshot(),
    policy: engine.n.centralPolicy.getSnapshot(),
    workspace: engine.n.centralWorkspace.getSnapshot(),
    artifacts: engine.n.centralArtifacts.getSnapshot(),
    providers: engine.n.centralProviders.getSnapshot(),
    modelCatalog: engine.n.centralModelCatalog.getSnapshot(),
    modelRuntime: engine.n.centralModelRuntime.getSnapshot(),
    meshMembership: engine.n.centralMeshMembership.getSnapshot(),
    routing: engine.n.centralRouting.getSnapshot(),
    harnesses: engine.n.centralHarnesses.getSnapshot(),
    jobs: engine.n.centralJobs.getSnapshot(),
    inference: engine.n.centralInference.getSnapshot(),
    proof: engine.n.centralProof.getSnapshot(),
    lab: engine.n.centralLab.getSnapshot(),
    experiments: engine.n.centralExperiments.getSnapshot(),
    lanAccess: engine.n.centralLanAccess.getSnapshot(),
    workflows: engine.n.centralWorkflows.getSnapshot()
  };
}
