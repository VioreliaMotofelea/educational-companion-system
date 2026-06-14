import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent } from "react";
import AppLayout from "../components/layout/AppLayout";
import { useCurrentUser } from "../hooks/useCurrentUser";
import {
  deleteResourceFile,
  getAccessibleResources,
  getResourceExtractedText,
  listResourceFiles,
  uploadResourceFile,
  type AccessibleLearningResource,
  type ResourceExtractedTextRecord,
  type ResourceFileRecord,
} from "../services/api";
import { formatUtcDateTime } from "../utils/formatDate";
import {
  PREFERRED_RESOURCE_ID,
  SUGGESTED_FILE_BY_RESOURCE,
  SUPPORTED_FILE_TYPES,
} from "./ingestionDemoConstants";
import "./ingestion-demo.css";

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

function friendlyVisibility(value?: string | null): string {
  const map: Record<string, string> = {
    Global: "Open catalog",
    CourseOnly: "Course only",
    GroupOnly: "Group only",
    PrivateToUser: "Private",
  };
  if (!value) return "—";
  return map[value] ?? value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function resourcesForSupplementaryUpload(
  catalog: AccessibleLearningResource[],
): AccessibleLearningResource[] {
  const eligible = new Map<string, AccessibleLearningResource>();
  for (const resource of catalog) {
    const visibility = resource.visibility ?? "Global";
    const restricted = visibility === "CourseOnly" || visibility === "GroupOnly";
    if (restricted || resource.hasSupplementaryFile) {
      eligible.set(resource.id, resource);
    }
  }
  return Array.from(eligible.values()).sort((a, b) => {
    const rank = (r: AccessibleLearningResource) => {
      if (r.visibility === "CourseOnly") return 0;
      if (r.visibility === "GroupOnly") return 1;
      return 2;
    };
    const diff = rank(a) - rank(b);
    return diff !== 0 ? diff : a.title.localeCompare(b.title);
  });
}

function friendlyError(message: string): string {
  const lower = message.toLowerCase();
  if (lower.includes("403") || lower.includes("forbidden") || lower.includes("not allowed")) {
    return "You do not have permission to add materials to this resource.";
  }
  if (lower.includes("ocr")) {
    return "This PDF appears to be scanned or image-only. Only documents with selectable text are supported.";
  }
  return message;
}

const SUMMARY_PREVIEW_CHARS = 420;

function plainSummaryPreview(text: string): string {
  return text
    .replace(/^#{1,6}\s+/gm, "")
    .replace(/\*\*([^*]+)\*\*/g, "$1")
    .replace(/\*([^*]+)\*/g, "$1")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/^---\s*$/gm, "")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function truncatedSummary(text: string, expanded: boolean): string {
  const plain = plainSummaryPreview(text);
  if (expanded || plain.length <= SUMMARY_PREVIEW_CHARS) return plain;
  const cut = plain.slice(0, SUMMARY_PREVIEW_CHARS);
  const lastSpace = cut.lastIndexOf(" ");
  return `${(lastSpace > 280 ? cut.slice(0, lastSpace) : cut).trim()}…`;
}

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === "completed") return "ingestion-demo__status ingestion-demo__status--completed";
  if (s === "failed") return "ingestion-demo__status ingestion-demo__status--failed";
  if (s === "processing") return "ingestion-demo__status ingestion-demo__status--processing";
  return "ingestion-demo__status ingestion-demo__status--pending";
}

function isOcrNeeded(error: string | null | undefined): boolean {
  if (!error) return false;
  return error.toLowerCase().includes("ocr");
}

function pickDefaultResourceId(catalog: AccessibleLearningResource[]): string {
  if (catalog.length === 0) return "";
  const preferred = catalog.find((r) => r.id === PREFERRED_RESOURCE_ID);
  return preferred?.id ?? catalog[0].id;
}

function resourceOptionLabel(resource: AccessibleLearningResource): string {
  const access = friendlyVisibility(resource.visibility);
  return `${resource.title} · ${resource.topic} (${access})`;
}

