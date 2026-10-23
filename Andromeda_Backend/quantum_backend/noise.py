# Cryogenic noise experiment for the "Hold the Qubit" puzzle.
#
# A qubit is prepared in |1>, held (idle) for some time, then measured.
# While idle it can relax back to |0>, modelled with Qiskit Aer's
# thermal_relaxation_error. The relaxation time T1 depends on the core
# temperature: a warmer core means a shorter T1 and a faster decay.
#
# This is a simplified teaching model, not the vessel's real hardware.

from qiskit import QuantumCircuit
from qiskit_aer import AerSimulator
from qiskit_aer.noise import thermal_relaxation_error

# T1 (microseconds) of each core qubit at the reference temperature.
# Q-C sits next to the cryogenic fault, so it relaxes much faster.
T1_AT_REFERENCE_US = {"A": 990.0, "B": 657.0, "C": 58.0}
REFERENCE_TEMPERATURE_MK = 80.0
MIN_TEMPERATURE_MK = 10.0
TEMPERATURE_EXPONENT = 1.6  # how strongly cooling extends T1

simulator = AerSimulator()


def t1_us(qubit, temperature_mk):
    """Relaxation time for a core qubit at the given temperature."""
    scale = (REFERENCE_TEMPERATURE_MK / temperature_mk) ** TEMPERATURE_EXPONENT
    return T1_AT_REFERENCE_US[qubit] * scale


def run_noisy_experiment(request):
    qubit = str(request.get("qubit", "C")).upper()
    temperature = request.get("temperature_mk", REFERENCE_TEMPERATURE_MK)
    hold = request.get("hold_us", 20)
    shots = request.get("shots", 200)
    encoded = bool(request.get("encoded", False))

    if qubit not in T1_AT_REFERENCE_US:
        raise ValueError("qubit must be A, B or C")
    if not isinstance(temperature, (int, float)) or isinstance(temperature, bool):
        raise ValueError("temperature_mk must be a number")
    if not MIN_TEMPERATURE_MK <= temperature <= REFERENCE_TEMPERATURE_MK:
        raise ValueError("temperature_mk must be between 10 and 80")
    if not isinstance(hold, (int, float)) or isinstance(hold, bool) or not 0 <= hold <= 200:
        raise ValueError("hold_us must be between 0 and 200")
    if not isinstance(shots, int) or isinstance(shots, bool) or not 1 <= shots <= 4000:
        raise ValueError("shots must be between 1 and 4000")

    t1 = t1_us(qubit, float(temperature))
    t2 = t1  # pure relaxation: T2 may not exceed 2*T1
    width = 3 if encoded else 1

    circuit = QuantumCircuit(width, width)
    circuit.x(range(width))  # prepare |1> (or |111> for the repetition code)
    if hold > 0:
        idle = thermal_relaxation_error(t1, t2, float(hold))
        for q in range(width):
            circuit.append(idle, [q])  # each qubit relaxes independently while held
    circuit.measure(range(width), range(width))

    counts = simulator.run(circuit, shots=shots).result().get_counts()

    if encoded:
        # Majority vote: the logical value is 1 if at least two of the three bits read 1.
        logical = {"1": 0, "0": 0}
        for bits, count in counts.items():
            logical["1" if bits.count("1") >= 2 else "0"] += count
        counts = logical

    kept = counts.get("1", 0)
    return {
        "success": True,
        "action": "noisy",
        "qubit": qubit,
        "temperature_mk": float(temperature),
        "hold_us": float(hold),
        "encoded": encoded,
        "shots": shots,
        "counts": {"1": kept, "0": counts.get("0", 0)},
        "survival": kept / shots,
        "t1_us": round(t1, 1),
        "message": "Thermal relaxation simulated on Qiskit Aer.",
    }
