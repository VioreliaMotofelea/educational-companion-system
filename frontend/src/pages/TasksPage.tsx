import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useAnalytics } from "../hooks/useAnalytics";
import TodayPlan from "../components/dashboard/TodayPlan";

export default function TasksPage() {
  const { userId } = useCurrentUser();
  const analytics = useAnalytics(userId);

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16 }}>
        <h2 style={{ margin: 0 }}>Tasks & Scheduling</h2>
        <p style={{ margin: 0, color: "var(--muted)" }}>AI-assisted view (based on analytics)</p>
      </div>

      {!analytics ? (
        <div style={{ marginTop: 12, border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
          <p style={{ margin: 0, color: "var(--muted)" }}>Loading tasks...</p>
        </div>
      ) : (
        <section style={{ marginTop: 14, display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
          <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
            <h3 style={{ marginTop: 0, marginBottom: 6 }}>Completed</h3>
            <div style={{ fontSize: 32, fontWeight: 900, color: "var(--color-progress-600)" }}>{analytics.kpis.tasksCompleted}</div>
          </div>
          <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
            <h3 style={{ marginTop: 0, marginBottom: 6 }}>Pending</h3>
            <div style={{ fontSize: 32, fontWeight: 900 }}>{analytics.kpis.tasksPending}</div>
          </div>
          <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
            <h3 style={{ marginTop: 0, marginBottom: 6 }}>Overdue</h3>
            <div style={{ fontSize: 32, fontWeight: 900, color: "rgba(239, 68, 68, 0.95)" }}>{analytics.kpis.tasksOverdue}</div>
          </div>
        </section>
      )}

      <div style={{ marginTop: 18, color: "var(--muted)", fontSize: 12 }}>
        Calendar/timeline and “AI suggested schedule” are generated client-side for now (backend schedule APIs are planned).
      </div>

      <div style={{ marginTop: 16 }}>
        <TodayPlan title="AI suggested schedule" />
      </div>
    </AppLayout>
  );
}