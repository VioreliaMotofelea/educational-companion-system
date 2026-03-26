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
    <div style={{ border: "1px solid #ddd", padding: "10px", margin: "10px 0" }}>
      <h3>{title}</h3>
      <p>💡 {reason}</p>
      {error ? <p style={{ color: "#b91c1c" }}>{error}</p> : null}

      <button onClick={handleStart} disabled={isSubmitting}>
        {isSubmitting ? "Starting..." : "Start"}
      </button>
    </div>
  );
}