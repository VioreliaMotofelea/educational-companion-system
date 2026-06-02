import { useMemo } from "react";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useRecommendations } from "../../hooks/useRecommendations";
import { useUser } from "../../hooks/useUser";
import { UI_LOCALE } from "../../constants/uiLocale";
import {
  friendlyRecommendationLoadError,
  humanizeResourceTitle,
  humanizeTopicLine,
  learnerFacingRecommendationReason,
  matchStrengthForLearner,
} from "../../utils/recommendationUtils";

type Props = {
  title?: string;
};

function addMinutes(base: Date, minutes: number) {
  return new Date(base.getTime() + minutes * 60_000);
}

function formatTime(d: Date) {
  return d.toLocaleTimeString(UI_LOCALE, { hour: "2-digit", minute: "2-digit" });
}

export default function TodayPlan({ title = "Today's plan" }: Props) {
  const { userId } = useCurrentUser();
  const { user, loading: userLoading } = useUser(userId);
  const {
    data: recommendations,
    loading: recLoading,
    error: recError,
    refetch,
  } = useRecommendations(userId, 10);

  const dailyMinutes = user?.dailyAvailableMinutes ?? 0;

  const scheduleBlocks = useMemo(() => {
    if (!recommendations.length || dailyMinutes <= 0) return [];

    const blocks: Array<{
      title: string;
      reason: string;
      matchLabel: string;
      startMinutes: number;
      endMinutes: number;
      durationMinutes: number;
      topic: string;
      contentType: string;
      difficulty: number;
      resourceId: string;
    }> = [];

    let remaining = dailyMinutes;
    let cursor = 0;

    for (const rec of recommendations) {
      if (remaining <= 0) break;
      const duration = rec.resource.estimatedDurationMinutes;
      if (!duration || duration <= 0) continue;

      if (duration > remaining) continue; // keep blocks whole for clean UX

      blocks.push({
        title: humanizeResourceTitle(rec.resource.title),
        reason: learnerFacingRecommendationReason(rec.explanation),
        matchLabel: matchStrengthForLearner(rec.score).label,
        startMinutes: cursor,
        endMinutes: cursor + duration,
        durationMinutes: duration,
        topic: humanizeTopicLine(rec.resource.topic),
        contentType: rec.resource.contentType,
        difficulty: rec.resource.difficulty,
        resourceId: rec.resource.id,
      });

      remaining -= duration;
      cursor += duration;
    }

    return blocks;
  }, [recommendations, dailyMinutes]);

  const baseStart = useMemo(() => {
    const d = new Date();
    d.setHours(9, 0, 0, 0);
    return d;
  }, []);

  if (userLoading || recLoading) {
    return (
      <div style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <p style={{ margin: 0, color: "var(--muted)" }}>Preparing your suggested schedule…</p>
      </div>
    );
  }

  if (recError) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>{title}</h3>
        <div
          style={{
            marginTop: 12,
            padding: 14,
            borderRadius: "var(--radius-md)",
            border: "1px solid rgba(251, 191, 36, 0.35)",
            background: "rgba(251, 191, 36, 0.08)",
          }}
        >
          <p style={{ margin: 0, color: "var(--text)", lineHeight: 1.55, fontWeight: 600 }}>
            Could not build today&apos;s schedule yet
          </p>
          <p style={{ margin: "8px 0 0 0", color: "var(--muted)", lineHeight: 1.5, fontSize: 14 }}>
            {friendlyRecommendationLoadError(recError)}
          </p>
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
      </section>
    );
  }

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <h3 style={{ marginTop: 0 }}>{title}</h3>

      <p style={{ margin: "8px 0 0 0", color: "var(--muted)", lineHeight: 1.5 }}>
        Based on your <b style={{ color: "var(--color-ai-600)" }}>{dailyMinutes}</b> minutes available today and your current top picks.
      </p>

      {dailyMinutes <= 0 ? (
        <p style={{ marginTop: 12, color: "var(--muted)" }}>
          Add how many minutes you can study each day in <b>Profile</b> — we will shape the plan around that.
        </p>
      ) : scheduleBlocks.length === 0 ? (
        <p style={{ marginTop: 12, color: "var(--muted)", lineHeight: 1.5 }}>
          {recommendations.length === 0
            ? "No recommendations loaded yet, or each item is longer than your available window. Try shorter sessions or refresh your picks on the Recommendations page."
            : "No single recommendation fits fully inside your remaining minutes today. Try freeing more time or completing shorter items first."}
        </p>
      ) : (
        <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 10 }}>
          {scheduleBlocks.map((b, idx) => {
            const start = addMinutes(baseStart, b.startMinutes);
            const end = addMinutes(baseStart, b.endMinutes);

            return (
              <div
                key={`${b.resourceId}-${idx}`}
                style={{
                  border: "1px solid var(--border)",
                  background: "rgba(255,255,255,0.03)",
                  borderRadius: "var(--radius-md)",
                  padding: 14,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "flex-start" }}>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
                      <span style={{ fontWeight: 800, color: "var(--text)" }}>
                        {idx + 1}. {b.title}
                      </span>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          padding: "2px 8px",
                          borderRadius: 999,
                          background: "rgba(245, 158, 11, 0.15)",
                          color: "var(--color-recommend-500)",
                          border: "1px solid rgba(245, 158, 11, 0.35)",
                        }}
                      >
                        {b.matchLabel}
                      </span>
                    </div>
                    <div style={{ color: "var(--muted)", marginTop: 6, fontSize: 13 }}>
                      {b.topic} · {b.contentType} · Level {b.difficulty}/5
                    </div>
                  </div>
                  <div
                    style={{
                      textAlign: "right",
                      flexShrink: 0,
                      padding: "6px 10px",
                      borderRadius: "var(--radius-md)",
                      background: "rgba(99, 102, 241, 0.12)",
                      border: "1px solid rgba(99, 102, 241, 0.25)",
                    }}
                  >
                    <div style={{ fontWeight: 800, fontSize: 14, color: "var(--color-ai-600)" }}>
                      {formatTime(start)} – {formatTime(end)}
                    </div>
                    <div style={{ color: "var(--muted)", marginTop: 2, fontSize: 12 }}>{b.durationMinutes} min</div>
                  </div>
                </div>

                <p style={{ margin: "12px 0 0 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>{b.reason}</p>
              </div>
            );
          })}
        </div>
      )}

      <div style={{ marginTop: 14, color: "var(--muted)", fontSize: 12, lineHeight: 1.45 }}>
        This schedule is a gentle suggestion built from your recommendations and daily time — adjust freely to match how you really study.
      </div>
    </section>
  );
}
