export type Mastery = {
  userId: string;
  topicMastery: TopicMasteryItem[];
  suggestedDifficulty: number;
  suggestedDifficultyReason: string | null;
  computedAtUtc: string;
};

export type TopicMasteryItem = {
  topic: string;
  resourcesCompleted: number;
  averageRating: number | null;
  averageDifficultyCompleted: number;
  masteryLevel: "None" | "Beginner" | "Intermediate" | "Advanced";
};