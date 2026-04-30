import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useAnalytics } from "../hooks/useAnalytics";
import TodayPlan from "../components/dashboard/TodayPlan";
import { useTasks } from "../hooks/useTasks";

export default function TasksPage() {
  const { userId } = useCurrentUser();
  const analytics = useAnalytics(userId);
  const { tasks, loading: tasksLoading, error: tasksError, setTaskStatus } = useTasks(userId);

  const sortedTasks = [...tasks].sort((a, b) => {
    const statusOrder = { Pending: 0, Overdue: 1, Completed: 2 } as const;
    const byStatus = statusOrder[a.status] - statusOrder[b.status];
    if (byStatus !== 0) return byStatus;
    return new Date(a.deadlineUtc).getTime() - new Date(b.deadlineUtc).getTime();
  });

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

      <section style={{ marginTop: 16, border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Your study tasks</h3>
        {tasksLoading ? (
          <p style={{ margin: 0, color: "var(--muted)" }}>Loading tasks...</p>
        ) : tasksError ? (
          <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{tasksError}</p>
        ) : sortedTasks.length === 0 ? (
          <p style={{ margin: 0, color: "var(--muted)" }}>
            No tasks yet. They are auto-created when recommendations are generated.
          </p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {sortedTasks.map((task) => (
              <div
                key={task.id}
                style={{
                  border: "1px solid var(--border)",
                  background: "rgba(255,255,255,0.02)",
                  borderRadius: "var(--radius-md)",
                  padding: 12,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "baseline" }}>
                  <div>
                    <div style={{ fontWeight: 800 }}>{task.learningResourceTitle ?? task.title}</div>
                    <div style={{ color: "var(--muted)", fontSize: 13 }}>
                      Status: <b>{task.status}</b> • Due: {new Date(task.deadlineUtc).toLocaleString()} • {task.estimatedMinutes} min
                    </div>
                  </div>
                  <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                    {task.status !== "Completed" ? (
                      <button
                        onClick={() => void setTaskStatus(task.id, "Completed")}
                        style={{
                          background: "rgba(34, 197, 94, 0.9)",
                          color: "white",
                          border: "none",
                          borderRadius: "var(--radius-md)",
                          padding: "7px 10px",
                          cursor: "pointer",
                        }}
                      >
                        Mark completed
                      </button>
                    ) : (
                      <button
                        onClick={() => void setTaskStatus(task.id, "Pending")}
                        style={{
                          background: "transparent",
                          color: "var(--text)",
                          border: "1px solid var(--border-strong)",
                          borderRadius: "var(--radius-md)",
                          padding: "7px 10px",
                          cursor: "pointer",
                        }}
                      >
                        Reopen
                      </button>
                    )}
                    {task.status !== "Overdue" ? (
                      <button
                        onClick={() => void setTaskStatus(task.id, "Overdue")}
                        style={{
                          background: "transparent",
                          color: "rgba(239, 68, 68, 0.95)",
                          border: "1px solid rgba(239, 68, 68, 0.5)",
                          borderRadius: "var(--radius-md)",
                          padding: "7px 10px",
                          cursor: "pointer",
                        }}
                      >
                        Mark overdue
                      </button>
                    ) : null}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </AppLayout>
  );
}