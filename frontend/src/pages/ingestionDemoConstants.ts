/** Preferred default when present in the signed-in user's accessible catalog. */
export const PREFERRED_RESOURCE_ID = "00000000-0000-4000-8000-000000000066";

export const SUPPORTED_FILE_TYPES =
  "Plain text (.txt), Markdown (.md), Word (.docx), and PDF with selectable text";

/** Optional hint shown when a resource is selected. */
export const SUGGESTED_FILE_BY_RESOURCE: Record<string, string> = {
  "00000000-0000-4000-8000-000000000066": "databases-normalization-notes.md",
  "00000000-0000-4000-8000-000000000064": "ai-classification-metrics-worksheet.pdf",
  "00000000-0000-4000-8000-000000000001": "programming-control-flow-practice.txt",
  "00000000-0000-4000-8000-000000000006": "study-skills-spaced-repetition-plan.md",
};
