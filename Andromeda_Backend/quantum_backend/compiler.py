from qiskit import QuantumCircuit
from qiskit_aer import AerSimulator
from qiskit import transpile


SINGLE_QUBIT_GATES = {"h", "x", "y", "z", "rx", "ry", "rz"}


def compile_circuit(request):
    num_qubits = request.get("num_qubits", 3)
    gates = request.get("gates", [])

    if not isinstance(num_qubits, int) or isinstance(num_qubits, bool):
        raise ValueError("num_qubits must be an integer")

    if num_qubits < 1 or num_qubits > 5:
        raise ValueError("Use between 1 and 5 qubits")

    if not isinstance(gates, list) or len(gates) > 100:
        raise ValueError("gates must be a list of at most 100 gates")

    circuit = QuantumCircuit(num_qubits)

    for gate in gates:
        name = gate.get("name", "").lower()
        targets = gate.get("targets", [])
        controls = gate.get("controls", [])
        angle = gate.get("angle", 0.0)

        if not isinstance(targets, list) or not targets:
            raise ValueError("Every gate needs a target qubit")

        target = targets[0]

        if not isinstance(target, int) or isinstance(target, bool):
            raise ValueError("Target qubit must be an integer")

        if target < 0 or target >= num_qubits:
            raise ValueError("Target qubit is out of range")

        if name in SINGLE_QUBIT_GATES:
            if controls:
                raise ValueError(name + " does not use control qubits")

            if name == "h":
                circuit.h(target)
            elif name == "x":
                circuit.x(target)
            elif name == "y":
                circuit.y(target)
            elif name == "z":
                circuit.z(target)
            elif name == "rx":
                circuit.rx(float(angle), target)
            elif name == "ry":
                circuit.ry(float(angle), target)
            elif name == "rz":
                circuit.rz(float(angle), target)

        elif name == "cx":
            if len(controls) != 1:
                raise ValueError("CNOT needs exactly one control")

            control = controls[0]

            if not isinstance(control, int) or isinstance(control, bool):
                raise ValueError("Control qubit must be an integer")

            if control < 0 or control >= num_qubits:
                raise ValueError("Control qubit is out of range")

            if control == target:
                raise ValueError("Control and target must differ")

            circuit.cx(control, target)

        else:
            raise ValueError("Unsupported gate: " + name)

    circuit.measure_all()
    return circuit


def simulate_player_circuit(request):
    shots = request.get("shots", 1024)

    if not isinstance(shots, int) or isinstance(shots, bool):
        raise ValueError("shots must be an integer")

    if shots < 1 or shots > 10000:
        raise ValueError("shots must be between 1 and 10000")

    circuit = compile_circuit(request)

    simulator = AerSimulator()
    compiled = transpile(circuit, simulator)
    result = simulator.run(compiled, shots=shots).result()
    counts = result.get_counts()

    return {
        "success": True,
        "action": "simulate",
        "shots": shots,
        "counts": [
            {"bitstring": bits, "count": count}
            for bits, count in counts.items()
        ],
        "message": "Circuit simulated successfully."
    }
