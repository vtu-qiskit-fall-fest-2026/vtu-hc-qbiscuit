from quantum_backend.compiler import simulate_player_circuit


# An empty circuit should measure all qubits as zero.
empty = simulate_player_circuit({
    "action": "simulate",
    "num_qubits": 2,
    "shots": 100,
    "gates": []
})

assert empty["success"]
assert empty["counts"] == [
    {"bitstring": "00", "count": 100}
]


# X on qubit 0 should produce 01 in Qiskit's display order.
x_test = simulate_player_circuit({
    "action": "simulate",
    "num_qubits": 2,
    "shots": 100,
    "gates": [
        {"name": "x", "targets": [0]}
    ]
})

assert x_test["success"]
assert x_test["counts"] == [
    {"bitstring": "01", "count": 100}
]


# H on q0 and CNOT from q0 to q1 should produce
# approximately equal numbers of 00 and 11.
bell = simulate_player_circuit({
    "action": "simulate",
    "num_qubits": 2,
    "shots": 1024,
    "gates": [
        {"name": "h", "targets": [0]},
        {"name": "cx", "controls": [0], "targets": [1]}
    ]
})

assert bell["success"]

observed = {
    item["bitstring"]: item["count"]
    for item in bell["counts"]
}

assert abs(observed.get("00", 0) - 512) < 100
assert abs(observed.get("11", 0) - 512) < 100

print("All player-circuit tests passed.")
