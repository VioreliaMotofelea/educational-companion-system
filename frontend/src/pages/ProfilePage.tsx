import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useUser } from "../hooks/useUser";
import { useAnalytics } from "../hooks/useAnalytics";
import { useMastery } from "../hooks/useMastery";
import { updateUserPreferences } from "../services/api";
import type { UserPreferences } from "../types";

function toNullIfEmpty(value: string) {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

export default function ProfilePage() {
  const { userId } = useCurrentUser();
  const { user, loading, error, refresh } = useUser(userId);
  const analytics = useAnalytics(userId);
  const mastery = useMastery(userId);

  const initialPrefs = user?.preferences ?? null;

  const [preferredDifficulty, setPreferredDifficulty] = useState<string>("");
  const [preferredContentTypesCsv, setPreferredContentTypesCsv] = useState<string>("");
  const [preferredTopicsCsv, setPreferredTopicsCsv] = useState<string>("");
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveOk, setSaveOk] = useState<string | null>(null);

  useEffect(() => {
    setSaveOk(null);
    setSaveError(null);

    setPreferredDifficulty(
      initialPrefs?.preferredDifficulty == null ? "" : String(initialPrefs.preferredDifficulty),
    );
    setPreferredContentTypesCsv(initialPrefs?.preferredContentTypesCsv ?? "");
    setPreferredTopicsCsv(initialPrefs?.preferredTopicsCsv ?? "");
  }, [initialPrefs]);

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
      setSaveOk("Saved preferences.");
      refresh();
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Could not save preferences.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <AppLayout>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 16 }}>
        <h2 style={{ margin: 0 }}>User Profile</h2>
        <p style={{ margin: 0, color: "var(--muted)" }}>{loading ? "Loading..." : `Level ${user?.level ?? "-"}`}</p>
      </div>

      {error ? (
        <div style={{ marginTop: 14, border: "1px solid rgba(239, 68, 68, 0.6)", background: "rgba(239, 68, 68, 0.08)", borderRadius: "var(--radius-md)", padding: 16 }}>
          <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
        </div>
      ) : null}

      <div style={{ marginTop: 16, display: "grid", gridTemplateColumns: "minmax(320px, 1fr) minmax(320px, 1fr)", gap: 16, alignItems: "start" }}>
        <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
          <h3 style={{ marginTop: 0 }}>Preferințe</h3>
          <form onSubmit={onSave} style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 12 }}>
            <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              Preferred difficulty (1-5){" "}
              <select
                value={preferredDifficulty}
                onChange={(e) => setPreferredDifficulty(e.target.value)}
                style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: `1px solid var(--border-strong)`, background: "rgba(255,255,255,0.02)", color: "var(--text)" }}
              >
                <option value="">Auto / none</option>
                <option value="1">1 - Beginner</option>
                <option value="2">2</option>
                <option value="3">3 - Intermediate</option>
                <option value="4">4</option>
                <option value="5">5 - Advanced</option>
              </select>
            </label>

            <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              Preferred content types (CSV: Article,Video,Quiz)
              <input
                value={preferredContentTypesCsv}
                onChange={(e) => setPreferredContentTypesCsv(e.target.value)}
                placeholder="Article,Video"
                style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: `1px solid var(--border-strong)`, background: "rgba(255,255,255,0.02)", color: "var(--text)" }}
              />
            </label>

            <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              Preferred topics (CSV)
              <input
                value={preferredTopicsCsv}
                onChange={(e) => setPreferredTopicsCsv(e.target.value)}
                placeholder="Mathematics,Algorithms"
                style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: `1px solid var(--border-strong)`, background: "rgba(255,255,255,0.02)", color: "var(--text)" }}
              />
            </label>

            <div style={{ display: "flex", gap: 10, alignItems: "center", marginTop: 6 }}>
              <button
                type="submit"
                disabled={saving}
                style={{
                  background: "var(--color-ai-600)",
                  color: "white",
                  border: "none",
                  padding: "10px 14px",
                  borderRadius: "var(--radius-md)",
                  cursor: saving ? "not-allowed" : "pointer",
                  opacity: saving ? 0.7 : 1,
                }}
              >
                {saving ? "Saving..." : "Save"}
              </button>
              {saveOk ? <span style={{ color: "rgba(34, 197, 94, 0.95)" }}>{saveOk}</span> : null}
              {saveError ? <span style={{ color: "rgba(239, 68, 68, 0.95)" }}>{saveError}</span> : null}
            </div>
          </form>
        </section>

        <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
          <h3 style={{ marginTop: 0 }}>Nivel dificultate</h3>
          {mastery.loading ? (
            <p style={{ marginTop: 12, color: "var(--muted)" }}>Loading mastery...</p>
          ) : mastery.error ? (
            <p style={{ marginTop: 12, color: "rgba(239, 68, 68, 0.95)" }}>{mastery.error}</p>
          ) : mastery.data ? (
            <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 8 }}>
              <div>
                Suggested difficulty:{" "}
                <b style={{ color: "var(--color-ai-600)" }}>{mastery.data.suggestedDifficulty}</b>
              </div>
              {mastery.data.suggestedDifficultyReason ? (
                <div style={{ color: "var(--muted)" }}>
                  Motivation: {mastery.data.suggestedDifficultyReason}
                </div>
              ) : (
                <div style={{ color: "var(--muted)" }}>No reason provided.</div>
              )}
            </div>
          ) : (
            <p style={{ marginTop: 12, color: "var(--muted)" }}>No mastery data.</p>
          )}

          <div style={{ marginTop: 16, borderTop: "1px solid var(--border)", paddingTop: 16 }}>
            <h3 style={{ marginTop: 0, fontSize: 16 }}>Stats & comportament</h3>
            {analytics ? (
              <div style={{ marginTop: 10, display: "flex", flexDirection: "column", gap: 8 }}>
                <div>Current level: <b style={{ color: "var(--color-progress-600)" }}>{analytics.kpis.currentLevel}</b></div>
                <div>Completion: <b>{analytics.kpis.completionRatePercent.toFixed(0)}%</b></div>
                <div style={{ color: "var(--muted)" }}>
                  {analytics.summary.summaryText ? analytics.summary.summaryText : "No summary yet. Complete resources to generate insights."}
                </div>
              </div>
            ) : (
              <p style={{ marginTop: 10, color: "var(--muted)" }}>Loading analytics...</p>
            )}
          </div>
        </section>
      </div>
    </AppLayout>
  );
}