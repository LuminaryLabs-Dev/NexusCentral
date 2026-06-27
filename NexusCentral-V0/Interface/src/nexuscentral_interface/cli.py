import argparse
import sys
from nexuscentral_interface.domains.models.download import register_download_command
from nexuscentral_interface.domains.nexuscentral import register_nexuscentral_commands
from nexuscentral_interface.domains.ui import register_tabs_command, register_screenshot_command
from nexuscentral_interface.domains.workflows import register_workflow_commands

def main():
    parser = argparse.ArgumentParser(
        prog="nci",
        description="NexusCentral Interface CLI"
    )
    
    subparsers = parser.add_subparsers(dest="domain", required=True, help="Domain commands")
    
    # --- DOMAINS --- #
    # Models Domain
    models_parser = subparsers.add_parser("models", help="Interact with models")
    models_subparsers = models_parser.add_subparsers(dest="action", required=True, help="Models actions")
    register_download_command(models_subparsers)
    
    # UI Domain
    ui_parser = subparsers.add_parser("ui", help="Interact with the Avalonia Desktop UI")
    ui_subparsers = ui_parser.add_subparsers(dest="action", required=True, help="UI actions")
    register_tabs_command(ui_subparsers)
    register_screenshot_command(ui_subparsers)

    nexuscentral_parser = subparsers.add_parser("nexuscentral", help="Call the app's public nexuscentral integration tools")
    nexuscentral_subparsers = nexuscentral_parser.add_subparsers(dest="action", required=True, help="NexusCentral tool actions")
    register_nexuscentral_commands(nexuscentral_subparsers)

    workflows_parser = subparsers.add_parser("workflows", help="Interact with first-class workflow definitions and workflow runs")
    workflows_subparsers = workflows_parser.add_subparsers(dest="action", required=True, help="Workflow actions")
    register_workflow_commands(workflows_subparsers)
    
    # Parse args
    args = parser.parse_args()
    
    # Route command to domain handlers based on parsed arguments
    try:
        if args.domain == "models":
            if args.action == "download":
                args.func(args)
        elif args.domain == "ui":
            if args.action in ["change-tab", "screenshot"]:
                args.func(args)
        elif args.domain == "nexuscentral":
            args.func(args)
        elif args.domain == "workflows":
            args.func(args)
    except Exception as e:
        print(f"Error executing command: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
