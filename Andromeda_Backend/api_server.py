from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from quantum_backend.api import handle_request

app = FastAPI(title="Andromeda Quantum Backend")

# Let the WebGL build (e.g. hosted on itch.io) call this API from the browser.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET", "POST"],
    allow_headers=["Content-Type"],
)


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
