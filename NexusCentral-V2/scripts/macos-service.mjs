#!/usr/bin/env node
import { resolve } from "node:path";
import { MacOsLaunchAgentAdapter } from "@nexuscentral/runtime-adapters";

const action = process.argv[2] || "status";
const service = new MacOsLaunchAgentAdapter({ projectRoot: resolve(import.meta.dirname, "..") });
if (!new Set(["install", "status", "uninstall"]).has(action)) throw new Error("Usage: macos-service.mjs install|status|uninstall");
const result = action === "install" ? service.install() : action === "uninstall" ? service.uninstall() : service.status();
console.log(JSON.stringify(result, null, 2));
