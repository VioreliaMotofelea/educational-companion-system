import requests
from config import BACKEND_BASE_URL


def get_user(user_id: str):
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}")
    r.raise_for_status()
    return r.json()


def get_user_interactions(user_id: str):
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}/interactions")
    r.raise_for_status()
    return r.json()


def get_resources():
    r = requests.get(f"{BACKEND_BASE_URL}/api/resources")
    r.raise_for_status()
    return r.json()


def push_recommendations(user_id: str, recommendations):
    body = {
        "recommendations": recommendations,
        "replaceExisting": True
    }

    r = requests.post(
        f"{BACKEND_BASE_URL}/api/users/{user_id}/recommendations",
        json=body
    )

    r.raise_for_status()
    return r.json()