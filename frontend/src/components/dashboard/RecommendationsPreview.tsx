import { useRecommendations } from "../../hooks/useRecommendations";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import RecommendationCard from "../recommendations/RecommendationCard";

export default function RecommendationsPreview() {
  const { userId } = useCurrentUser();
  const { data, loading } = useRecommendations(userId);

  if (loading) return <p>Loading...</p>;

  return (
    <div>
      <h2>Recommended for you</h2>

      {data.map((rec) => (
        <RecommendationCard
          key={rec.recommendationId}
          title={rec.resource.title}
          reason={rec.explanation}
          resourceId={rec.resource.id}
          userId={userId}
        />
      ))}
    </div>
  );
}