from .evaluator import Evaluator
from .tracking import load_logs


def online_evaluation(log_file_path, k=5):
    logs = load_logs(log_file_path)

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(logs)

    print("\n--- ONLINE EVALUATION ---")
    for key, value in results.items():
        print(f"{key}: {value:.4f}")

    return results