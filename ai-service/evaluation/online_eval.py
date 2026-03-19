import json
from .evaluator import Evaluator


def load_logs(path):
    with open(path, "r") as f:
        return json.load(f)


def online_evaluation(log_file_path, k=5):
    logs = load_logs(log_file_path)

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(logs)

    print("\n--- ONLINE EVALUATION ---")
    for key, value in results.items():
        print(f"{key}: {value:.4f}")

    return results