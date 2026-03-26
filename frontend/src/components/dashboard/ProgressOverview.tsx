import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useAnalytics } from "../../hooks/useAnalytics";

export default function ProgressOverview() {
  const { userId } = useCurrentUser();
  const analytics = useAnalytics(userId);

  if (!analytics) {
    return (
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <p style={{ margin: 0, color: "var(--muted)" }}>Loading progress...</p>
      </div>
    );
  }

  const { completionRatePercent, tasksCompleted, tasksPending, tasksOverdue, totalTimeSpentMinutes, totalXpEarned, currentLevel } =
    analytics.kpis;

  return (
    <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Progres</h3>
        <div style={{ display: "flex", gap: 10, alignItems: "baseline", marginTop: 8 }}>
          <div style={{ fontSize: 28, fontWeight: 800, color: "var(--color-progress-600)" }}>{completionRatePercent.toFixed(0)}%</div>
          <div style={{ color: "var(--muted)" }}>completion rate</div>
        </div>
        <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 6 }}>
          <div>Completed: <b style={{ color: "var(--color-progress-600)" }}>{tasksCompleted}</b></div>
          <div>Pending: <b>{tasksPending}</b></div>
          <div>Overdue: <b style={{ color: "rgba(239, 68, 68, 0.95)" }}>{tasksOverdue}</b></div>
        </div>
      </div>

      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Level & Gains</h3>
        <div style={{ marginTop: 10, display: "flex", flexDirection: "column", gap: 6 }}>
          <div>Current level: <b style={{ color: "var(--color-ai-600)" }}>{currentLevel}</b></div>
          <div>Total XP: <b>{totalXpEarned}</b></div>
          <div>Total time spent: <b>{totalTimeSpentMinutes}</b> min</div>
        </div>
      </div>
    </section>
  );
}

