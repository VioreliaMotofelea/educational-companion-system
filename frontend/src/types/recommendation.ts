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
    contentType: "Article" | "Video" | "Quiz";
    estimatedDurationMinutes: number;
    sourceName?: string | null;
    url?: string | null;
    accessType?: string;
    accessInstructions?: string | null;
    visibility?: string;
  };
};