import { access } from "node:fs/promises";
import { resolve } from "node:path";

await access(resolve(import.meta.dirname, "../../web/out/index.html"));
console.log("Electron shell is ready to load the shared Nexus Central Web build.");
