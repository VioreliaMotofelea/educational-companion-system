import {
  friendlyHybridReason,
  humanizeResourceTitle,
  humanizeTopicLine,
  hybridModelBreakdown,
  hybridTechnicalReason,
  matchStrengthForLearner,
  parseHybridExplanation,
} from "../../utils/recommendationUtils";
import ResourceStudyPanel from "../study/ResourceStudyPanel";

type Props = {
  title: string;
  reason: string;
  description?: string | null;
  topic?: string;
  contentType?: "Article" | "Video" | "Quiz";
  sourceName?: string | null;
  url?: string | null;
  accessType?: string;
  accessInstructions?: string | null;
  resourceId: string;
  userId: string;
  difficulty: number;
  durationMinutes: number;
  score: number;
};

function difficultyLabel(difficulty: number) {
  if (difficulty <= 1) return "Easy";
  if (difficulty === 2) return "Beginner+";
  if (difficulty === 3) return "Medium";
  if (difficulty === 4) return "Hard";
  return "Advanced";
}

function shortenText(text: string, maxLength: number) {
  if (text.length <= maxLength) return text;
  return `${text.slice(0, maxLength - 1)}...`;
}

export default function RecommendationCard({
  title,
  reason,
  description,
  topic,
  contentType,
  sourceName,
  url,
  accessInstructions,
  resourceId,
  userId,
  difficulty,
  durationMinutes,
  score,
}: Props) {
  const parsedReason = parseHybridExplanation(reason);
  const friendlyReason = parsedReason ? friendlyHybridReason(parsedReason) : reason;
  const technicalReason = parsedReason ? hybridTechnicalReason(parsedReason) : null;
  const modelBreakdown = parsedReason ? hybridModelBreakdown(parsedReason) : null;
  const displayTitle = humanizeResourceTitle(title);
  const topicLine = humanizeTopicLine(topic);
  const subtitle = [topicLine, contentType].filter(Boolean).join(" · ");
  const matchStrength = matchStrengthForLearner(score);
  const compactDescription = description ? shortenText(description, 140) : null;

  return (
    <div
      style={{
        border: "1px solid var(--border)",
        background: "var(--panel)",
        padding: "14px",
        margin: "10px 0",
        borderRadius: "var(--radius-md)",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "flex-start" }}>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <span
            style={{
              border: "1px solid var(--border)",
              background: "rgba(255,255,255,0.03)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--text)",
            }}
          >
            {difficultyLabel(difficulty)} • {difficulty}/5
          </span>
          <span
            style={{
              border: "1px solid var(--border)",
              background: "rgba(255,255,255,0.03)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--text)",
            }}
          >
            {durationMinutes} min
          </span>
          <span
            title={matchStrength.hint}
            style={{
              border: "1px solid var(--border)",
              background: "rgba(245, 158, 11, 0.12)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--color-recommend-500)",
              fontWeight: 800,
              cursor: "help",
            }}
          >
            {matchStrength.label}
          </span>
        </div>
      </div>
      <h3 style={{ margin: 0 }}>{displayTitle}</h3>
      <p style={{ margin: "6px 0 0 0", color: "var(--muted)", fontSize: 13 }}>{subtitle}</p>
      <p style={{ margin: "8px 0 0 0", color: "var(--muted)" }}>{friendlyReason}</p>
      {technicalReason ? (
        <details style={{ margin: "10px 0 0 0", color: "var(--muted)", fontSize: 13 }}>
          <summary style={{ cursor: "pointer", color: "var(--text)", fontWeight: 600 }}>
            How this pick was chosen
          </summary>
          <p style={{ margin: "8px 0 0 0", lineHeight: 1.5 }}>{technicalReason}</p>
          {modelBreakdown ? (
            <p style={{ margin: "6px 0 0 0", lineHeight: 1.5, opacity: 0.8 }}>{modelBreakdown}</p>
          ) : null}
        </details>
      ) : null}
      {compactDescription ? (
        <p style={{ margin: "6px 0 0 0", color: "var(--muted)", fontSize: 12, opacity: 0.85 }}>{compactDescription}</p>
      ) : null}

      <div style={{ marginTop: 12, borderTop: "1px solid var(--border)", paddingTop: 12 }}>
        <ResourceStudyPanel
          userId={userId}
          resourceId={resourceId}
          title={displayTitle}
          durationMinutes={durationMinutes}
          url={url}
          sourceName={sourceName}
          accessInstructions={accessInstructions}
          context="recommendation"
        />
      </div>
    </div>
  );
}
