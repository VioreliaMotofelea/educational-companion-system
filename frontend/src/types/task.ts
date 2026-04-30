export type TaskStatus = "Pending" | "Completed" | "Overdue";

export type StudyTask = {
  id: string;
  userId: string;
  learningResourceId: string | null;
  learningResourceTitle: string | null;
  title: string;
  notes: string | null;
  deadlineUtc: string;
  estimatedMinutes: number;
  priority: number;
  status: TaskStatus;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

