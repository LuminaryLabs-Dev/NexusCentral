#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-osx-arm64}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-$ROOT_DIR/.publish-verify/$RID}"
LAUNCHER_PROJECT="$ROOT_DIR/Launcher/NexusCentral-Launcher.csproj"

rm -rf "$OUTPUT_DIR"

dotnet publish "$LAUNCHER_PROJECT" -c "$CONFIGURATION" -r "$RID" -o "$OUTPUT_DIR"
dotnet publish "$LAUNCHER_PROJECT" -c "$CONFIGURATION" -r "$RID" -o "$OUTPUT_DIR"

if [[ -d "$OUTPUT_DIR/models" || -d "$OUTPUT_DIR/logs" ]]; then
  echo "Publish output still contains writable state directories." >&2
  exit 1
fi

if find "$OUTPUT_DIR" \( -name '*.gguf' -o -name '*.safetensors' -o -name 'catalog.json' -o -name 'library-state.json' \) -print -quit | grep -q .; then
  echo "Publish output still contains model or state artifacts." >&2
  exit 1
fi

echo "Publish output verified clean: $OUTPUT_DIR"
