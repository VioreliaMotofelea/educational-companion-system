import { useMemo, useState } from "react";
import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useAnalytics } from "../hooks/useAnalytics";
import TodayPlan from "../components/dashboard/TodayPlan";
import { useTasks } from "../hooks/useTasks";
import type { StudyTask } from "../types";

export default function TasksPage() {
  const { userId } = useCurrentUser();
  const analytics = useAnalytics(userId);
  const { tasks, loading: tasksLoading, error: tasksError, setTaskStatus, createTask, editTask, removeTask } = useTasks(userId);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null);
  const [pendingDeleteTask, setPendingDeleteTask] = useState<{ id: string; title: string } | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [formSaving, setFormSaving] = useState(false);
  const [draft, setDraft] = useState({
    title: "",
    notes: "",
    deadlineLocal: "",
    estimatedMinutes: 30,
    priority: 3,
  });

  const sortedTasks = useMemo(() => [...tasks].sort((a, b) => {
    const statusOrder = { Pending: 0, Overdue: 1, Completed: 2 } as const;
    const byStatus = statusOrder[a.status] - statusOrder[b.status];
    if (byStatus !== 0) return byStatus;
    return new Date(a.deadlineUtc).getTime() - new Date(b.deadlineUtc).getTime();
  }), [tasks]);

  const resetDraft = () =>
    setDraft({
      title: "",
      notes: "",
      deadlineLocal: "",
      estimatedMinutes: 30,
      priority: 3,
    });

  const beginEdit = (task: StudyTask) => {
    if (task.learningResourceId) {
      setFormError("This task is linked to a recommendation and is locked for editing.");
      return;
    }
    setFormError(null);
    setEditingTaskId(task.id);
    setDraft({
      title: task.title,
      notes: task.notes ?? "",
      deadlineLocal: toLocalInputValue(task.deadlineUtc),
      estimatedMinutes: task.estimatedMinutes,
      priority: task.priority,
    });
  };

  const submitCreate = async () => {
    const title = draft.title.trim();
    if (title.length < 3) {
      setFormError("Task title must have at least 3 characters.");
      return;
    }
    if (!draft.deadlineLocal) {
      setFormError("Please choose a deadline.");
      return;
    }

    setFormSaving(true);
    setFormError(null);
    try {
      await createTask({
        title,
        notes: draft.notes.trim() || null,
        deadlineUtc: new Date(draft.deadlineLocal).toISOString(),
        estimatedMinutes: draft.estimatedMinutes,
        priority: draft.priority,
      });
      setShowCreateForm(false);
      resetDraft();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : "Could not save task.");
    } finally {
      setFormSaving(false);
    }
  };

  const submitEdit = async () => {
    const title = draft.title.trim();
    if (!editingTaskId) return;
    if (title.length < 3) {
      setFormError("Task title must have at least 3 characters.");
      return;
    }
    if (!draft.deadlineLocal) {
      setFormError("Please choose a deadline.");
      return;
    }

    setFormSaving(true);
    setFormError(null);
    try {
      await editTask(editingTaskId, {
        title,
        notes: draft.notes.trim() || null,
        deadlineUtc: new Date(draft.deadlineLocal).toISOString(),
        estimatedMinutes: draft.estimatedMinutes,
        priority: draft.priority,
        learningResourceId: null,
      });
      setEditingTaskId(null);
      resetDraft();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : "Could not update task.");
    } finally {
      setFormSaving(false);
    }
  };

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
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>Your study tasks</h3>
          <button
            onClick={() => {
              setShowCreateForm((v) => !v);
              setEditingTaskId(null);
              setFormError(null);
              resetDraft();
            }}
            style={{
              background: "var(--accent)",
              color: "white",
              border: "none",
              borderRadius: "var(--radius-md)",
              padding: "7px 10px",
              cursor: "pointer",
              fontWeight: 700,
            }}
          >
            {showCreateForm ? "Cancel" : "Create custom task"}
          </button>
        </div>
        {showCreateForm ? (
          <TaskEditor
            draft={draft}
            onChange={setDraft}
            onSubmit={() => void submitCreate()}
            submitLabel="Save task"
            saving={formSaving}
          />
        ) : null}
        {formError ? (
          <p style={{ margin: "8px 0 0 0", color: "rgba(239, 68, 68, 0.95)", fontSize: 13 }}>{formError}</p>
        ) : null}
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
                    <button
                      onClick={() => beginEdit(task)}
                      disabled={Boolean(task.learningResourceId)}
                      title={task.learningResourceId ? "Linked recommendation tasks are locked for editing." : "Edit task"}
                      style={{
                        background: "transparent",
                        color: task.learningResourceId ? "var(--muted)" : "var(--text)",
                        border: "1px solid var(--border-strong)",
                        borderRadius: "var(--radius-md)",
                        padding: "7px 10px",
                        cursor: task.learningResourceId ? "not-allowed" : "pointer",
                        opacity: task.learningResourceId ? 0.7 : 1,
                      }}
                    >
                      {task.learningResourceId ? "Locked" : "Edit"}
                    </button>
                    <button
                      onClick={() => {
                        setFormError(null);
                        setPendingDeleteTask({
                          id: task.id,
                          title: task.learningResourceTitle ?? task.title,
                        });
                      }}
                      style={{
                        background: "transparent",
                        color: "rgba(239, 68, 68, 0.95)",
                        border: "1px solid rgba(239, 68, 68, 0.5)",
                        borderRadius: "var(--radius-md)",
                        padding: "7px 10px",
                        cursor: "pointer",
                      }}
                    >
                      Delete
                    </button>
                  </div>
                </div>
                {editingTaskId === task.id ? (
                  <div style={{ marginTop: 10 }}>
                    <TaskEditor
                      draft={draft}
                      onChange={setDraft}
                      onSubmit={() => void submitEdit()}
                      submitLabel="Save changes"
                      saving={formSaving}
                    />
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        )}
      </section>

      {pendingDeleteTask ? (
        <div
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(4, 8, 18, 0.82)",
            backdropFilter: "blur(4px)",
            WebkitBackdropFilter: "blur(4px)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1200,
            padding: 16,
          }}
        >
          <div
            style={{
              width: "100%",
              maxWidth: 520,
              border: "1px solid var(--border)",
              background: "rgba(12, 18, 34, 0.98)",
              borderRadius: "var(--radius-md)",
              padding: 16,
              boxShadow: "0 12px 40px rgba(0,0,0,0.35)",
            }}
          >
            <h3 style={{ marginTop: 0, marginBottom: 8 }}>Delete task?</h3>
            <p style={{ margin: 0, color: "var(--muted)" }}>
              This will permanently remove <b style={{ color: "var(--text)" }}>{pendingDeleteTask.title}</b>.
            </p>
            <div style={{ marginTop: 14, display: "flex", justifyContent: "flex-end", gap: 8 }}>
              <button
                onClick={() => setPendingDeleteTask(null)}
                style={{
                  background: "transparent",
                  color: "var(--text)",
                  border: "1px solid var(--border-strong)",
                  borderRadius: "var(--radius-md)",
                  padding: "7px 10px",
                  cursor: "pointer",
                }}
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  void removeTask(pendingDeleteTask.id)
                    .catch((err) => {
                      setFormError(err instanceof Error ? err.message : "Could not delete task.");
                    })
                    .finally(() => {
                      setPendingDeleteTask(null);
                    });
                }}
                style={{
                  background: "rgba(239, 68, 68, 0.95)",
                  color: "white",
                  border: "none",
                  borderRadius: "var(--radius-md)",
                  padding: "7px 10px",
                  cursor: "pointer",
                  fontWeight: 700,
                }}
              >
                Delete task
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </AppLayout>
  );
}

