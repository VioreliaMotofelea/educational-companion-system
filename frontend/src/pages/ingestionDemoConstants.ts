export const RESOURCE_066 = "00000000-0000-4000-8000-000000000066";
export const RESOURCE_063 = "00000000-0000-4000-8000-000000000063";
export const RESOURCE_064 = "00000000-0000-4000-8000-000000000064";
export const RESOURCE_061 = "00000000-0000-4000-8000-000000000001";
export const RESOURCE_006 = "00000000-0000-4000-8000-000000000006";

export const SUPPORTED_EXTENSIONS = ".txt, .md, .markdown, .pdf, .docx";

export type DemoPreset = {
  id: string;
  label: string;
  learnerId: string;
  resourceId: string;
  hint: string;
};

export const DEMO_PRESETS: DemoPreset[] = [
  {
    id: "alex-066",
    label: "Alex + Course normalization (066)",
    learnerId: "demo-alex",
    resourceId: RESOURCE_066,
    hint: "demo-alex has course scope databases-demo-course",
  },
  {
    id: "bianca-066",
    label: "Bianca + same CourseOnly resource (066)",
    learnerId: "demo-bianca",
    resourceId: RESOURCE_066,
    hint: "demo-bianca should not access 066",
  },
  {
    id: "alex-063",
    label: "Alex + Global databases unit (063)",
    learnerId: "demo-alex",
    resourceId: RESOURCE_063,
    hint: "Global visibility — any learner",
  },
  {
    id: "alex-064",
    label: "Alex + AI unit (064) — PDF sample",
    learnerId: "demo-alex",
    resourceId: RESOURCE_064,
    hint: "Use ai-classification-metrics-worksheet.pdf",
  },
];

export const SAMPLE_FILES = [
  { name: "databases-normalization-notes.md", type: "Markdown" },
  { name: "programming-control-flow-practice.txt", type: "Plain text" },
  { name: "study-skills-spaced-repetition-plan.md", type: "Markdown" },
  { name: "web-development-accessibility-lab.docx", type: "DOCX" },
  { name: "ai-classification-metrics-worksheet.pdf", type: "PDF (selectable text)" },
  { name: "data-analysis-cleaning-checklist.txt", type: "Plain text" },
];
