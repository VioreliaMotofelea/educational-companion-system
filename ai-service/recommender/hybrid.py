from recommender.content_based import generate_content_based
from recommender.collaborative import generate_collaborative

def generate_hybrid(user, interactions, resources):

    content_recs = generate_content_based(user, interactions, resources)
    collab_recs = generate_collaborative(user, interactions, resources)

    combined = content_recs + collab_recs

    # sort by score
    combined.sort(key=lambda x: x["score"], reverse=True)

    # for rec in content_recs:
    #     rec["algorithmUsed"] = "Hybrid"
    #
    # return content_recs

    return combined[:10]
