import AppLayout from "../components/layout/AppLayout";
import RecommendationCard from "../components/recommendations/RecommendationCard";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useRecommendations } from "../hooks/useRecommendations";

export default function RecommendationsPage() {
  const { userId } = useCurrentUser();
  const { data, loading, error, refetch } = useRecommendations(userId, 10);

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16, flexWrap: "wrap" }}>
        <div>
          <h2 style={{ margin: 0 }}>Recommendations</h2>
          <p style={{ margin: "6px 0 0 0", color: "var(--muted)", maxWidth: 560, lineHeight: 1.5 }}>
            Curated next steps based on your progress. Open any card to start studying, mark completion, and leave feedback so future picks stay relevant.
          </p>
        </div>
        <p style={{ margin: 0, color: "var(--muted)" }}>
          {loading ? "Loading…" : error ? "—" : `${data.length} suggestion${data.length === 1 ? "" : "s"}`}
        </p>
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
        </div>
      ) : data.length === 0 ? (
        <div style={{ marginTop: 16, border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 20 }}>
          <p style={{ margin: 0, color: "var(--muted)", lineHeight: 1.55 }}>
            There are no suggestions yet. Keep learning, or refresh — the assistant may need a moment to build your list.
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            style={{
              marginTop: 14,
              background: "var(--color-ai-600)",
              color: "#fff",
              border: "none",
              borderRadius: "var(--radius-md)",
              padding: "8px 14px",
              cursor: "pointer",
              fontWeight: 700,
            }}
          >
            Refresh suggestions
          </button>
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
