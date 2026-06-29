import AppLayout from "../components/layout/AppLayout";
import RecommendationCard from "../components/recommendations/RecommendationCard";
import RegenerateRecommendationsButton from "../components/recommendations/RegenerateRecommendationsButton";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useRecommendations } from "../hooks/useRecommendations";

export default function RecommendationsPage() {
  const { userId } = useCurrentUser();
  const { data, loading, error, regenerate, regenerating } = useRecommendations(userId, 10);

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16, flexWrap: "wrap" }}>
        <div>
          <h2 style={{ margin: 0 }}>Recommendations</h2>
          <p style={{ margin: "6px 0 0 0", color: "var(--muted)", maxWidth: 560, lineHeight: 1.5 }}>
            Curated next steps based on your progress. Completed items leave this list automatically; regenerate when you want a fresh batch from the AI service.
          </p>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <p style={{ margin: 0, color: "var(--muted)" }}>
            {loading ? "Loading…" : error ? "—" : regenerating ? "Updating…" : `${data.length} suggestion${data.length === 1 ? "" : "s"}`}
          </p>
          {userId ? (
            <RegenerateRecommendationsButton onClick={() => void regenerate()} disabled={loading || regenerating} />
          ) : null}
        </div>
      </div>

      {loading ? (
        <p style={{ color: "var(--muted)", marginTop: 12 }}>Loading your recommendations…</p>
      ) : error ? (
        <div
          style={{
            marginTop: 16,
            border: "1px solid rgba(239, 68, 68, 0.45)",
            background: "rgba(239, 68, 68, 0.08)",
            borderRadius: "var(--radius-md)",
            padding: 16,
          }}
        >
          <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
          <div style={{ marginTop: 12 }}>
            <RegenerateRecommendationsButton onClick={() => void regenerate()} disabled={regenerating} />
          </div>
        </div>
      ) : data.length === 0 ? (
        <div style={{ marginTop: 16, border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 20 }}>
          <p style={{ margin: 0, color: "var(--muted)", lineHeight: 1.55 }}>
            No open suggestions right now. You may have finished everything in the current batch, or the catalogue has no new matches. Regenerate to ask the assistant for a new set based on your latest progress.
          </p>
          <div style={{ marginTop: 14 }}>
            <RegenerateRecommendationsButton onClick={() => void regenerate()} disabled={regenerating} />
          </div>
        </div>
      ) : (
        <div style={{ marginTop: 14, display: "flex", flexDirection: "column" }}>
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
      )}
    </AppLayout>
  );
}
