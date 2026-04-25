import { useMemo } from "react";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useRecommendations } from "../../hooks/useRecommendations";
import { useUser } from "../../hooks/useUser";

type Props = {
  title?: string;
};

function addMinutes(base: Date, minutes: number) {
  return new Date(base.getTime() + minutes * 60_000);
}

function formatTime(d: Date) {
  return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

export default function TodayPlan({ title = "Today's plan" }: Props) {
  const { userId } = useCurrentUser();
  const { user, loading: userLoading } = useUser(userId);
  const {
    data: recommendations,
    loading: recLoading,
  } = useRecommendations(userId, 10);

  const dailyMinutes = user?.dailyAvailableMinutes ?? 0;

  const scheduleBlocks = useMemo(() => {
    if (!recommendations.length || dailyMinutes <= 0) return [];

    const blocks: Array<{
      title: string;
      reason: string;
      startMinutes: number;
      endMinutes: number;
      durationMinutes: number;
      topic: string;
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
        title: rec.resource.title,
        reason: rec.explanation,
        startMinutes: cursor,
        endMinutes: cursor + duration,
        durationMinutes: duration,
        topic: rec.resource.topic,
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
        <p style={{ margin: 0, color: "var(--muted)" }}>Generating AI suggested schedule...</p>
      </div>
    );
  }

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <h3 style={{ marginTop: 0 }}>{title}</h3>

      <p style={{ margin: "8px 0 0 0", color: "var(--muted)" }}>
        Daily available minutes: <b style={{ color: "var(--color-ai-600)" }}>{dailyMinutes}</b>
      </p>

      {dailyMinutes <= 0 ? (
        <p style={{ marginTop: 12, color: "var(--muted)" }}>Set your daily time in Profile to see a schedule.</p>
      ) : scheduleBlocks.length === 0 ? (
        <p style={{ marginTop: 12, color: "var(--muted)" }}>No schedule fits your available time yet.</p>
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
                  padding: 12,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                  <div>
                    <div style={{ fontWeight: 800, color: "var(--text)" }}>{idx + 1}. {b.title}</div>
                    <div style={{ color: "var(--muted)", marginTop: 4 }}>
                      Topic: <b>{b.topic}</b> • Difficulty: <b style={{ color: "var(--color-ai-600)" }}>{b.difficulty}</b>
                    </div>
                  </div>
                  <div style={{ textAlign: "right" }}>
                    <div style={{ fontWeight: 800 }}>{formatTime(start)} - {formatTime(end)}</div>
                    <div style={{ color: "var(--muted)", marginTop: 4 }}>{b.durationMinutes} min</div>
                  </div>
                </div>

                <p style={{ margin: "10px 0 0 0", color: "var(--muted)" }}>
                  💡 {b.reason}
                </p>
              </div>
            );
          })}
        </div>
      )}

      <div style={{ marginTop: 14, color: "var(--muted)", fontSize: 12 }}>
        AI suggested schedule is currently generated from your top recommendations and your daily available minutes.
      </div>
    </section>
  );
}