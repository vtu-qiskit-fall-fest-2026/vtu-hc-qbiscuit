
from quantum_backend.diagnosis import diagnose_error
from quantum_backend.verification import verify_correction
from quantum_backend.compiler import simulate_player_circuit


def handle_request(request):
    if not isinstance(request, dict):
        return {
            "success": False,
            "error": "Request must be a JSON object"
        }

    if request.get("version") != 1:
        return {
            "version": 1,
            "success": False,
            "error": "Unsupported API version"
        }

    action = request.get("action")

    # Player-created circuit: compile and run using Qiskit Aer.
    if action == "simulate":
        try:
            result = simulate_player_circuit(request)
            result["version"] = 1
            return result
        except (ValueError, TypeError, KeyError) as exc:
            return {
                "version": 1,
                "success": False,
                "action": "simulate",
                "error": str(exc)
            }

    # Existing error-correction puzzle.
    if action != "correct":
        return {
            "version": 1,
            "success": False,
            "error": "Unknown action"
        }

    error_qubit = request.get("error_qubit", -1)
    correction_qubit = request.get("correction_qubit", -1)

    # Unity uses -1 to represent None.
    if error_qubit == -1:
        error_qubit = None

    if correction_qubit == -1:
        correction_qubit = None

    if error_qubit not in (None, 0, 1, 2):
        return {
            "version": 1,
            "success": False,
            "error": "Invalid error_qubit"
        }

    if correction_qubit not in (None, 0, 1, 2):
        return {
            "version": 1,
            "success": False,
            "error": "Invalid correction_qubit"
        }

    try:
        diagnosis = diagnose_error(error_qubit)

        verification = verify_correction(
            error_qubit,
            correction_qubit
        )

        return {
            "version": 1,
            "success": True,
            "action": "correct",
            "syndrome": diagnosis["syndrome"],
            "detected_error": diagnosis["detected_error"],
            "correction_qubit": correction_qubit,
            "final_data": verification["final_data"],
            "verified": verification["verified"],
            "error": None
        }

    except Exception as exc:
        return {
            "version": 1,
            "success": False,
            "action": "correct",
            "error": str(exc)
        }
