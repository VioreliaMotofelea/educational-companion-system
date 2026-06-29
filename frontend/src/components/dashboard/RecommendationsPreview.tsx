import { useRecommendations } from "../../hooks/useRecommendations";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import RecommendationCard from "../recommendations/RecommendationCard";
import RegenerateRecommendationsButton from "../recommendations/RegenerateRecommendationsButton";

export default function RecommendationsPreview() {
  const { userId } = useCurrentUser();
  const { data, loading, error, regenerate, regenerating } = useRecommendations(userId);

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
        <div style={{ marginTop: 12 }}>
          <RegenerateRecommendationsButton onClick={() => void regenerate()} disabled={regenerating} />
        </div>
      </section>
    );
  }

  if (data.length === 0) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Recommended for you</h3>
        <p style={{ margin: "10px 0 0 0", color: "var(--muted)", lineHeight: 1.55 }}>
          Nothing to show yet. Regenerate when you want a fresh batch based on your latest study history.
        </p>
        <div style={{ marginTop: 12 }}>
          <RegenerateRecommendationsButton onClick={() => void regenerate()} disabled={regenerating} variant="secondary" />
        </div>
      </section>
    );
  }

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <h3 style={{ margin: 0 }}>Recommended for you</h3>
        <RegenerateRecommendationsButton
          onClick={() => void regenerate()}
          disabled={regenerating}
          variant="secondary"
        />
      </div>

      <div style={{ marginTop: 10, display: "flex", flexDirection: "column" }}>
        {data.map((rec) => (
          <RecommendationCard
            key={rec.recommendationId}
            title={rec.resource.title}
            reason={rec.explanation}
            description={rec.resource.description}
            topic={rec.resource.topic}
            contentType={rec.resource.contentType}
            sourceName={rec.resource.sourceName}
            url={rec.resource.url}
            accessType={rec.resource.accessType}
            accessInstructions={rec.resource.accessInstructions}
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
