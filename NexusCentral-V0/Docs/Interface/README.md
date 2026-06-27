# Interface (Python CLI) Module

## Purpose

`Interface` exposes a command-line surface (the `nci` executable) for operators to drive NexusCentral tasks from scripts and terminal.

## Project structure

- `setup.py` and `pyproject.toml` for package metadata and installation.
- `src/nexuscentral_interface`: python package:
  - `cli.py`: top-level CLI entrypoint using `argparse`/`click` style (probably with `cloup`).
  - `domains/`: domain-specific command modules.
    - `models/`: model management (download, list, inspect)
    - `ui/`: controls a running Avalonia desktop app via `IntegrationServer` API (change-tab, screenshot)
    - `workflows/`: workflow operations (start, stop, status)
    - `nexuscentral/`: core app-level commands.

## CLI model

From `Interface/docs/CLI_ARCHITECTURE.md`:

- command syntax: `nci <domain> <action> [arguments...]`

### domain `models`
- `download`: downloads a HuggingFace model to local `models/` state.

### domain `ui`
- `change-tab`: switch active tab in Avalonia window via API.
- `screenshot`: capture Avalonia window output.

## Integration points

- Reads and manipulates same config/workflow/wrapper/agent models as Desktop.
- Uses `IntegrationServer` in Desktop at `127.0.0.1:3005` for UI ops.
- Supports automation scripts that can be triggered without opening Avalonia, for CI and tests.

## How to run

```bash
pip install -e Interface
nci models download --repo <hf_repo_id> --out <local_path>
nci ui change-tab --tab Downloads
nci ui screenshot --out /tmp/app_shot.png
```

## Notes

- Keep command output simple and scriptable.
- Observe naming conventions: domains lowercase, actions explicit, flags explicit (no positional required args for core paths).
