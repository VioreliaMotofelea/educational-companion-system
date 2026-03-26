import { createInteraction } from "../../services/api";

type Props = {
  title: string;
  reason: string;
  resourceId: string;
};

const USER_ID = "11111111-1111-1111-1111-111111111111";

export default function RecommendationCard({
  title,
  reason,
  resourceId,
}: Props) {

  const handleStart = () => {
    createInteraction({
      userId: USER_ID,
      learningResourceId: resourceId,
      interactionType: "Viewed",
      timeSpentMinutes: 0,
    });
  };

  return (
    <div style={{ border: "1px solid #ddd", padding: "10px", margin: "10px 0" }}>
      <h3>{title}</h3>
      <p>💡 {reason}</p>

      <button onClick={handleStart}>Start</button>
    </div>
  );
}