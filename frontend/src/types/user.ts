export type UserProfile = {
  userId: string;
  level: number;
  xp: number;
  dailyAvailableMinutes: number;
  preferences: UserPreferences | null;
};

export type UserPreferences = {
  preferredDifficulty: number | null;
  preferredContentTypesCsv: string | null;
  preferredTopicsCsv: string | null;
};

export type UserXp = {
  userId: string;
  level: number;
  xp: number;
};