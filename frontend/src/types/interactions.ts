export type InteractionType = "Viewed" | "Completed" | "Rated" | "Skipped";

export type CreateInteractionRequest = {
  userId: string;
  learningResourceId: string;
  interactionType: InteractionType;
  rating?: number;
  timeSpentMinutes?: number;
};

export type UserInteraction = {
  id: string;
  userId: string;
  learningResourceId: string;
  interactionType: InteractionType;
  rating: number | null;
  timeSpentMinutes: number | null;
  createdAtUtc: string;
};

