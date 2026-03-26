export type Recommendation = {
  recommendationId: string;
  score: number;
  algorithmUsed: string;
  explanation: string;
  createdAtUtc: string;

  resource: {
    id: string;
    title: string;
    description: string | null;
    topic: string;
    difficulty: number;
    contentType: string;
    estimatedDurationMinutes: number;
  };
};