export default function ResourceIngestionDemoPage() {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { userId } = useCurrentUser();

  const [catalog, setCatalog] = useState<AccessibleLearningResource[]>([]);
  const [resourceId, setResourceId] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [files, setFiles] = useState<ResourceFileRecord[]>([]);
  const [extracted, setExtracted] = useState<ResourceExtractedTextRecord | null>(null);
  const [summaryExpanded, setSummaryExpanded] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const uploadTargets = useMemo(() => resourcesForSupplementaryUpload(catalog), [catalog]);
  const selectedResource = uploadTargets.find((r) => r.id === resourceId) ?? null;
  const canUpload = Boolean(userId && resourceId && file && !busy);
  const suggestedFile = resourceId ? SUGGESTED_FILE_BY_RESOURCE[resourceId] : undefined;
  const summaryPlain = extracted?.summary ? plainSummaryPreview(extracted.summary) : "";
  const summaryIsLong = summaryPlain.length > SUMMARY_PREVIEW_CHARS;

  useEffect(() => {
    setSummaryExpanded(false);
  }, [resourceId]);

  const loadCatalog = useCallback(async () => {
    if (!userId) return;
    setBusy(true);
    setError(null);
    try {
      const accessible = await getAccessibleResources(userId);
      setCatalog(accessible);
      const targets = resourcesForSupplementaryUpload(accessible);
      setResourceId((current) => {
        if (current && targets.some((r) => r.id === current)) return current;
        return pickDefaultResourceId(targets);
      });
    } catch (e) {
      setCatalog([]);
      setResourceId("");
      setError(friendlyError(e instanceof Error ? e.message : "Could not load your learning catalog."));
    } finally {
      setBusy(false);
    }
  }, [userId]);

  const refreshResourceDetails = useCallback(async () => {
    if (!userId || !resourceId) {
      setFiles([]);
      setExtracted(null);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const [fileList, text] = await Promise.all([
        listResourceFiles(resourceId, userId),
        getResourceExtractedText(resourceId, userId),
      ]);
      setFiles(fileList);
      setExtracted(text);
    } catch (e) {
      setFiles([]);
      setExtracted(null);
      setError(friendlyError(e instanceof Error ? e.message : "Could not load resource details."));
    } finally {
      setBusy(false);
    }
  }, [userId, resourceId]);

  useEffect(() => {
    void loadCatalog();
  }, [loadCatalog]);

  useEffect(() => {
    void refreshResourceDetails();
  }, [refreshResourceDetails]);

  async function onUpload(e: FormEvent) {
    e.preventDefault();
    if (!file || !userId || !resourceId) return;
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await uploadResourceFile(resourceId, userId, file);
      if (result.processingStatus === "Completed") {
        setSuccess(`"${result.originalFileName}" was uploaded and processed successfully.`);
        setFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
      } else if (result.processingStatus === "Failed") {
        setError(friendlyError(result.processingError ?? "Text extraction failed."));
      } else {
        setSuccess(`File uploaded. Status: ${friendlyStatus(result.processingStatus)}.`);
      }
      await loadCatalog();
      await refreshResourceDetails();
    } catch (err) {
      setError(friendlyError(err instanceof Error ? err.message : "Upload failed."));
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(fileId: string) {
    if (!userId || !resourceId) return;
    if (!window.confirm("Remove this file and its extracted summary?")) return;
    setBusy(true);
    setError(null);
    try {
      await deleteResourceFile(resourceId, fileId, userId);
      setSuccess("File removed.");
      await loadCatalog();
      await refreshResourceDetails();
    } catch (err) {
      setError(friendlyError(err instanceof Error ? err.message : "Could not remove file."));
    } finally {
      setBusy(false);
    }
  }

  function onFileChosen(next: File | null) {
    setFile(next);
    setSuccess(null);
    setError(null);
  }

  const lastFailed = files.find((f) => f.processingStatus === "Failed");
  const isCourseOnly = selectedResource?.visibility === "CourseOnly";

  return (
    <AppLayout>
      <div className="ingestion-demo">
        <header className="ingestion-demo__header">
          <h2 style={{ margin: 0 }}>Supplementary materials</h2>
          <p className="ingestion-demo__lead">
            Attach files to course or group materials. Access follows the selected item; summaries
            feed recommendations.
          </p>
        </header>

        {error ? <div className="ingestion-demo__alert ingestion-demo__alert--error">{error}</div> : null}
        {success ? <div className="ingestion-demo__alert ingestion-demo__alert--success">{success}</div> : null}

        {uploadTargets.length === 0 && !busy ? (
          <section className="ingestion-demo__card">
            <p className="ingestion-demo__empty">
              {catalog.length === 0
                ? "No learning resources are available in your catalog yet. Complete a few activities or ask your instructor to grant access, then return here."
                : "No course or group materials are available for you yet. Course readings (for example Week 3 — Normalization) appear here once you are enrolled. Open catalog items use external links and are not listed on this page."}
            </p>
          </section>
        ) : (
          <div className="ingestion-demo__stack">
            <section className="ingestion-demo__card">
              <div className="ingestion-demo__field">
                <label htmlFor="resource-select">Material</label>
                <select
                  id="resource-select"
                  className="ingestion-demo__select"
                  value={resourceId}
                  disabled={busy || uploadTargets.length === 0}
                  onChange={(e) => {
                    setResourceId(e.target.value);
                    setFile(null);
                    if (fileInputRef.current) fileInputRef.current.value = "";
                  }}
                >
                  {uploadTargets.map((r) => (
                    <option key={r.id} value={r.id}>
                      {resourceOptionLabel(r)}
                    </option>
                  ))}
                </select>
              </div>

              {isCourseOnly ? (
                <p className="ingestion-demo__inline-note">
                  Course only — visible to enrolled learners in this course.
                </p>
              ) : null}

              <form onSubmit={onUpload} className="ingestion-demo__upload-form">
                <div
                  className={`ingestion-demo__dropzone${dragOver ? " ingestion-demo__dropzone--active" : ""}`}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setDragOver(true);
                  }}
                  onDragLeave={() => setDragOver(false)}
                  onDrop={(e) => {
                    e.preventDefault();
                    setDragOver(false);
                    const dropped = e.dataTransfer.files?.[0];
                    if (dropped) onFileChosen(dropped);
                  }}
                  onClick={() => fileInputRef.current?.click()}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      fileInputRef.current?.click();
                    }
                  }}
                  role="button"
                  tabIndex={0}
                >
                  Drag and drop a file here, or click to choose one.
                  <span className="ingestion-demo__dropzone-hint">
                    Supported formats: {SUPPORTED_FILE_TYPES}
                  </span>
                </div>
                <input
                  ref={fileInputRef}
                  className="ingestion-demo__file-input-hidden"
                  type="file"
                  accept=".txt,.md,.markdown,.pdf,.docx"
                  onChange={(e) => onFileChosen(e.target.files?.[0] ?? null)}
                />
                <div className="ingestion-demo__file-row">
                  <button
                    type="button"
                    className="ingestion-demo__btn ingestion-demo__btn--secondary"
                    disabled={busy}
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
                    onClick={() => void refreshResourceDetails()}
                  >
                    Refresh
                  </button>
                </div>
              </form>
            </section>

            {files.length > 0 ? (
              <section className="ingestion-demo__card">
                <h3>Uploaded files</h3>
                <div className="ingestion-demo__table-wrap">
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
                              disabled={busy}
                              onClick={() => void onDelete(f.id)}
                            >
                              Remove
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            ) : null}

            {extracted?.summary || (lastFailed && isOcrNeeded(lastFailed.processingError)) ? (
              <section className="ingestion-demo__card">
                <div className="ingestion-demo__card-head">
                  <h3>Extracted summary</h3>
                  {extracted?.summary && selectedResource?.extractedTextSummary ? (
                    <span className="ingestion-demo__badge">Used by recommendations</span>
                  ) : null}
                </div>
                {extracted?.summary ? (
                  <>
                    <p className="ingestion-demo__muted ingestion-demo__summary-meta">
                      {friendlyExtractionMethod(extracted.extractionMethod)} · Updated{" "}
                      {formatUtcDateTime(extracted.createdAtUtc)}
                    </p>
                    <div
                      className={`ingestion-demo__preview${summaryExpanded ? " ingestion-demo__preview--expanded" : ""}`}
                    >
                      {truncatedSummary(extracted.summary, summaryExpanded)}
                    </div>
                    {summaryIsLong ? (
                      <button
                        type="button"
                        className="ingestion-demo__link-btn"
                        onClick={() => setSummaryExpanded((v) => !v)}
                      >
                        {summaryExpanded ? "Show less" : "Show full summary"}
                      </button>
                    ) : null}
                  </>
                ) : (
                  <p className="ingestion-demo__empty">
                    Text could not be extracted from the last upload. Use a document with selectable
                    text instead of a scanned PDF.
                  </p>
                )}
              </section>
            ) : null}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
