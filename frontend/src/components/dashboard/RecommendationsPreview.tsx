import { useRecommendations } from "../../hooks/useRecommendations";
import RecommendationCard from "../recommendations/RecommendationCard";

const USER_ID = "11111111-1111-1111-1111-111111111111"; // temporar

export default function RecommendationsPreview() {
  const { data, loading } = useRecommendations(USER_ID);

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
        />
      ))}
    </div>
  );
}