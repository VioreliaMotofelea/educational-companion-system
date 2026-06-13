import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import AppLayout from "../components/layout/AppLayout";
import {
  deleteResourceFile,
  getAccessibleResources,
  getResourceExtractedText,
  listResourceFiles,
  uploadResourceFile,
  type AccessibleLearningResource,
  type ResourceExtractedTextRecord,
  type ResourceFileRecord,
} from "../services/resourceIngestionService";
import { formatUtcDateTime } from "../utils/formatDate";
import {
  LEARNER_OPTIONS,
  QUICK_SCENARIOS,
  RESOURCE_066,
  RESOURCE_OPTIONS,
  SUPPORTED_FILE_TYPES,
} from "./ingestionDemoConstants";
import "./ingestion-demo.css";

const DEFAULT_LEARNER = "demo-alex";
const DEFAULT_RESOURCE = RESOURCE_066;

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function friendlyMime(mime: string): string {
  const map: Record<string, string> = {
    "text/plain": "Plain text",
    "text/markdown": "Markdown",
    "application/pdf": "PDF",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "Word document",
    "application/octet-stream": "Document",
  };
  return map[mime] ?? "Document";
}

function friendlyExtractionMethod(method: string): string {
  const map: Record<string, string> = {
    MarkdownText: "Markdown document",
    PlainText: "Plain text file",
    PdfText: "PDF document",
    DocxText: "Word document",
  };
  return map[method] ?? method;
}

function friendlyStatus(status: string): string {
  const map: Record<string, string> = {
    Completed: "Ready",
    Failed: "Failed",
    Processing: "Processing",
    Pending: "Pending",
  };
  return map[status] ?? status;
}

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === "completed") return "ingestion-demo__status ingestion-demo__status--completed";
  if (s === "failed") return "ingestion-demo__status ingestion-demo__status--failed";
  if (s === "processing") return "ingestion-demo__status ingestion-demo__status--processing";
  return "ingestion-demo__status ingestion-demo__status--pending";
}

function friendlyError(message: string): string {
  const lower = message.toLowerCase();
  if (lower.includes("403") || lower.includes("forbidden") || lower.includes("not allowed")) {
    return "You do not have permission to add materials to this resource. It may be restricted to a specific course or group.";
  }
  if (lower.includes("ocr")) {
    return "This PDF appears to be scanned or image-only. Only documents with selectable text are supported.";
  }
  return message;
}

function isOcrNeeded(error: string | null | undefined): boolean {
  if (!error) return false;
  return error.toLowerCase().includes("ocr");
}

