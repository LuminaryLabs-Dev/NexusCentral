import argparse
import urllib.request
import urllib.error
import json
import base64
import sys
import os

def register_screenshot_command(subparsers: argparse._SubParsersAction):
    """Registers the 'screenshot' command on the 'ui' domain parsed."""
    parser = subparsers.add_parser(
        "screenshot", 
        help="Capture a screenshot of the NexusCentral Avalonia interface"
    )
    parser.add_argument("--out", required=True, help="Local file path to save the PNG screenshot")
    parser.set_defaults(func=execute_screenshot)

def execute_screenshot(args: argparse.Namespace):
    """Executes the screenshot REST call and decodes the image."""
    out_path = args.out
    url = "http://localhost:3005/nexuscentral/get_screenshot"
    
    try:
        req = urllib.request.Request(url, method="GET")
        res = urllib.request.urlopen(req)
        response_data = json.loads(res.read().decode("utf-8"))
        
        if response_data.get("ok"):
            img_b64 = response_data["data"]["image"]
            
            # Ensure the output directory exists
            out_dir = os.path.dirname(out_path)
            if out_dir:
                os.makedirs(out_dir, exist_ok=True)
                
            with open(out_path, "wb") as f:
                f.write(base64.b64decode(img_b64))
                
            print(f"Success: Screenshot saved to {out_path}")
        else:
            error = response_data.get("error", {})
            print(f"Error [{error.get('code')}]: {error.get('message')}")
            sys.exit(1)
            
    except urllib.error.URLError as e:
        print(f"Connection Error: Could not reach the Avalonia IntegrationServer at {url}. Is the app running?")
        print(f"Details: {e.reason}")
        sys.exit(1)
    except Exception as e:
        print(f"Failed to capture screenshot: {e}")
        sys.exit(1)
