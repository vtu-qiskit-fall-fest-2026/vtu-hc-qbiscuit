from qiskit import QuantumCircuit
from quantum_backend.circuit import run_circuit


def diagnose_error(error_qubit):
    if error_qubit not in (None, 0, 1, 2):
        raise ValueError("error_qubit must be 0, 1, 2, or None")

    # Three data qubits and two ancillas.
    # Two classical bits store the syndrome.
    circuit = QuantumCircuit(5, 2)

    # Inject a simulated bit-flip error.
    if error_qubit is not None:
        circuit.x(error_qubit)

    # First parity check: q0 XOR q1.
    circuit.cx(0, 3)
    circuit.cx(1, 3)

    # Second parity check: q1 XOR q2.
    circuit.cx(1, 4)
    circuit.cx(2, 4)

    # Measure only the ancillas.
    circuit.measure(3, 0)
    circuit.measure(4, 1)

    counts = run_circuit(circuit)

    # Qiskit displays classical bit 1 before bit 0.
    # Reverse the two-bit result to get (s0, s1).
    measured = next(iter(counts))
    syndrome = measured[::-1]

    syndrome_to_error = {
        "00": None,
        "10": 0,
        "11": 1,
        "01": 2
    }

    return {
        "syndrome": syndrome,
        "detected_error": syndrome_to_error[syndrome]
    }
