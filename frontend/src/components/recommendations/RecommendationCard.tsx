import { createInteraction } from "../../services/api";
import { useState } from "react";

type Props = {
  title: string;
  reason: string;
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

export default function RecommendationCard({
  title,
  reason,
  resourceId,
  userId,
  difficulty,
  durationMinutes,
  score,
}: Props) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const handleStart = async () => {
    setError(null);
    setOk(null);
    setIsSubmitting(true);

    try {
      await createInteraction({
        userId,
        learningResourceId: resourceId,
        interactionType: "Viewed",
        timeSpentMinutes: 0,
      });
      setOk("Saved to your activity.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not start resource.");
    } finally {
      setIsSubmitting(false);
    }
  };

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
            style={{
              border: "1px solid var(--border)",
              background: "rgba(245, 158, 11, 0.12)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--color-recommend-500)",
              fontWeight: 800,
            }}
          >
            AI score: {score.toFixed(2)}
          </span>
        </div>
      </div>
      <h3 style={{ margin: 0 }}>{title}</h3>
      <p style={{ margin: "8px 0 0 0", color: "var(--muted)" }}>💡 {reason}</p>
      {error ? <p style={{ color: "rgba(239, 68, 68, 0.95)", margin: "10px 0 0 0" }}>{error}</p> : null}
      {ok ? <p style={{ color: "rgba(34, 197, 94, 0.95)", margin: "10px 0 0 0" }}>{ok}</p> : null}

      <button
        onClick={handleStart}
        disabled={isSubmitting}
        style={{
          marginTop: "12px",
          background: "var(--color-recommend-600)",
          color: "white",
          border: "none",
          padding: "10px 14px",
          borderRadius: "var(--radius-md)",
          cursor: isSubmitting ? "not-allowed" : "pointer",
          opacity: isSubmitting ? 0.7 : 1,
        }}
      >
        {isSubmitting ? "Starting..." : "Start"}
      </button>
    </div>
  );
}