export type StudyScheduleBlock =
  | {
      order: number;
      type: "Study";
      startTimeLocal: string;
      endTimeLocal: string;
      durationMinutes: number;
      taskId: string;
      learningResourceId: string | null;
      title: string;
      topic: string | null;
      contentType: string | null;
      difficulty: number | null;
      taskStatus: string | null;
      deadlineUtc: string | null;
      recommendationScore: number | null;
      explanation: string | null;
      description: string | null;
      sourceName: string | null;
      url: string | null;
      accessInstructions: string | null;
      label: null;
    }
  | {
      order: number;
      type: "Break";
      startTimeLocal: string;
      endTimeLocal: string;
      durationMinutes: number;
      taskId: null;
      learningResourceId: null;
      title: null;
      topic: null;
      contentType: null;
      difficulty: null;
      taskStatus: null;
      deadlineUtc: null;
      recommendationScore: null;
      explanation: null;
      label: string;
    };

export type StudyDaySchedule = {
  userId: string;
  scheduleDate: string;
  timeZoneId: string;
  dailyAvailableMinutes: number;
  plannedStudyMinutes: number;
  plannedBreakMinutes: number;
  unscheduledOpenTaskCount: number;
  summary: string;
  blocks: StudyScheduleBlock[];
};
