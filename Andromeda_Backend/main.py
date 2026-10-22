import json
import sys

from quantum_backend.api import handle_request


def main():
    try:
        request = json.load(sys.stdin)
        response = handle_request(request)

    except json.JSONDecodeError:
        response = {
            "success": False,
            "error": "Invalid JSON request"
        }

    except Exception as exc:
        response = {
            "success": False,
            "error": str(exc)
        }

    print(json.dumps(response))


if __name__ == "__main__":
    main()
