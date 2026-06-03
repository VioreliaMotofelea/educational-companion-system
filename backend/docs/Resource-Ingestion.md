# Resource ingestion (V1)

## Purpose

Optional **uploaded educational files** attached to existing `LearningResource` rows. Machine-readable text is extracted locally and a **short summary** enriches the recommendation candidate catalog. Private/offline/course materials do not become public URLs; access scopes are unchanged.

## Pipeline

1. Client uploads a supported file to `POST /api/resources/{resourceId}/files?userId=...` after access checks on the parent resource.
2. File bytes are stored on disk under `ResourceFiles:StoragePath` with a safe `storageKey` (GUID-based path segment).
3. `ResourceFile` metadata is persisted with processing status.
4. **Direct extraction** runs synchronously (MVP): TXT/MD (UTF-8), DOCX (paragraphs), PDF (embedded/selectable text via PdfPig).
5. Normalized text is capped (`MaxExtractedTextLength`); a deterministic summary is stored (`MaxSummaryLength`, first N characters).
6. `ResourceExtractedText` row links file + resource.
7. `GET /api/users/{id}/resources/accessible` exposes only `extractedTextSummary` (not full text) for resources already in the accessible set.

## Direct extraction vs OCR

| Approach | Status | Use case |
|----------|--------|----------|
| Direct text (V1) | Implemented | DOCX, TXT, Markdown, PDFs with selectable text |
| OCR (`OcrFuture` enum) | **Not implemented** | Scanned PDFs, photos, handwriting |

When a PDF yields fewer than `MinMeaningfulExtractedCharacters` characters, the file is marked **Failed** with a safe message that OCR would be required later. No Tesseract, cloud OCR, or external shells.

## Privacy and access

- Upload/list/delete/extracted-text endpoints require the same access as the parent resource (`IResourceAccessRepository`).
- Extracted text never appears on `GET /api/resources` (full catalog).
- The AI service only receives summaries for resources returned by the accessible endpoint (already filtered).
- Logs must not include full extracted text, URLs, or access instructions.

## Limitations (thesis MVP)

- No OCR, handwriting, or image ingestion
- No Teams/Moodle/Discord scraping or live website fetch
- No public file URLs; local disk storage only
- No PPTX extraction
- No LLM summarization

See also `API-Reference.md` (ingestion endpoints) and `For-AI-Service.md` (ranking field `extractedTextSummary`).