function TaskEditor({
  draft,
  onChange,
  onSubmit,
  submitLabel,
  saving,
}: {
  draft: {
    title: string;
    notes: string;
    deadlineLocal: string;
    estimatedMinutes: number;
    priority: number;
  };
  onChange: (next: {
    title: string;
    notes: string;
    deadlineLocal: string;
    estimatedMinutes: number;
    priority: number;
  }) => void;
  onSubmit: () => void;
  submitLabel: string;
  saving: boolean;
}) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "2fr 1.5fr 1fr 1fr auto", gap: 8 }}>
      <input placeholder="Task title" value={draft.title} onChange={(e) => onChange({ ...draft, title: e.target.value })} />
      <input type="datetime-local" value={draft.deadlineLocal} onChange={(e) => onChange({ ...draft, deadlineLocal: e.target.value })} />
      <input type="number" min={1} max={600} value={draft.estimatedMinutes} onChange={(e) => onChange({ ...draft, estimatedMinutes: Number(e.target.value) })} />
      <input type="number" min={1} max={5} value={draft.priority} onChange={(e) => onChange({ ...draft, priority: Number(e.target.value) })} />
      <button
        onClick={onSubmit}
        disabled={saving}
        style={{
          background: "rgba(34, 197, 94, 0.9)",
          color: "white",
          border: "none",
          borderRadius: "var(--radius-md)",
          padding: "7px 10px",
          cursor: saving ? "wait" : "pointer",
          opacity: saving ? 0.7 : 1,
          fontWeight: 700,
        }}
      >
        {saving ? "Saving..." : submitLabel}
      </button>
      <input
        style={{ gridColumn: "1 / span 5" }}
        placeholder="Notes (optional)"
        value={draft.notes}
        onChange={(e) => onChange({ ...draft, notes: e.target.value })}
      />
    </div>
  );
}

function toLocalInputValue(value: string): string {
  const date = new Date(value);
  const pad = (n: number) => n.toString().padStart(2, "0");
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hours = pad(date.getHours());
  const minutes = pad(date.getMinutes());
  return `${year}-${month}-${day}T${hours}:${minutes}`;
}