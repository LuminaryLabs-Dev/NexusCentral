import argparse
import urllib.request
import urllib.error
import json
import sys

def register_tabs_command(subparsers: argparse._SubParsersAction):
    """Registers the 'change-tab' command on the 'ui' domain parsed."""
    parser = subparsers.add_parser(
        "change-tab", 
        help="Change the active tab in the NexusCentral Avalonia interface"
    )
    parser.add_argument("--tab", required=True, help="Target tab name (e.g., 'Downloads', 'Router', 'Chat (LLM)')")
    parser.set_defaults(func=execute_change_tab)

def execute_change_tab(args: argparse.Namespace):
    """Executes the change-tab REST call against the Avalonia IntegrationServer."""
    target_tab = args.tab
    url = "http://localhost:3005/nexuscentral/change_tab"
    
    payload = json.dumps({"tab": target_tab}).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    
    try:
        req = urllib.request.Request(url, data=payload, headers=headers, method="POST")
        res = urllib.request.urlopen(req)
        response_data = json.loads(res.read().decode("utf-8"))
        
        if response_data.get("ok"):
            print(f"Success: Tab changed to '{target_tab}'")
        else:
            error = response_data.get("error", {})
            print(f"Error [{error.get('code')}]: {error.get('message')}")
            sys.exit(1)
            
    except urllib.error.URLError as e:
        print(f"Connection Error: Could not reach the Avalonia IntegrationServer at {url}. Is the app running?")
        print(f"Details: {e.reason}")
        sys.exit(1)
    except Exception as e:
        print(f"Failed to change tab: {e}")
        sys.exit(1)
