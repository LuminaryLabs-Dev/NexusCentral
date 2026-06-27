# NexusCentral-Interface CLI Architecture

The `NexusCentral-Interface` project is a command-line interface (CLI) tool designed as a Python wheel. It serves as an auxiliary testing and operational surface for the NexusCentral application, organized strictly by Domain.

## Domain-Driven Design

All commands are structured hierarchically under a domain. The general syntax is:
```bash
nci <domain> <action> [arguments...]
```

### Current Domains

#### 1. `models`
Deals with operations related to Local LLMs and HuggingFace models.
**Actions:**
- `download`: Downloads a model from a remote repository (like HuggingFace) into the local system directory.

  ```bash
  nci models download --repo <hf_repo_id> --out <local_path>
  ```

#### 2. `ui`
Deals with UI automation and state observation in the running Avalonia app (via HTTP Integration Server on port 3005).
**Actions:**
- `change-tab`: Switches the active tab in the Avalonia app window.
  ```bash
  nci ui change-tab --tab Downloads
  ```
- `screenshot`: Takes a screenshot of the running Avalonia window and saves it locally.
  ```bash
  nci ui screenshot --out /tmp/app_shot.png
  ```

## Naming Conventions
- **Domain Names**: Always **lowercase**, pluralized when representing a collection of entities (e.g., `models`, `agents`, `workflows`).
- **Actions**: Always **lowercase**, explicit verbs denoting the operation (e.g., `download`, `start`, `list`, `stop`).
- **Arguments**: Use explicit UNIX-style flags (`--repo`, `--out`). Avoid positional arguments for required inputs to ensure explicit readability during testing.

## Output Structure
- To ensure `nci` can be consumed by other scripts or automated processes, actions should ideally support a structured output format or simply clear, line-delimited readable logs that pipe well into `grep`.
