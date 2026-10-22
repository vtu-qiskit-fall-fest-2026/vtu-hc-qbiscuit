from fastapi import FastAPI, HTTPException
from quantum_backend.api import handle_request

app = FastAPI(title="Andromeda Quantum Backend")


@app.get("/health")
def health():
    return {
        "status": "ok",
        "backend": "Qiskit Aer"
    }


@app.post("/run")
def run(request: dict):
    result = handle_request(request)

    if not result.get("success"):
        raise HTTPException(
            status_code=400,
            detail=result.get("error", "Request failed")
        )

    return result
