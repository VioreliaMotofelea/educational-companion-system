export type Analytics = {
  userId: string;
  summary: AnalyticsSummary;
  kpis: AnalyticsKpis;
};

export type AnalyticsSummary = {
  summaryText: string | null;
  computedAtUtc: string;
};

export type AnalyticsKpis = {
  totalResourcesViewed: number;
  totalResourcesCompleted: number;
  completionRatePercent: number;
  averageRatingGiven: number | null;
  totalTimeSpentMinutes: number;
  totalXpEarned: number;
  currentLevel: number;
  tasksCompleted: number;
  tasksPending: number;
  tasksOverdue: number;
  gamificationEventsCount: number;
};