export const RESOURCE_066 = "00000000-0000-4000-8000-000000000066";
export const RESOURCE_063 = "00000000-0000-4000-8000-000000000063";
export const RESOURCE_064 = "00000000-0000-4000-8000-000000000064";
export const RESOURCE_001 = "00000000-0000-4000-8000-000000000001";
export const RESOURCE_006 = "00000000-0000-4000-8000-000000000006";

export const SUPPORTED_FILE_TYPES =
  "Plain text (.txt), Markdown (.md), Word (.docx), and PDF with selectable text";

export type LearnerOption = {
  id: string;
  name: string;
  description: string;
};

export const LEARNER_OPTIONS: LearnerOption[] = [
  {
    id: "demo-alex",
    name: "Alex",
    description: "Enrolled in the Databases course",
  },
  {
    id: "demo-bianca",
    name: "Bianca",
    description: "AI and Data Analysis learning path",
  },
  {
    id: "demo-catalin",
    name: "Catalin",
    description: "Programming and study skills focus",
  },
];

export type ResourceOption = {
  id: string;
  title: string;
  topic: string;
  accessLabel: string;
  suggestedFile?: string;
};

export const RESOURCE_OPTIONS: ResourceOption[] = [
  {
    id: RESOURCE_066,
    title: "Week 3 Reading — Normalization",
    topic: "Databases",
    accessLabel: "Course only",
    suggestedFile: "databases-normalization-notes.md",
  },
  {
    id: RESOURCE_063,
    title: "Databases learning unit (099)",
    topic: "Databases",
    accessLabel: "Everyone",
  },
  {
    id: RESOURCE_064,
    title: "AI learning unit",
    topic: "Artificial Intelligence",
    accessLabel: "Everyone",
    suggestedFile: "ai-classification-metrics-worksheet.pdf",
  },
  {
    id: RESOURCE_001,
    title: "Programming fundamentals",
    topic: "Programming",
    accessLabel: "Everyone",
    suggestedFile: "programming-control-flow-practice.txt",
  },
  {
    id: RESOURCE_006,
    title: "Study skills guide",
    topic: "Study Skills",
    accessLabel: "Everyone",
    suggestedFile: "study-skills-spaced-repetition-plan.md",
  },
];

export type QuickScenario = {
  id: string;
  label: string;
  learnerId: string;
  resourceId: string;
};

/** One-click setups for common presentation flows */
export const QUICK_SCENARIOS: QuickScenario[] = [
  {
    id: "course-upload",
    label: "Course reading upload",
    learnerId: "demo-alex",
    resourceId: RESOURCE_066,
  },
  {
    id: "access-check",
    label: "Compare course access",
    learnerId: "demo-bianca",
    resourceId: RESOURCE_066,
  },
];
