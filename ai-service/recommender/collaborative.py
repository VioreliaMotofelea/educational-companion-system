def generate_collaborative(user, interactions, resources):

    # Placeholder logic
    # In real systems you compare with other users

    recommendations = []

    for r in resources[:5]:

        recommendations.append({
            "learningResourceId": r["id"],
            "score": 0.6,
            "algorithmUsed": "Collaborative",
            "explanation": "Users with similar interests completed this resource"
        })

    return recommendations