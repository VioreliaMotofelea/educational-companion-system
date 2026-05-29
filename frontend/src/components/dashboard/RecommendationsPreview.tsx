import { useRecommendations } from "../../hooks/useRecommendations";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import RecommendationCard from "../recommendations/RecommendationCard";

export default function RecommendationsPreview() {
  const { userId } = useCurrentUser();
  const { data, loading, error, refetch } = useRecommendations(userId);

  if (loading) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Recommended for you</h3>
        <p style={{ margin: "10px 0 0 0", color: "var(--muted)" }}>Loading your personalized picks…</p>
      </section>
    );
  }

  if (error) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Recommended for you</h3>
        <p style={{ margin: "10px 0 0 0", color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
        <button
          type="button"
          onClick={() => refetch()}
          style={{
            marginTop: 12,
            background: "var(--color-ai-600)",
            color: "#fff",
            border: "none",
            borderRadius: "var(--radius-md)",
            padding: "8px 14px",
            cursor: "pointer",
            fontWeight: 700,
          }}
        >
          Try again
        </button>
      </section>
    );
  }

  if (data.length === 0) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Recommended for you</h3>
        <p style={{ margin: "10px 0 0 0", color: "var(--muted)", lineHeight: 1.55 }}>
          Nothing to show yet. When your study history grows, the assistant will suggest the next best resources here.
        </p>
        <button
          type="button"
          onClick={() => refetch()}
          style={{
            marginTop: 12,
            background: "transparent",
            color: "var(--text)",
            border: "1px solid var(--border-strong)",
            borderRadius: "var(--radius-md)",
            padding: "8px 14px",
            cursor: "pointer",
            fontWeight: 600,
          }}
        >
          Refresh suggestions
        </button>
      </section>
    );
  }

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <h3 style={{ marginTop: 0 }}>Recommended for you</h3>

      <div style={{ marginTop: 10, display: "flex", flexDirection: "column" }}>
        {data.map((rec) => (
          <RecommendationCard
            key={rec.recommendationId}
            title={rec.resource.title}
            reason={rec.explanation}
            description={rec.resource.description}
            topic={rec.resource.topic}
            contentType={rec.resource.contentType}
            resourceId={rec.resource.id}
            userId={userId}
            difficulty={rec.resource.difficulty}
            durationMinutes={rec.resource.estimatedDurationMinutes}
            score={rec.score}
          />
        ))}
      </div>
    </section>
  );
}
