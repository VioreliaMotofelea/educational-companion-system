import { createInteraction } from "../../services/api";
import { useState } from "react";

type Props = {
  title: string;
  reason: string;
  resourceId: string;
  userId: string;
};

export default function RecommendationCard({
  title,
  reason,
  resourceId,
  userId,
}: Props) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleStart = async () => {
    setError(null);
    setIsSubmitting(true);

    try {
      await createInteraction({
        userId,
        learningResourceId: resourceId,
        interactionType: "Viewed",
        timeSpentMinutes: 0,
      });
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
      <h3 style={{ margin: 0 }}>{title}</h3>
      <p style={{ margin: "8px 0 0 0", color: "var(--muted)" }}>💡 {reason}</p>
      {error ? <p style={{ color: "rgba(239, 68, 68, 0.95)", margin: "10px 0 0 0" }}>{error}</p> : null}

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