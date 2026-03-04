def generate_content_based(user, interactions, resources):

    completed_topics = []

    for interaction in interactions:
        if interaction["interactionType"] == "Completed":
            completed_topics.append(interaction["topic"])

    recommendations = []

    for resource in resources:
        if resource["topic"] in completed_topics:
            recommendations.append({
                "learningResourceId": resource["id"],
                "score": 0.8,
                "algorithmUsed": "ContentBased",
                "explanation": f"Because you studied {resource['topic']}"
            })

    return recommendations[:10]