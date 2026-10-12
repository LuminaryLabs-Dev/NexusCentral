#!/usr/bin/env node
import { chmodSync, copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { tmpdir } from "node:os";

const sourceUrl = "https://github.com/ggml-org/llama.cpp.git";
const sourceCommit = "f2cc7282ce0042641df55e0e563b75e824e2b647";
const root = resolve(import.meta.dirname, "..");
const defaultOutput = resolve(root, ".llama-runtime");
const args = process.argv.slice(2);
const sourceArg = args.indexOf("--source");
const outputArg = args.indexOf("--output");
if (sourceArg >= 0 && !args[sourceArg + 1]) throw new Error("--source requires an existing llama.cpp checkout path.");
if (outputArg >= 0 && !args[outputArg + 1]) throw new Error("--output requires a directory path.");
const sourcePath = sourceArg >= 0 ? resolve(args[sourceArg + 1]) : "";
const outputRoot = outputArg >= 0 ? resolve(args[outputArg + 1]) : defaultOutput;

if (process.platform !== "linux" || process.arch !== "x64") throw new Error("The pinned llama.cpp setup currently supports Linux x64 only.");

const temporary = resolve(tmpdir(), `nexuscentral-llama-${process.pid}`);
const checkout = sourcePath || join(temporary, "source");
const build = join(temporary, "build");
const stage = join(temporary, "stage");
let success = false;
try {
  if (!sourcePath) {
    mkdirSync(temporary, { recursive: true });
    run("git", ["clone", "--filter=blob:none", "--no-checkout", sourceUrl, checkout]);
    run("git", ["-C", checkout, "fetch", "--depth=1", "origin", sourceCommit]);
    run("git", ["-C", checkout, "checkout", "--detach", sourceCommit]);
  }
  if (!existsSync(join(checkout, "CMakeLists.txt"))) throw new Error("The supplied path is not a llama.cpp source checkout.");
  const head = run("git", ["-C", checkout, "rev-parse", "HEAD"], true).trim();
  if (head !== sourceCommit) throw new Error(`llama.cpp checkout must be pinned at ${sourceCommit}; found ${head}.`);
  const dirty = run("git", ["-C", checkout, "status", "--porcelain", "--untracked-files=all"], true).trim();
  if (dirty) throw new Error("llama.cpp source checkout has modified or untracked files; use a clean checkout at the pinned commit.");
  mkdirSync(stage, { recursive: true });
  run("cmake", ["-S", checkout, "-B", build, "-DCMAKE_BUILD_TYPE=Release", "-DGGML_NATIVE=OFF", "-DGGML_CUDA=OFF", "-DGGML_METAL=OFF", "-DLLAMA_BUILD_SERVER=ON", "-DLLAMA_BUILD_TESTS=OFF", "-DLLAMA_BUILD_EXAMPLES=OFF", "-DLLAMA_BUILD_UI=OFF", "-DLLAMA_USE_PREBUILT_UI=OFF"]);
  run("cmake", ["--build", build, "--config", "Release", "--target", "llama-server", "-j", "2"]);
  copyRuntime(join(build, "bin"), stage);
  const license = join(checkout, "LICENSE");
  const licenseText = existsSync(license) ? readFileSync(license, "utf8") : "License file unavailable from the pinned source checkout.";
  writeFileSync(join(stage, "THIRD_PARTY_NOTICES.txt"), `llama.cpp\nSource: ${sourceUrl}\nCommit: ${sourceCommit}\n\n${licenseText}`);
  mkdirSync(outputRoot, { recursive: true });
  const target = join(outputRoot, "linux-x64");
  const stagedTarget = join(outputRoot, `.linux-x64-${process.pid}-staging`);
  rmSync(stagedTarget, { recursive: true, force: true });
  copyDirectory(stage, stagedTarget);
  rmSync(target, { recursive: true, force: true });
  const { renameSync } = await import("node:fs");
  renameSync(stagedTarget, target);
  success = true;
  console.log(JSON.stringify({ status: "ready", platform: "linux-x64", sourceCommit, executable: join(target, "bin", "llama-server"), notices: join(target, "THIRD_PARTY_NOTICES.txt") }, null, 2));
}
finally {
  if (!sourcePath) rmSync(temporary, { recursive: true, force: true });
  else rmSync(temporary, { recursive: true, force: true });
  if (!success) rmSync(join(outputRoot, `.linux-x64-${process.pid}-staging`), { recursive: true, force: true });
}

function copyRuntime(binaryDirectory, destination) {
  const server = join(binaryDirectory, "llama-server");
  if (!existsSync(server)) throw new Error("llama-server was not produced by the pinned source build.");
  const bin = join(destination, "bin");
  mkdirSync(bin, { recursive: true });
  for (const name of readdirSync(binaryDirectory)) {
    if (name !== "llama-server" && !/^lib(?:llama|ggml|mtmd)[^/]*\.so(?:\..*)?$/.test(name)) continue;
    const from = join(binaryDirectory, name);
    if (!statSync(from).isFile()) continue;
    copyFileSync(from, join(bin, name));
    if (name === "llama-server") chmodSync(join(bin, name), 0o755);
  }
}

function copyDirectory(from, to) {
  mkdirSync(to, { recursive: true });
  for (const name of readdirSync(from)) {
    const source = join(from, name);
    const target = join(to, name);
    if (statSync(source).isDirectory()) copyDirectory(source, target);
    else copyFileSync(source, target);
  }
}

function run(command, commandArgs, capture = false) {
  const result = spawnSync(command, commandArgs, { cwd: dirname(root), encoding: "utf8", stdio: capture ? ["ignore", "pipe", "pipe"] : "inherit" });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} ${commandArgs.join(" ")} failed${result.stderr ? `: ${result.stderr.trim()}` : ` (exit ${result.status})`}.`);
  return capture ? result.stdout : "";
}
