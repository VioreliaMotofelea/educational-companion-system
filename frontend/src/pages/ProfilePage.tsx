import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useUser } from "../hooks/useUser";
import { useAnalytics } from "../hooks/useAnalytics";
import { useMastery } from "../hooks/useMastery";
import { useInteractions } from "../hooks/useInteractions";
import { useResources } from "../hooks/useResources";
import { updateUserPreferences, updateUserStudySettings } from "../services/api";
import type { UserPreferences } from "../types";
import { formatAppDateTime } from "../utils/formatDate";

function toNullIfEmpty(value: string) {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function truncateId(value: string, max = 10) {
  if (value.length <= max) return value;
  return `${value.slice(0, 8)}…${value.slice(-2)}`;
}

export default function ProfilePage() {
  const { userId } = useCurrentUser();
  const { user, loading, error, refresh } = useUser(userId);
  const analytics = useAnalytics(userId);
  const mastery = useMastery(userId);
  const interactions = useInteractions(userId, 8);
  const resources = useResources();

  const resourceTitleById = useMemo(() => {
    const map = new Map<string, { title: string; topic: string }>();
    for (const r of resources.data) {
      map.set(r.id, { title: r.title, topic: r.topic });
    }
    return map;
  }, [resources.data]);

  const initialPrefs = user?.preferences ?? null;

  const [preferredDifficulty, setPreferredDifficulty] = useState<string>("");
  const [preferredContentTypesCsv, setPreferredContentTypesCsv] = useState<string>("");
  const [preferredTopicsCsv, setPreferredTopicsCsv] = useState<string>("");
  const [dailyAvailableMinutes, setDailyAvailableMinutes] = useState<string>("60");
  const [saving, setSaving] = useState(false);
  const [savingStudySettings, setSavingStudySettings] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveOk, setSaveOk] = useState<string | null>(null);
  const [studySaveError, setStudySaveError] = useState<string | null>(null);
  const [studySaveOk, setStudySaveOk] = useState<string | null>(null);

  useEffect(() => {
    setSaveOk(null);
    setSaveError(null);

    setPreferredDifficulty(
      initialPrefs?.preferredDifficulty == null ? "" : String(initialPrefs.preferredDifficulty),
    );
    setPreferredContentTypesCsv(initialPrefs?.preferredContentTypesCsv ?? "");
    setPreferredTopicsCsv(initialPrefs?.preferredTopicsCsv ?? "");
    setDailyAvailableMinutes(String(user?.dailyAvailableMinutes ?? 60));
  }, [initialPrefs, user?.dailyAvailableMinutes]);

  const diffValue = useMemo(() => {
    if (preferredDifficulty.trim() === "") return null;
    const parsed = Number(preferredDifficulty);
    return Number.isFinite(parsed) ? parsed : null;
  }, [preferredDifficulty]);

  const payload: UserPreferences = useMemo(
    () => ({
      preferredDifficulty: diffValue,
      preferredContentTypesCsv: toNullIfEmpty(preferredContentTypesCsv),
      preferredTopicsCsv: toNullIfEmpty(preferredTopicsCsv),
    }),
    [diffValue, preferredContentTypesCsv, preferredTopicsCsv],
  );

  const onSave = async (e: FormEvent) => {
    e.preventDefault();
    if (saving) return;

    setSaving(true);
    setSaveError(null);
    setSaveOk(null);

    try {
      await updateUserPreferences(userId, payload);
      setSaveOk("Saved.");
      refresh();
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Could not save preferences.");
    } finally {
      setSaving(false);
    }
  };

  const onSaveStudySettings = async (e: FormEvent) => {
    e.preventDefault();
    if (savingStudySettings) return;

    const parsed = Number(dailyAvailableMinutes);
    if (!Number.isFinite(parsed) || parsed < 15 || parsed > 600) {
      setStudySaveError("Use 15–600 minutes.");
      return;
    }

    setSavingStudySettings(true);
    setStudySaveError(null);
    setStudySaveOk(null);

    try {
      await updateUserStudySettings(userId, { dailyAvailableMinutes: parsed });
      setStudySaveOk("Saved.");
      refresh();
      window.dispatchEvent(new Event("task-updated"));
    } catch (err) {
      setStudySaveError(err instanceof Error ? err.message : "Could not save study settings.");
    } finally {
      setSavingStudySettings(false);
    }
  };

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16 }}>
        <div>
          <h2 style={{ margin: 0 }}>Profile</h2>
          <p style={{ margin: "4px 0 0 0", color: "var(--muted)", fontSize: 14 }}>
            Study budget, content preferences, and your learning progress.
          </p>
        </div>
        <p style={{ margin: 0, color: "var(--muted)", fontSize: 14, flexShrink: 0 }}>
          {loading ? "…" : `Level ${user?.level ?? "—"}`}
        </p>
      </div>

      {error ? (
        <div
          style={{
            marginTop: 14,
            border: "1px solid rgba(239, 68, 68, 0.6)",
            background: "rgba(239, 68, 68, 0.08)",
            borderRadius: "var(--radius-md)",
            padding: 16,
          }}
        >
          <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
        </div>
      ) : null}

      <div className="profile-page">
        <section className="profile-card">
          <h3>Study & preferences</h3>
          <p className="profile-card-sub">Controls your daily plan, calendar deadlines, and recommendation tuning.</p>

          <div className="profile-settings-split">
            <div>
              <h4 style={{ margin: "0 0 10px 0", fontSize: 14, fontWeight: 700 }}>Daily study budget</h4>
              <form onSubmit={onSaveStudySettings} className="profile-form">
                <label>
                  Minutes per day (15–600)
                  <input
                    type="number"
                    min={15}
                    max={600}
                    value={dailyAvailableMinutes}
                    onChange={(e) => setDailyAvailableMinutes(e.target.value)}
                  />
                </label>
                <div className="profile-form-actions">
                  <button type="submit" disabled={savingStudySettings} className="profile-btn-primary">
                    {savingStudySettings ? "Saving…" : "Save budget"}
                  </button>
                  {studySaveOk ? <span style={{ color: "rgba(34, 197, 94, 0.95)", fontSize: 13 }}>{studySaveOk}</span> : null}
                  {studySaveError ? (
                    <span style={{ color: "rgba(239, 68, 68, 0.95)", fontSize: 13 }}>{studySaveError}</span>
                  ) : null}
                </div>
              </form>
            </div>

            <div>
              <h4 style={{ margin: "0 0 10px 0", fontSize: 14, fontWeight: 700 }}>Content preferences</h4>
              <form onSubmit={onSave} className="profile-form">
                <label>
                  Preferred difficulty
                  <select value={preferredDifficulty} onChange={(e) => setPreferredDifficulty(e.target.value)}>
                    <option value="">Auto</option>
                    <option value="1">1 — Beginner</option>
                    <option value="2">2</option>
                    <option value="3">3 — Intermediate</option>
                    <option value="4">4</option>
                    <option value="5">5 — Advanced</option>
                  </select>
                </label>

                <label>
                  Preferred formats
                  <input
                    value={preferredContentTypesCsv}
                    onChange={(e) => setPreferredContentTypesCsv(e.target.value)}
                    placeholder="Article, Video, Quiz"
                  />
                </label>

                <label>
                  Preferred topics
                  <input
                    value={preferredTopicsCsv}
                    onChange={(e) => setPreferredTopicsCsv(e.target.value)}
                    placeholder="Python, Databases"
                  />
                </label>

                <div className="profile-form-actions">
                  <button type="submit" disabled={saving} className="profile-btn-primary">
                    {saving ? "Saving…" : "Save preferences"}
                  </button>
                  {saveOk ? <span style={{ color: "rgba(34, 197, 94, 0.95)", fontSize: 13 }}>{saveOk}</span> : null}
                  {saveError ? <span style={{ color: "rgba(239, 68, 68, 0.95)", fontSize: 13 }}>{saveError}</span> : null}
                </div>
              </form>
            </div>
          </div>
        </section>

        <div className="profile-insights-grid">
          <section className="profile-card">
            <h3>Learning progress</h3>
            <p className="profile-card-sub">Suggested level from your activity and completion.</p>

            {mastery.loading ? (
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>Loading…</p>
            ) : mastery.error ? (
              <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)", fontSize: 13 }}>{mastery.error}</p>
            ) : (
              <>
                <div className="profile-stat-row">
                  <div className="profile-stat">
                    <div className="profile-stat-label">Suggested level</div>
                    <div className="profile-stat-value" style={{ color: "var(--color-ai-600)" }}>
                      {mastery.data?.suggestedDifficulty ?? "—"}
                    </div>
                  </div>
                  <div className="profile-stat">
                    <div className="profile-stat-label">Current level</div>
                    <div className="profile-stat-value" style={{ color: "var(--color-progress-600)" }}>
                      {analytics?.kpis.currentLevel ?? user?.level ?? "—"}
                    </div>
                  </div>
                  <div className="profile-stat">
                    <div className="profile-stat-label">Completion</div>
                    <div className="profile-stat-value">
                      {analytics ? `${analytics.kpis.completionRatePercent.toFixed(0)}%` : "—"}
                    </div>
                  </div>
                </div>
                {mastery.data?.suggestedDifficultyReason ? (
                  <p style={{ margin: "14px 0 0 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>
                    {mastery.data.suggestedDifficultyReason}
                  </p>
                ) : null}
                {analytics?.summary.summaryText ? (
                  <p style={{ margin: "10px 0 0 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>
                    {analytics.summary.summaryText}
                  </p>
                ) : null}
              </>
            )}
          </section>

          <section className="profile-card">
            <h3>Recent activity</h3>
            <p className="profile-card-sub">Latest interactions that feed recommendations.</p>

            {interactions.loading ? (
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>Loading…</p>
            ) : interactions.error ? (
              <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)", fontSize: 13 }}>{interactions.error}</p>
            ) : interactions.data.length === 0 ? (
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>No interactions yet.</p>
            ) : (
              <ul className="profile-activity-list">
                {interactions.data.map((it) => {
                  const typeColor =
                    it.interactionType === "Completed"
                      ? "var(--color-progress-600)"
                      : it.interactionType === "Rated"
                        ? "var(--color-recommend-600)"
                        : it.interactionType === "Viewed"
                          ? "var(--color-ai-600)"
                          : it.interactionType === "Skipped"
                            ? "rgba(245, 158, 11, 0.95)"
                            : "var(--muted)";

                  return (
                    <li key={it.id} className="profile-activity-item">
                      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "flex-start" }}>
                        <div style={{ minWidth: 0 }}>
                          <div style={{ fontWeight: 800, color: typeColor }}>{it.interactionType}</div>
                          <div style={{ color: "var(--muted)", marginTop: 4 }}>
                            {resourceTitleById.get(it.learningResourceId)?.title ??
                              truncateId(it.learningResourceId)}
                            {resourceTitleById.get(it.learningResourceId)?.topic ? (
                              <span> · {resourceTitleById.get(it.learningResourceId)?.topic}</span>
                            ) : null}
                          </div>
                          {it.rating != null ? (
                            <div style={{ color: "var(--muted)", marginTop: 4 }}>
                              Rating: <b>{it.rating}</b>/5
                            </div>
                          ) : null}
                        </div>
                        <div style={{ textAlign: "right", color: "var(--muted)", fontSize: 12, whiteSpace: "nowrap" }}>
                          {formatAppDateTime(it.createdAtUtc, { includeSeconds: true })}
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </section>
        </div>
      </div>
    </AppLayout>
  );
}
