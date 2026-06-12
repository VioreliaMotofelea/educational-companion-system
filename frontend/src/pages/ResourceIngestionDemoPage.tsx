import { useCallback, useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import AppLayout from "../components/layout/AppLayout";
import { getStoredAuthSession } from "../services/authStorage";
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
  DEMO_PRESETS,
  RESOURCE_066,
  SAMPLE_FILES,
  SUPPORTED_EXTENSIONS,
} from "./ingestionDemoConstants";
import "./ingestion-demo.css";

const DEFAULT_LEARNER = "demo-alex";
const DEFAULT_RESOURCE = RESOURCE_066;

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
  const lower = error.toLowerCase();
  return lower.includes("ocr");
}

export default function ResourceIngestionDemoPage() {
  const [learnerId, setLearnerId] = useState(DEFAULT_LEARNER);
  const [resourceId, setResourceId] = useState(DEFAULT_RESOURCE);
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [files, setFiles] = useState<ResourceFileRecord[]>([]);
  const [extracted, setExtracted] = useState<ResourceExtractedTextRecord | null>(null);
  const [accessibleRow, setAccessibleRow] = useState<AccessibleLearningResource | null>(null);
  const [biancaHas066, setBiancaHas066] = useState<boolean | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const session = getStoredAuthSession();
  const learnerTrimmed = learnerId.trim();
  const identityMismatch = session != null && session.userId !== learnerTrimmed;
  const canUpload = Boolean(learnerTrimmed && resourceId.trim() && file && !busy);

  const refresh = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const [fileList, text, accessible] = await Promise.all([
        listResourceFiles(resourceId, learnerId),
        getResourceExtractedText(resourceId, learnerId),
        getAccessibleResources(learnerId),
      ]);
      setFiles(fileList);
      setExtracted(text);
      setAccessibleRow(accessible.find((r) => r.id === resourceId) ?? null);

      if (resourceId === RESOURCE_066) {
        const biancaCatalog = await getAccessibleResources("demo-bianca");
        setBiancaHas066(biancaCatalog.some((r) => r.id === RESOURCE_066));
      } else {
        setBiancaHas066(null);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Refresh failed");
      setAccessibleRow(null);
    } finally {
      setBusy(false);
    }
  }, [learnerId, resourceId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function onUpload(e: FormEvent) {
    e.preventDefault();
    if (!file) return;
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await uploadResourceFile(resourceId, learnerId, file);
      if (result.processingStatus === "Completed") {
        setSuccess(`File processed successfully (${result.originalFileName}).`);
      } else if (result.processingStatus === "Failed") {
        setError(result.processingError ?? "Extraction failed.");
      } else {
        setSuccess(`Upload accepted. Status: ${result.processingStatus}`);
      }
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(fileId: string) {
    if (!window.confirm("Delete this file and its extracted text?")) return;
    setBusy(true);
    setError(null);
    try {
      await deleteResourceFile(resourceId, fileId, learnerId);
      setSuccess("File removed.");
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setBusy(false);
    }
  }

  function applyPreset(presetId: string) {
    const preset = DEMO_PRESETS.find((p) => p.id === presetId);
    if (!preset) return;
    setLearnerId(preset.learnerId);
    setResourceId(preset.resourceId);
    setFile(null);
  }

  function onFileChosen(next: File | null) {
    setFile(next);
    setSuccess(null);
    setError(null);
  }

  const semanticPreview = useMemo(() => {
    if (!accessibleRow) return [];
    const lines: { label: string; value: string }[] = [];
    lines.push({ label: "Title", value: accessibleRow.title });
    lines.push({ label: "Topic", value: accessibleRow.topic });
    if (accessibleRow.description?.trim()) {
      lines.push({ label: "Description", value: accessibleRow.description });
    }
    if (accessibleRow.extractedTextSummary?.trim()) {
      lines.push({
        label: "Summary (AI enrichment)",
        value: `${accessibleRow.extractedTextSummary.slice(0, 160)}${accessibleRow.extractedTextSummary.length > 160 ? "…" : ""}`,
      });
    }
    return lines;
  }, [accessibleRow]);

  const lastFailed = files.find((f) => f.processingStatus === "Failed");

  return (
    <AppLayout>
      <div className="ingestion-demo">
        <header className="ingestion-demo__header">
          <div>
            <h2 style={{ margin: 0 }}>Resource ingestion demo</h2>
            <p className="ingestion-demo__muted" style={{ marginTop: 8, maxWidth: 640 }}>
              Upload a supported learning material, extract text, and verify that the extracted summary
              enriches only the accessible catalog entry for the selected learner.
            </p>
          </div>
          <span className="ingestion-demo__badge">Access-aware extraction</span>
        </header>

        <section className="ingestion-demo__card">
          <h3>How it works</h3>
          <ol className="ingestion-demo__steps">
            <li>Select learner and resource (use presets for Alex vs Bianca).</li>
            <li>Upload a supported file — TXT, Markdown, DOCX, or selectable-text PDF.</li>
            <li>
              Extracted summary appears only on <code>GET /users/…/resources/accessible</code> for that
              learner.
            </li>
          </ol>
          <p className="ingestion-demo__muted">
            Scanned or image-only PDFs are not supported (OCR is documented as future work). Sample files
            live in <code>datasets/demo/resource-files/</code>.
          </p>
        </section>

        {identityMismatch ? (
          <div className="ingestion-demo__alert ingestion-demo__alert--warn">
            <strong>Login ID ≠ demo learner ID.</strong> You are signed in as{" "}
            <code>{session?.userId}</code> but the form targets <code>{learnerTrimmed}</code>. API calls
            omit your JWT so <code>demo-alex</code> demos still work — same as the curl script.
          </div>
        ) : null}

        {error ? <div className="ingestion-demo__alert ingestion-demo__alert--error">{error}</div> : null}
        {success ? <div className="ingestion-demo__alert ingestion-demo__alert--success">{success}</div> : null}

        <section className="ingestion-demo__card">
          <h3>Access context</h3>
          <div className="ingestion-demo__grid-2">
            <div className="ingestion-demo__field">
              <label htmlFor="learner-id">Learner ID</label>
              <input
                id="learner-id"
                className="ingestion-demo__input"
                value={learnerId}
                onChange={(e) => setLearnerId(e.target.value)}
              />
            </div>
            <div className="ingestion-demo__field">
              <label htmlFor="resource-id">Resource ID</label>
              <input
                id="resource-id"
                className="ingestion-demo__input"
                value={resourceId}
                onChange={(e) => setResourceId(e.target.value)}
              />
            </div>
          </div>
          <div className="ingestion-demo__presets">
            {DEMO_PRESETS.map((p) => (
              <button
                key={p.id}
                type="button"
                className="ingestion-demo__preset-btn"
                title={p.hint}
                onClick={() => applyPreset(p.id)}
              >
                {p.label}
              </button>
            ))}
          </div>
          <p className="ingestion-demo__muted">
            <strong>demo-alex</strong> has course access to resource 066. <strong>demo-bianca</strong> should
            not see or upload to that CourseOnly resource.
          </p>
        </section>

        <section className="ingestion-demo__card">
          <h3>Upload material</h3>
          <p className="ingestion-demo__muted">Supported: {SUPPORTED_EXTENSIONS}</p>
          <form onSubmit={onUpload}>
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
            >
              Drop a file here or use the picker below.
            </div>
            <input
              className="ingestion-demo__file-input"
              type="file"
              accept=".txt,.md,.markdown,.pdf,.docx"
              onChange={(e) => onFileChosen(e.target.files?.[0] ?? null)}
            />
            {file ? (
              <p className="ingestion-demo__file-meta">
                Selected: <strong>{file.name}</strong> ({formatBytes(file.size)}, {file.type || "unknown"})
              </p>
            ) : (
              <p className="ingestion-demo__file-meta">No file selected.</p>
            )}
            <div className="ingestion-demo__actions">
              <button type="submit" className="ingestion-demo__btn ingestion-demo__btn--primary" disabled={!canUpload}>
                {busy ? "Working…" : "Upload and extract"}
              </button>
              <button
                type="button"
                className="ingestion-demo__btn ingestion-demo__btn--secondary"
                disabled={busy}
                onClick={() => void refresh()}
              >
                Refresh status
              </button>
            </div>
          </form>
          <details style={{ marginTop: 12 }}>
            <summary className="ingestion-demo__muted" style={{ cursor: "pointer" }}>
              Sample files in repo
            </summary>
            <ul className="ingestion-demo__muted" style={{ margin: "8px 0 0", paddingLeft: 20 }}>
              {SAMPLE_FILES.map((s) => (
                <li key={s.name}>
                  <code>{s.name}</code> — {s.type}
                </li>
              ))}
            </ul>
          </details>
        </section>

        <section className="ingestion-demo__card">
          <h3>Uploaded files</h3>
          {files.length === 0 ? (
            <p className="ingestion-demo__empty">No supplementary files uploaded yet.</p>
          ) : (
            <table className="ingestion-demo__table">
              <thead>
                <tr>
                  <th>File</th>
                  <th>Type</th>
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
                    <td>{f.mimeType}</td>
                    <td>{formatBytes(f.sizeBytes)}</td>
                    <td>
                      <span className={statusClass(f.processingStatus)}>{f.processingStatus}</span>
                      {f.processingError ? (
                        <div className="ingestion-demo__muted" style={{ marginTop: 4, maxWidth: 280 }}>
                          {f.processingError}
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
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <section className="ingestion-demo__card">
          <h3>Summary preview</h3>
          {extracted?.summary ? (
            <>
              <dl className="ingestion-demo__meta-grid" style={{ marginBottom: 12 }}>
                <dt>Method</dt>
                <dd>{extracted.extractionMethod}</dd>
                <dt>Characters</dt>
                <dd>{extracted.characterCount}</dd>
                <dt>Created</dt>
                <dd>{formatUtcDateTime(extracted.createdAtUtc)}</dd>
              </dl>
              <div className="ingestion-demo__preview">{extracted.summary}</div>
            </>
          ) : lastFailed && isOcrNeeded(lastFailed.processingError) ? (
            <p className="ingestion-demo__empty">
              This file appears to require OCR. OCR is documented as future work and is not part of the
              current MVP.
            </p>
          ) : (
            <p className="ingestion-demo__empty">
              No extracted summary is available yet. Upload a supported file or refresh after processing.
            </p>
          )}
        </section>

        <section className="ingestion-demo__card">
          <h3>Accessible catalog verification</h3>
          {!accessibleRow ? (
            <div className="ingestion-demo__alert ingestion-demo__alert--warn">
              This learner cannot access the selected resource. File operations and extracted summary
              should be blocked by the backend.
            </div>
          ) : (
            <dl className="ingestion-demo__meta-grid">
              <dt>Title</dt>
              <dd>{accessibleRow.title}</dd>
              <dt>Topic</dt>
              <dd>{accessibleRow.topic}</dd>
              <dt>Visibility</dt>
              <dd>{accessibleRow.visibility ?? "—"}</dd>
              <dt>Has supplementary file</dt>
              <dd>{accessibleRow.hasSupplementaryFile ? "Yes" : "No"}</dd>
              <dt>Summary length</dt>
              <dd>{accessibleRow.extractedTextSummary?.length ?? 0}</dd>
              <dt>Summary for AI</dt>
              <dd>{accessibleRow.extractedTextSummary ? "Yes" : "No"}</dd>
            </dl>
          )}

          {resourceId === RESOURCE_066 && biancaHas066 !== null ? (
            <div
              className={`ingestion-demo__alert ${biancaHas066 ? "ingestion-demo__alert--error" : "ingestion-demo__alert--success"}`}
              style={{ marginTop: 12 }}
            >
              Privacy check (resource 066): demo-bianca catalog{" "}
              {biancaHas066 ? "includes" : "does not include"} this CourseOnly resource
              {biancaHas066 ? " — unexpected!" : " — expected."}
            </div>
          ) : null}
        </section>

        {semanticPreview.length > 0 ? (
          <section className="ingestion-demo__card">
            <h3>AI semantic text contribution</h3>
            <p className="ingestion-demo__muted">
              The recommendation service builds labeled text from metadata plus optional summary (not full
              extracted text).
            </p>
            <ul className="ingestion-demo__semantic-lines">
              {semanticPreview.map((line) => (
                <li key={line.label}>
                  <strong>{line.label}:</strong> {line.value}
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <p className="ingestion-demo__muted">
          Full curl walkthrough: <code>docs/demo/resource-ingestion-demo.md</code>
        </p>
      </div>
    </AppLayout>
  );
}
