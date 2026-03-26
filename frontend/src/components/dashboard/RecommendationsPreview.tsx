import { useRecommendations } from "../../hooks/useRecommendations";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import RecommendationCard from "../recommendations/RecommendationCard";

export default function RecommendationsPreview() {
  const { userId } = useCurrentUser();
  const { data, loading } = useRecommendations(userId);

  if (loading) return <p>Loading...</p>;

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <h3 style={{ marginTop: 0 }}>Recommended for you</h3>

      <div style={{ marginTop: 10, display: "flex", flexDirection: "column" }}>
        {data.map((rec) => (
          <RecommendationCard
            key={rec.recommendationId}
            title={rec.resource.title}
            reason={rec.explanation}
            resourceId={rec.resource.id}
            userId={userId}
          />
        ))}
      </div>
    </section>
  );
}