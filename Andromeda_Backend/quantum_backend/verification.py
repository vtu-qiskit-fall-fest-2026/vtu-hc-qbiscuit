from qiskit import QuantumCircuit
from quantum_backend.circuit import run_circuit


def verify_correction(error_qubit, correction_qubit):
    if error_qubit not in (None, 0, 1, 2):
        raise ValueError("Invalid error_qubit")

    if correction_qubit not in (None, 0, 1, 2):
        raise ValueError("Invalid correction_qubit")

    circuit = QuantumCircuit(3, 3)

    # Inject the same error.
    if error_qubit is not None:
        circuit.x(error_qubit)

    # Apply the player's chosen correction.
    if correction_qubit is not None:
        circuit.x(correction_qubit)

    # Measure the three data qubits.
    circuit.measure([0, 1, 2], [0, 1, 2])

    counts = run_circuit(circuit)
    measured = next(iter(counts))

    # Convert Qiskit's display order into q0, q1, q2.
    final_data = measured[::-1]

    return {
        "final_data": final_data,
        "verified": final_data == "000"
    }