export default function ResourceIngestionDemoPage() {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [learnerId, setLearnerId] = useState(DEFAULT_LEARNER);
  const [resourceId, setResourceId] = useState(DEFAULT_RESOURCE);
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [files, setFiles] = useState<ResourceFileRecord[]>([]);
  const [extracted, setExtracted] = useState<ResourceExtractedTextRecord | null>(null);
  const [accessibleRow, setAccessibleRow] = useState<AccessibleLearningResource | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const selectedLearner = LEARNER_OPTIONS.find((l) => l.id === learnerId);
  const selectedResource = RESOURCE_OPTIONS.find((r) => r.id === resourceId);
  const hasCatalogAccess = accessibleRow != null;
  const canUpload = Boolean(hasCatalogAccess && file && !busy);

  const refresh = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const accessible = await getAccessibleResources(learnerId);
      const row = accessible.find((r) => r.id === resourceId) ?? null;
      setAccessibleRow(row);

      if (!row) {
        setFiles([]);
        setExtracted(null);
        return;
      }

      const [fileList, text] = await Promise.all([
        listResourceFiles(resourceId, learnerId),
        getResourceExtractedText(resourceId, learnerId),
      ]);
      setFiles(fileList);
      setExtracted(text);
    } catch (e) {
      setError(friendlyError(e instanceof Error ? e.message : "Could not load resource details."));
      setAccessibleRow(null);
      setFiles([]);
      setExtracted(null);
    } finally {
      setBusy(false);
    }
  }, [learnerId, resourceId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function onUpload(e: FormEvent) {
    e.preventDefault();
    if (!file || !hasCatalogAccess) return;
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await uploadResourceFile(resourceId, learnerId, file);
      if (result.processingStatus === "Completed") {
        setSuccess(`"${result.originalFileName}" was uploaded and processed successfully.`);
        setFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
      } else if (result.processingStatus === "Failed") {
        setError(friendlyError(result.processingError ?? "Text extraction failed."));
      } else {
        setSuccess(`File uploaded. Processing status: ${friendlyStatus(result.processingStatus)}.`);
      }
      await refresh();
    } catch (err) {
      setError(friendlyError(err instanceof Error ? err.message : "Upload failed."));
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(fileId: string) {
    if (!window.confirm("Remove this file and its extracted summary?")) return;
    setBusy(true);
    setError(null);
    try {
      await deleteResourceFile(resourceId, fileId, learnerId);
      setSuccess("File removed.");
      await refresh();
    } catch (err) {
      setError(friendlyError(err instanceof Error ? err.message : "Could not remove file."));
    } finally {
      setBusy(false);
    }
  }

  function applyScenario(scenarioId: string) {
    const scenario = QUICK_SCENARIOS.find((s) => s.id === scenarioId);
    if (!scenario) return;
    setLearnerId(scenario.learnerId);
    setResourceId(scenario.resourceId);
    setFile(null);
    setSuccess(null);
    setError(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  function onFileChosen(next: File | null) {
    setFile(next);
    setSuccess(null);
    setError(null);
  }

  const lastFailed = files.find((f) => f.processingStatus === "Failed");
  const isCourseOnly =
    accessibleRow?.visibility === "CourseOnly" || selectedResource?.accessLabel === "Course only";

  return (
    <AppLayout>
      <div className="ingestion-demo">
        <header className="ingestion-demo__header">
          <div>
            <h2 style={{ margin: 0 }}>Supplementary materials</h2>
            <p className="ingestion-demo__lead">
              Attach readings, notes, or worksheets to a learning resource. The system extracts text
              automatically so recommendations can use a short summary—only for learners who can access
              that resource.
            </p>
          </div>
        </header>

        {error ? <div className="ingestion-demo__alert ingestion-demo__alert--error">{error}</div> : null}
        {success ? <div className="ingestion-demo__alert ingestion-demo__alert--success">{success}</div> : null}

        <section className="ingestion-demo__card">
          <h3>1. Choose learner and resource</h3>
          <p className="ingestion-demo__muted">
            Select who is uploading and which learning resource the file belongs to.
          </p>

          <div className="ingestion-demo__quick-row">
            <span className="ingestion-demo__quick-label">Quick setup:</span>
            {QUICK_SCENARIOS.map((s) => (
              <button
                key={s.id}
                type="button"
                className="ingestion-demo__chip"
                onClick={() => applyScenario(s.id)}
              >
                {s.label}
              </button>
            ))}
          </div>

          <div className="ingestion-demo__grid-2 ingestion-demo__grid-2--spaced">
            <div className="ingestion-demo__field">
              <label htmlFor="learner-select">Learner</label>
              <select
                id="learner-select"
                className="ingestion-demo__select"
                value={learnerId}
                onChange={(e) => {
                  setLearnerId(e.target.value);
                  setFile(null);
                  if (fileInputRef.current) fileInputRef.current.value = "";
                }}
              >
                {LEARNER_OPTIONS.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name} — {l.description}
                  </option>
                ))}
              </select>
            </div>
            <div className="ingestion-demo__field">
              <label htmlFor="resource-select">Learning resource</label>
              <select
                id="resource-select"
                className="ingestion-demo__select"
                value={resourceId}
                onChange={(e) => {
                  setResourceId(e.target.value);
                  setFile(null);
                  if (fileInputRef.current) fileInputRef.current.value = "";
                }}
              >
                {RESOURCE_OPTIONS.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.title} ({r.accessLabel})
                  </option>
                ))}
              </select>
            </div>
          </div>

          {!hasCatalogAccess && !busy ? (
            <div className="ingestion-demo__notice ingestion-demo__notice--neutral">
              <strong>Not in your catalog.</strong>{" "}
              {selectedLearner?.name ?? "This learner"} cannot access &ldquo;
              {selectedResource?.title ?? "this resource"}&rdquo;, so uploads are not available. Try
              selecting a different learner or a resource they can open.
            </div>
          ) : null}

          {hasCatalogAccess && isCourseOnly ? (
            <div className="ingestion-demo__notice ingestion-demo__notice--info">
              <strong>Course-restricted resource.</strong> Only learners enrolled in this course will
              see the uploaded material and its summary in their catalog.
            </div>
          ) : null}
        </section>

        <section className="ingestion-demo__card">
          <h3>2. Upload a file</h3>
          <p className="ingestion-demo__muted">Supported formats: {SUPPORTED_FILE_TYPES}.</p>
          {selectedResource?.suggestedFile ? (
            <p className="ingestion-demo__hint">
              Suggested file for this resource: <strong>{selectedResource.suggestedFile}</strong>
            </p>
          ) : null}

          <form onSubmit={onUpload}>
            <div
              className={`ingestion-demo__dropzone${dragOver ? " ingestion-demo__dropzone--active" : ""}${!hasCatalogAccess ? " ingestion-demo__dropzone--disabled" : ""}`}
              onDragOver={(e) => {
                if (!hasCatalogAccess) return;
                e.preventDefault();
                setDragOver(true);
              }}
              onDragLeave={() => setDragOver(false)}
              onDrop={(e) => {
                if (!hasCatalogAccess) return;
                e.preventDefault();
                setDragOver(false);
                const dropped = e.dataTransfer.files?.[0];
                if (dropped) onFileChosen(dropped);
              }}
              onClick={() => {
                if (hasCatalogAccess) fileInputRef.current?.click();
              }}
              onKeyDown={(e) => {
                if (hasCatalogAccess && (e.key === "Enter" || e.key === " ")) {
                  e.preventDefault();
                  fileInputRef.current?.click();
                }
              }}
              role="button"
              tabIndex={hasCatalogAccess ? 0 : -1}
              aria-disabled={!hasCatalogAccess}
            >
              {hasCatalogAccess
                ? "Drag and drop a file here, or click to choose one."
                : "Upload is unavailable until the resource appears in the learner catalog."}
            </div>
            <input
              ref={fileInputRef}
              className="ingestion-demo__file-input-hidden"
              type="file"
              accept=".txt,.md,.markdown,.pdf,.docx"
              disabled={!hasCatalogAccess}
              onChange={(e) => onFileChosen(e.target.files?.[0] ?? null)}
            />
            <div className="ingestion-demo__file-row">
              <button
                type="button"
                className="ingestion-demo__btn ingestion-demo__btn--secondary"
                disabled={!hasCatalogAccess || busy}
                onClick={() => fileInputRef.current?.click()}
              >
                Choose file
              </button>
              {file ? (
                <span className="ingestion-demo__file-meta">
                  <strong>{file.name}</strong> · {formatBytes(file.size)}
                </span>
              ) : (
                <span className="ingestion-demo__file-meta">No file selected</span>
              )}
            </div>
            <div className="ingestion-demo__actions">
              <button
                type="submit"
                className="ingestion-demo__btn ingestion-demo__btn--primary"
                disabled={!canUpload}
              >
                {busy ? "Working…" : "Upload and extract text"}
              </button>
              <button
                type="button"
                className="ingestion-demo__btn ingestion-demo__btn--secondary"
                disabled={busy}
                onClick={() => void refresh()}
              >
                Refresh
              </button>
            </div>
          </form>
        </section>

        <section className="ingestion-demo__card">
          <h3>3. Uploaded files</h3>
          {files.length === 0 ? (
            <p className="ingestion-demo__empty">No supplementary files yet for this resource.</p>
          ) : (
            <table className="ingestion-demo__table">
              <thead>
                <tr>
                  <th>File name</th>
                  <th>Format</th>
                  <th>Size</th>
                  <th>Status</th>
                  <th>Processed</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {files.map((f) => (
                  <tr key={f.id}>
                    <td>{f.originalFileName}</td>
                    <td>{friendlyMime(f.mimeType)}</td>
                    <td>{formatBytes(f.sizeBytes)}</td>
                    <td>
                      <span className={statusClass(f.processingStatus)}>
                        {friendlyStatus(f.processingStatus)}
                      </span>
                      {f.processingError ? (
                        <div className="ingestion-demo__muted ingestion-demo__cell-note">
                          {friendlyError(f.processingError)}
                        </div>
                      ) : null}
                    </td>
                    <td>{f.processedAtUtc ? formatUtcDateTime(f.processedAtUtc) : "—"}</td>
                    <td>
                      <button
                        type="button"
                        className="ingestion-demo__btn--danger"
                        disabled={busy || !hasCatalogAccess}
                        onClick={() => void onDelete(f.id)}
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <section className="ingestion-demo__card">
          <h3>4. Extracted summary</h3>
          {extracted?.summary ? (
            <>
              <dl className="ingestion-demo__meta-grid ingestion-demo__meta-grid--compact">
                <dt>Source</dt>
                <dd>{friendlyExtractionMethod(extracted.extractionMethod)}</dd>
                <dt>Updated</dt>
                <dd>{formatUtcDateTime(extracted.createdAtUtc)}</dd>
              </dl>
              <div className="ingestion-demo__preview">{extracted.summary}</div>
            </>
          ) : lastFailed && isOcrNeeded(lastFailed.processingError) ? (
            <p className="ingestion-demo__empty">
              Text could not be extracted from the last upload. Scanned PDFs are not supported—use a
              document with selectable text.
            </p>
          ) : (
            <p className="ingestion-demo__empty">
              No summary yet. Upload a supported file to generate one automatically.
            </p>
          )}
        </section>

        {hasCatalogAccess ? (
          <section className="ingestion-demo__card">
            <h3>5. Catalog entry</h3>
            <p className="ingestion-demo__muted">
              This is how the resource appears in {selectedLearner?.name ?? "the learner"}&apos;s
              accessible catalog after upload.
            </p>
            <dl className="ingestion-demo__meta-grid">
              <dt>Title</dt>
              <dd>{accessibleRow.title}</dd>
              <dt>Topic</dt>
              <dd>{accessibleRow.topic}</dd>
              <dt>Access</dt>
              <dd>{accessibleRow.visibility ?? selectedResource?.accessLabel ?? "—"}</dd>
              <dt>Supplementary file</dt>
              <dd>{accessibleRow.hasSupplementaryFile ? "Yes" : "No"}</dd>
              <dt>Summary for recommendations</dt>
              <dd>{accessibleRow.extractedTextSummary ? "Available" : "Not yet available"}</dd>
            </dl>
            {accessibleRow.extractedTextSummary ? (
              <p className="ingestion-demo__hint">
                The recommendation engine uses the title, topic, description, and this summary—never
                the full file—for learners who can access this resource.
              </p>
            ) : null}
          </section>
        ) : null}
      </div>
    </AppLayout>
  );
}
