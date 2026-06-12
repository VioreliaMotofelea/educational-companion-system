# Demo resource files (ingestion V1)

Original **thesis educational materials** for the Educational Companion System. They demonstrate how teachers, admins, or learners attach meaningful course content to catalog resources. Summaries from extraction enrich **access-filtered** catalog rows for semantic recommendations.

**Do not** add copyrighted third-party PDFs, scanned/image-only files, or unsupported extensions.

## Supported extraction (V1)

| Extension | Backend method |
|-----------|----------------|
| `.txt` | Plain text |
| `.md`, `.markdown` | Markdown |
| `.docx` | DOCX (python-docx path in backend) |
| `.pdf` | Selectable text only (PdfPig) |

**OCR is not implemented.** Scanned PDFs should fail with a clear processing error. This folder intentionally excludes image-only samples.

## Files and catalog mapping

| File | Topic | Suggested resource | Visibility | Recommended for `/demo/ingestion` |
|------|-------|-------------------|------------|-----------------------------------|
| `databases-normalization-notes.md` | Databases | `...066` Week 3 Reading — Normalization | CourseOnly | **Yes** — primary privacy demo |
| `programming-control-flow-practice.txt` | Programming | `...001` Programming Fundamentals 001 | Global | Yes |
| `study-skills-spaced-repetition-plan.md` | Study Skills | `...006` Study Skills 006 | Global | Yes |
| `web-development-accessibility-lab.docx` | Web Dev | `...062` Web Development 098 | Global | Yes (regenerate binary) |
| `ai-classification-metrics-worksheet.pdf` | AI | `...064` AI Learning Unit 100 | Global | Yes (regenerate binary) |
| `data-analysis-cleaning-checklist.txt` | Data Analysis | `...00b` Data Analysis 011 | Global | Yes |
| `sample-databases-notes.md` | Databases | `...066` (legacy only) | CourseOnly | No — minimal smoke test only |

See `demo-resource-file-map.json` for machine-readable entries (documentation; not loaded by runtime).

## Regenerating DOCX and PDF

Binary files are generated deterministically:

```bash
.venv/bin/python scripts/demo/generate_binary_demo_files.py
```

Commit regenerated `.docx` / `.pdf` before demos, or run the script after cloning.

## Validate files exist

```bash
.venv/bin/python scripts/demo/validate_resource_demo_files.py
```

## Suggested manual demo sequence

1. **CourseOnly + privacy:** Preset **Alex + 066**, upload `databases-normalization-notes.md`. Expect `Completed`, non-empty `extractedTextSummary` on accessible catalog for Alex only.
2. **Blocked access:** Preset **Bianca + 066**. Upload or list should **403**; accessible catalog must not include 066.
3. **Global formats:** Upload `programming-control-flow-practice.txt` to `...001`, DOCX to `...062`, PDF to `...064` (preset Alex + 064 for PDF).
4. **Legacy sample (optional):** `sample-databases-notes.md` — short summary only; not the main story.

Full curl steps: `docs/demo/resource-ingestion-demo.md`. UI: `http://localhost:5173/demo/ingestion`.

## Privacy reminder

Extracted summaries **inherit the parent resource access scope**. CourseOnly uploads are invisible to learners outside the course scope, even when enrichment improves recommendations for eligible learners.
