import { createRunContext } from "@nexuscentral/contracts";

const SUPPORTED_TOOLS = new Set(["workspace.read", "workspace.write"]);
const SUPPORTED_SANDBOXES = new Set(["read-only", "workspace-write"]);

export class HarnessExecutorRegistry {
  constructor({ processHarness }) {
    this.executors = new Map([
      ["direct-inference", new DirectInferenceExecutor()],
      ["codex-cli", new CodexCliExecutor(processHarness)]
    ]);
  }

  assertProfile(profile, configuration = {}) {
    if (!this.executors.has(profile?.executor)) throw new Error(`Unsupported harness executor: ${profile?.executor || "missing"}.`);
    if (!profile.stages?.length) throw new Error("Harness profile requires at least one stage.");
    if ((profile.tools || []).some((tool) => !SUPPORTED_TOOLS.has(tool))) throw new Error("Harness profile requests an unsupported tool capability.");
    const sandbox = String(configuration.sandbox || profile.defaultSandbox || "read-only");
    if (!SUPPORTED_SANDBOXES.has(sandbox) || !profile.allowedSandboxes?.includes(sandbox)) throw new Error("Harness sandbox is not allowed by its profile.");
    if (profile.stages.includes("act") && profile.tools.includes("workspace.write") && sandbox !== "workspace-write") throw new Error("A write-capable Act stage requires the workspace-write sandbox.");
    if (!profile.tools.includes("workspace.write") && sandbox === "workspace-write") throw new Error("Read-only harnesses cannot request workspace-write.");
    return { sandbox, allowedTools: [...profile.tools] };
  }

  context(value) { return createRunContext(value); }

  runStage(profile, stage, configuration, prompt, context, options = {}) {
    if (!profile.stages.includes(stage)) throw new Error(`Harness cannot run stage ${stage}.`);
    this.assertProfile(profile, configuration);
    return this.executors.get(profile.executor).runStage(profile, stage, configuration, prompt, context, options);
  }
}

class DirectInferenceExecutor {
  async runStage(_profile, stage, configuration, prompt, context, options) {
    if (stage !== "respond" || typeof context.infer !== "function") throw new Error("Direct inference requires a routed inference context.");
    const messages = options.messages?.length ? options.messages : [{ role: "user", content: prompt }];
    const result = await context.infer({ messages, modelId: configuration.model || "", maxOutputTokens: options.maxOutputTokens, contextLength: options.contextLength });
    return { stage, output: result.output, route: result.route, exitCode: 0, stdout: result.output, stderr: "", args: [] };
  }
}

class CodexCliExecutor {
  constructor(processHarness) { this.processHarness = processHarness; }
  runStage(profile, stage, configuration, prompt, context, options) {
    return this.processHarness.runCodexStage(profile, stage, configuration, prompt, {
      workspaceRoot: context.sandbox.workspaceRoot, signal: context.signal, gateway: context.provider.gateway,
      timeoutMs: options.timeoutMs, stdoutPath: options.stdoutPath, stderrPath: options.stderrPath, onOutput: context.onOutput
    });
  }
}
