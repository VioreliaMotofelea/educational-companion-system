import { Link } from "react-router-dom";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useStudySchedule } from "../../hooks/useStudySchedule";
import ResourceStudyPanel from "../study/ResourceStudyPanel";
import {
  humanizeResourceTitle,
  humanizeTopicLine,
  learnerFacingRecommendationReason,
  matchStrengthForLearner,
} from "../../utils/recommendationUtils";

type Props = {
  title?: string;
  /** Shorter copy for Calendar and other dense layouts */
  compact?: boolean;
};

export default function TodayPlan({ title = "Today's study plan", compact = false }: Props) {
  const { userId } = useCurrentUser();
  const { schedule, loading, error } = useStudySchedule(userId);

  if (loading) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>{title}</h3>
        <p style={{ margin: "10px 0 0 0", color: "var(--muted)" }}>Loading your study plan…</p>
      </section>
    );
  }

  if (error || !schedule) {
    return (
      <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>{title}</h3>
        <p style={{ margin: "10px 0 0 0", color: "rgba(239, 68, 68, 0.95)" }}>
          {error ?? "Could not load today's study plan."}
        </p>
      </section>
    );
  }

  const studyBlocks = schedule.blocks.filter((block) => block.type === "Study");
  let studyIndex = 0;

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <h3 style={{ marginTop: 0 }}>{title}</h3>

      <p style={{ margin: "8px 0 0 0", color: "var(--muted)", lineHeight: 1.5, fontSize: compact ? 13 : undefined }}>
        {compact ? (
          <>
            {schedule.dailyAvailableMinutes} min today ·{" "}
            <Link to="/profile" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
              Edit budget
            </Link>
          </>
        ) : (
          <>
            Built from your <b style={{ color: "var(--color-ai-600)" }}>open study tasks</b> and{" "}
            <b style={{ color: "var(--color-ai-600)" }}>{schedule.dailyAvailableMinutes} minutes</b> available today.
            Overdue tasks are scheduled first, with short breaks between sessions.{" "}
            <Link to="/profile" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
              Update daily budget
            </Link>
          </>
        )}
      </p>

      {studyBlocks.length === 0 ? (
        <p style={{ marginTop: 12, color: "var(--muted)", lineHeight: 1.5 }}>{schedule.summary}</p>
      ) : (
        <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 10 }}>
          {schedule.blocks.map((block) => {
            if (block.type === "Break") {
              return (
                <div
                  key={`break-${block.order}`}
                  style={{
                    border: "1px dashed var(--border-strong)",
                    background: "rgba(255,255,255,0.02)",
                    borderRadius: "var(--radius-md)",
                    padding: "10px 14px",
                    display: "flex",
                    justifyContent: "space-between",
                    gap: 12,
                    alignItems: "center",
                  }}
                >
                  <div style={{ color: "var(--muted)", fontWeight: 600 }}>{block.label}</div>
                  <div style={{ color: "var(--muted)", fontSize: 13 }}>
                    {block.startTimeLocal} – {block.endTimeLocal} · {block.durationMinutes} min
                  </div>
                </div>
              );
            }

            const matchLabel =
              block.recommendationScore != null
                ? matchStrengthForLearner(block.recommendationScore).label
                : null;
            studyIndex += 1;
            const topicLine = [humanizeTopicLine(block.topic ?? ""), block.contentType]
              .filter(Boolean)
              .join(" · ");
            const reason = block.explanation
              ? learnerFacingRecommendationReason(block.explanation)
              : null;
            const resourceId = block.learningResourceId;

            return (
              <div
                key={`study-${block.taskId}`}
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
                        {studyIndex}. {humanizeResourceTitle(block.title ?? "Study task")}
                      </span>
                      {block.taskStatus === "Overdue" ? (
                        <span
                          style={{
                            fontSize: 11,
                            fontWeight: 700,
                            padding: "2px 8px",
                            borderRadius: 999,
                            background: "rgba(239, 68, 68, 0.12)",
                            color: "rgba(239, 68, 68, 0.95)",
                            border: "1px solid rgba(239, 68, 68, 0.35)",
                          }}
                        >
                          Overdue
                        </span>
                      ) : null}
                      {matchLabel && !compact ? (
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
                          {matchLabel}
                        </span>
                      ) : null}
                    </div>
                    {topicLine ? (
                      <div style={{ color: "var(--muted)", marginTop: 6, fontSize: 13 }}>
                        {topicLine}
                        {block.difficulty != null ? ` · Level ${block.difficulty}/5` : ""}
                      </div>
                    ) : null}
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
                      {block.startTimeLocal} – {block.endTimeLocal}
                    </div>
                    <div style={{ color: "var(--muted)", marginTop: 2, fontSize: 12 }}>{block.durationMinutes} min</div>
                  </div>
                </div>
                {reason && !compact ? (
                  <p style={{ margin: "12px 0 0 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>{reason}</p>
                ) : null}

                {resourceId && userId ? (
                  <div style={{ marginTop: 12, borderTop: "1px solid var(--border)", paddingTop: 12 }}>
                    <ResourceStudyPanel
                      userId={userId}
                      resourceId={resourceId}
                      title={block.title ?? "Study resource"}
                      durationMinutes={block.durationMinutes}
                      url={block.url}
                      sourceName={block.sourceName}
                      accessInstructions={block.accessInstructions}
                      context="plan"
                      compact={compact}
                      linkedTaskStatus={
                        block.taskStatus === "Pending" || block.taskStatus === "Overdue" || block.taskStatus === "Completed"
                          ? block.taskStatus
                          : null
                      }
                    />
                  </div>
                ) : (
                  <p style={{ margin: "12px 0 0 0", color: "var(--muted)", fontSize: 13 }}>
                    Custom task — manage it in{" "}
                    <Link to="/tasks#tasks-list" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
                      your task list
                    </Link>
                    .
                  </p>
                )}
              </div>
            );
          })}
        </div>
      )}

      <p style={{ marginTop: 14, color: "var(--muted)", fontSize: 12, lineHeight: 1.45 }}>
        {schedule.summary}
      </p>
    </section>
  );
}
