import AppLayout from "../components/layout/AppLayout";
import RecommendationCard from "../components/recommendations/RecommendationCard";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useRecommendations } from "../hooks/useRecommendations";

export default function RecommendationsPage() {
  const { userId } = useCurrentUser();
  const { data, loading } = useRecommendations(userId, 10);

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16 }}>
        <h2 style={{ margin: 0 }}>Recommendations</h2>
        <p style={{ margin: 0, color: "var(--muted)" }}>{loading ? "Loading..." : `${data.length} items`}</p>
      </div>

      {loading ? (
        <p style={{ color: "var(--muted)", marginTop: 12 }}>Loading recommendations...</p>
      ) : (
        <div style={{ marginTop: 14, display: "flex", flexDirection: "column" }}>
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
      )}
    </AppLayout>
  );
}