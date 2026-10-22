# AerSimulator() creates a local quantum simulator.
#transpile() converts the circuit into instructions the simulator can execute.
#run() executes the circuit.
#get_counts() returns the measurement outcomes.

from qiskit import transpile
from qiskit_aer import AerSimulator

simulator = AerSimulator()

def run_circuit(circuit, shots=1):
    compiled_circuit = transpile(circuit, simulator)

    job = simulator.run(compiled_circuit, shots=shots)

    result = job.result()

    return result.get_counts()
