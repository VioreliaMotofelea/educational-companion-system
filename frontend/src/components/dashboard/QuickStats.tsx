import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useAnalytics } from "../../hooks/useAnalytics";

export default function QuickStats() {
  const { userId } = useCurrentUser();
  const analytics = useAnalytics(userId);

  if (!analytics) {
    return (
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <p style={{ margin: 0, color: "var(--muted)" }}>Loading quick stats...</p>
      </div>
    );
  }

  const kpis = analytics.kpis;
  const avgRating = kpis.averageRatingGiven ?? 0;
  const ratingText = kpis.averageRatingGiven == null ? "N/A" : avgRating.toFixed(1);

  return (
    <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h4 style={{ marginTop: 0 }}>Resources viewed</h4>
        <div style={{ fontSize: 24, fontWeight: 800 }}>{kpis.totalResourcesViewed}</div>
      </div>
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h4 style={{ marginTop: 0 }}>Resources completed</h4>
        <div style={{ fontSize: 24, fontWeight: 800, color: "var(--color-progress-600)" }}>{kpis.totalResourcesCompleted}</div>
      </div>
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h4 style={{ marginTop: 0 }}>Avg rating</h4>
        <div style={{ fontSize: 24, fontWeight: 800 }}>{ratingText}</div>
      </div>
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h4 style={{ marginTop: 0 }}>Gamification events</h4>
        <div style={{ fontSize: 24, fontWeight: 800, color: "var(--color-ai-600)" }}>{kpis.gamificationEventsCount}</div>
      </div>
    </section>
  );
}

