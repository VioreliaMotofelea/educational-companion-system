import numpy as np
import pandas as pd
from sklearn.metrics.pairwise import cosine_similarity

from recommender.collaborative import generate_collaborative


def test_collaborative_target_row_matches_full_cosine_similarity():
    """One-row cosine_similarity(U, M) equals the corresponding row of cosine_similarity(M)."""
    mat = pd.DataFrame(
        [[1.0, 0.0, 2.0], [0.0, 1.0, 1.0], [2.0, 1.0, 0.0]],
        index=["u1", "u2", "u3"],
        columns=["c", "a", "b"],
    )
    uid = "u2"
    full = cosine_similarity(mat.to_numpy(dtype=np.float64))
    row_full = full[mat.index.get_loc(uid)]
    row_partial = cosine_similarity(
        mat.loc[[uid]].to_numpy(dtype=np.float64),
        mat.to_numpy(dtype=np.float64),
    )[0]
    np.testing.assert_allclose(row_full, row_partial, rtol=1e-9, atol=1e-9)


def test_collaborative_excludes_completed_and_ranks_by_similarity():
    resources = [
        {"id": "r1"},
        {"id": "r2"},
        {"id": "r3"},
    ]
    all_users_interactions = [
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "r2", "interactionType": "Completed"},
        {"userId": "u3", "learningResourceId": "r3", "interactionType": "Completed"},
    ]

    recs = generate_collaborative(
        user_id="u1",
        all_users_interactions=all_users_interactions,
        resources=resources,
        top_k=2,
        neighbors_k=2,
    )

    assert all(r.learningResourceId != "r1" for r in recs)
    assert len(recs) <= 2
    assert recs[0].algorithmUsed == "Collaborative-Cosine-KNN"
    assert recs[0].learningResourceId == "r2"


def test_collaborative_returns_empty_without_user_interactions():
    resources = [{"id": "r1"}, {"id": "r2"}]
    all_users_interactions = [
        {"userId": "u2", "learningResourceId": "r1", "interactionType": "Completed"},
    ]

    recs = generate_collaborative(
        user_id="u1",
        all_users_interactions=all_users_interactions,
        resources=resources,
        top_k=5,
        neighbors_k=2,
    )
    assert recs == []

