def generate_content_based(user, interactions, resources):

    # Build resource lookup by id
    resource_by_id = {r["id"]: r for r in resources}

    completed_topics = set()

    for interaction in interactions:

        if interaction["interactionType"] == "Completed":

            resource_id = interaction["learningResourceId"]

            resource = resource_by_id.get(resource_id)

            if resource:
                completed_topics.add(resource["topic"])

    recommendations = []

    for r in resources:

        if r["topic"] in completed_topics:

            recommendations.append({
                "learningResourceId": r["id"],
                "score": 0.8,
                "algorithmUsed": "ContentBased",
                "explanation": f"Recommended because you studied {r['topic']}"
            })

    return recommendations