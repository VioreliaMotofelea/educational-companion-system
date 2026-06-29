import { useEffect, useState } from "react";
import { createInteraction } from "../../services/api";
import { INTERACTION_UPDATED_EVENT } from "../../hooks/useRecommendations";
import { humanizeResourceTitle } from "../../utils/recommendationUtils";

export type ResourceStudyPanelProps = {
  userId: string;
  resourceId: string;
  title: string;
  durationMinutes: number;
  url?: string | null;
  sourceName?: string | null;
  accessInstructions?: string | null;
  /** recommendation = default buttons; task/plan = explains linked task flow */
  context?: "recommendation" | "task" | "plan";
  defaultControlsOpen?: boolean;
  /** When linked to a study task, sync panel state on reopen */
  linkedTaskStatus?: "Pending" | "Overdue" | "Completed" | null;
  /** Hide helper paragraphs (e.g. on Calendar) */
  compact?: boolean;
};

function isOpenableHttpUrl(url: string | null | undefined): url is string {
  if (!url?.trim()) return false;
  try {
    const parsed = new URL(url.trim());
    return parsed.protocol === "http:" || parsed.protocol === "https:";
  } catch {
    return false;
  }
}

function formatDuration(seconds: number) {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}m ${String(secs).padStart(2, "0")}s`;
}

const ACTIVE_SESSION_KEY_PREFIX = "ecs.activeSession.";
const STUDY_CONTROLS_OPEN_KEY_PREFIX = "ecs.studyControlsOpen.";

function studyControlsStorageKey(userId: string, resourceId: string) {
  return `${STUDY_CONTROLS_OPEN_KEY_PREFIX}${userId}.${resourceId}`;
}

function readStudyControlsOpen(userId: string, resourceId: string) {
  try {
    return window.sessionStorage.getItem(studyControlsStorageKey(userId, resourceId)) === "1";
  } catch {
    return false;
  }
}

function writeStudyControlsOpen(userId: string, resourceId: string, open: boolean) {
  try {
    const key = studyControlsStorageKey(userId, resourceId);
    if (open) {
      window.sessionStorage.setItem(key, "1");
    } else {
      window.sessionStorage.removeItem(key);
    }
  } catch {
    // ignore storage errors
  }
}

type ActiveSession = {
  userId: string;
  resourceId: string;
  resourceTitle?: string;
  startedAtMs: number;
};

function getActiveSession(userId: string): ActiveSession | null {
  try {
    const raw = window.localStorage.getItem(`${ACTIVE_SESSION_KEY_PREFIX}${userId}`);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as ActiveSession;
    if (!parsed || parsed.userId !== userId || !parsed.resourceId || !parsed.startedAtMs) return null;
    return parsed;
  } catch {
    return null;
  }
}

function setActiveSession(userId: string, session: ActiveSession | null) {
  const key = `${ACTIVE_SESSION_KEY_PREFIX}${userId}`;
  if (session) {
    window.localStorage.setItem(key, JSON.stringify(session));
  } else {
    window.localStorage.removeItem(key);
  }
  window.dispatchEvent(new CustomEvent("active-session-changed"));
}

function notifyInteractionUpdated() {
  window.dispatchEvent(new CustomEvent(INTERACTION_UPDATED_EVENT));
  window.dispatchEvent(new Event("task-updated"));
}

export default function ResourceStudyPanel({
  userId,
  resourceId,
  title,
  durationMinutes,
  url,
  sourceName,
  accessInstructions,
  context = "recommendation",
  defaultControlsOpen = false,
  linkedTaskStatus = null,
  compact = false,
}: ResourceStudyPanelProps) {
  const displayTitle = humanizeResourceTitle(title);
  const [isStarting, setIsStarting] = useState(false);
  const [isCompleting, setIsCompleting] = useState(false);
  const [isRating, setIsRating] = useState(false);
  const [isOpen, setIsOpenState] = useState(
    () => defaultControlsOpen || readStudyControlsOpen(userId, resourceId),
  );
  const setIsOpen = (value: boolean | ((prev: boolean) => boolean)) => {
    setIsOpenState((prev) => {
      const next = typeof value === "function" ? value(prev) : value;
      writeStudyControlsOpen(userId, resourceId, next);
      return next;
    });
  };
  const [hasStarted, setHasStarted] = useState(false);
  const [hasCompleted, setHasCompleted] = useState(false);
  const [timeSpentMinutes, setTimeSpentMinutes] = useState<number>(Math.max(1, durationMinutes));
  const [sessionStartedAtMs, setSessionStartedAtMs] = useState<number | null>(null);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [ratingValue, setRatingValue] = useState<number>(4);
  const [startBlockedByActiveSession, setStartBlockedByActiveSession] = useState(false);
  const [activeSessionResourceId, setActiveSessionResourceId] = useState<string | null>(null);
  const [activeSessionResourceTitle, setActiveSessionResourceTitle] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const openUrl = isOpenableHttpUrl(url) ? url.trim() : null;
  const trimmedInstructions = accessInstructions?.trim() || null;
  const trimmedSource = sourceName?.trim() || null;
  const linkedTaskContext = context === "task" || context === "plan";

  useEffect(() => {
    if (linkedTaskStatus !== "Pending" && linkedTaskStatus !== "Overdue") return;

    setHasCompleted(false);
    setOk(null);
    const active = getActiveSession(userId);
    if (!active || active.resourceId !== resourceId) {
      setHasStarted(false);
      setSessionStartedAtMs(null);
      setElapsedSeconds(0);
      setTimeSpentMinutes(Math.max(1, durationMinutes));
    }
  }, [linkedTaskStatus, userId, resourceId, durationMinutes]);

  useEffect(() => {
    const syncFromActiveSession = () => {
      const active = getActiveSession(userId);
      if (!active) {
        setActiveSessionResourceId(null);
        setActiveSessionResourceTitle(null);
        if (!hasCompleted) {
          setHasStarted(false);
          setSessionStartedAtMs(null);
          setElapsedSeconds(0);
          setStartBlockedByActiveSession(false);
        }
        return;
      }

      setActiveSessionResourceId(active.resourceId);
      setActiveSessionResourceTitle(active.resourceTitle ?? null);

      if (!active.resourceTitle && active.resourceId === resourceId) {
        setActiveSession(userId, {
          ...active,
          resourceTitle: displayTitle,
        });
      }

      if (active.resourceId === resourceId && !hasCompleted) {
        setHasStarted(true);
        setSessionStartedAtMs(active.startedAtMs);
      }
    };

    syncFromActiveSession();
    window.addEventListener("active-session-changed", syncFromActiveSession);
    window.addEventListener("storage", syncFromActiveSession);
    return () => {
      window.removeEventListener("active-session-changed", syncFromActiveSession);
      window.removeEventListener("storage", syncFromActiveSession);
    };
  }, [userId, resourceId, hasCompleted, displayTitle]);

  useEffect(() => {
    if (!sessionStartedAtMs || hasCompleted) return;

    const tick = () => {
      const deltaSeconds = Math.max(0, Math.floor((Date.now() - sessionStartedAtMs) / 1000));
      setElapsedSeconds(deltaSeconds);
      setTimeSpentMinutes(Math.max(1, Math.ceil(deltaSeconds / 60)));
    };

    tick();
    const timerId = window.setInterval(tick, 1000);
    return () => window.clearInterval(timerId);
  }, [sessionStartedAtMs, hasCompleted]);

  const handleStart = async () => {
    setError(null);
    setOk(null);
    setStartBlockedByActiveSession(false);

    const active = getActiveSession(userId);
    if (active && active.resourceId !== resourceId) {
      setStartBlockedByActiveSession(true);
      setActiveSessionResourceId(active.resourceId);
      setActiveSessionResourceTitle(active.resourceTitle ?? null);
      return;
    }

    setIsStarting(true);

    try {
      await createInteraction({
        userId,
        learningResourceId: resourceId,
        interactionType: "Viewed",
        timeSpentMinutes: 1,
      });
      setHasStarted(true);
      setHasCompleted(false);
      setIsOpen(true);
      const startedAt = Date.now();
      setSessionStartedAtMs(startedAt);
      setElapsedSeconds(0);
      setTimeSpentMinutes(1);
      setActiveSession(userId, {
        userId,
        resourceId,
        resourceTitle: displayTitle,
        startedAtMs: startedAt,
      });
      setOk("Session started. Mark completed when you finish to update your task and recommender.");
      notifyInteractionUpdated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not start resource.");
    } finally {
      setIsStarting(false);
    }
  };

  const handleComplete = async () => {
    setError(null);
    setOk(null);
    setIsCompleting(true);

    try {
      const trackedMinutes = Math.max(1, Math.max(Math.round(timeSpentMinutes), Math.ceil(elapsedSeconds / 60)));
      await createInteraction({
        userId,
        learningResourceId: resourceId,
        interactionType: "Completed",
        timeSpentMinutes: trackedMinutes,
      });
      setHasStarted(false);
      setHasCompleted(true);
      setSessionStartedAtMs(null);
      setTimeSpentMinutes(trackedMinutes);
      setActiveSession(userId, null);
      setOk(
        linkedTaskContext
          ? "Resource completed. Your linked task was updated and future recommendations can adapt."
          : "Resource marked as completed.",
      );
      notifyInteractionUpdated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not complete resource.");
    } finally {
      setIsCompleting(false);
    }
  };

  const handleCloseCurrentAndStartThis = async () => {
    setActiveSession(userId, null);
    setStartBlockedByActiveSession(false);
    setOk("Previous session closed. You can now start this resource.");
    await handleStart();
  };

  const handleRate = async () => {
    setError(null);
    setOk(null);
    setIsRating(true);

    try {
      await createInteraction({
        userId,
        learningResourceId: resourceId,
        interactionType: "Rated",
        rating: Math.max(1, Math.min(5, Math.round(ratingValue))),
        timeSpentMinutes: Math.max(1, Math.max(Math.round(timeSpentMinutes), Math.ceil(elapsedSeconds / 60))),
      });
      setOk("Feedback saved. Future recommendations can adapt better.");
      notifyInteractionUpdated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save feedback.");
    } finally {
      setIsRating(false);
    }
  };

  return (
    <div>
      {linkedTaskContext && !compact ? (
        <p style={{ margin: "0 0 10px 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>
          This task links to a learning resource. Start here to open it, track time, and record completion for the
          recommender — even if it is no longer in your current recommendation list.
        </p>
      ) : null}

      {trimmedSource && !compact ? (
        <p style={{ margin: "0 0 8px 0", color: "var(--muted)", fontSize: 13 }}>
          <span style={{ color: "var(--text)", fontWeight: 600 }}>Source:</span> {trimmedSource}
        </p>
      ) : null}

      {openUrl ? (
        <p style={{ margin: "0 0 8px 0" }}>
          <a
            href={openUrl}
            target="_blank"
            rel="noopener noreferrer"
            style={{ color: "var(--color-ai-400)", fontWeight: 600, fontSize: 13 }}
          >
            Open resource
          </a>
        </p>
      ) : trimmedInstructions ? (
        <p style={{ margin: "0 0 8px 0", color: "var(--muted)", fontSize: 13, lineHeight: 1.5 }}>
          <span style={{ color: "var(--text)", fontWeight: 600 }}>Where to find it:</span> {trimmedInstructions}
        </p>
      ) : (
        <p style={{ margin: "0 0 8px 0", color: "var(--muted)", fontSize: 12 }}>
          No direct access link is available for this resource.
        </p>
      )}

      {error ? <p style={{ color: "rgba(239, 68, 68, 0.95)", margin: "8px 0 0 0" }}>{error}</p> : null}
      {ok ? <p style={{ color: "rgba(34, 197, 94, 0.95)", margin: "8px 0 0 0" }}>{ok}</p> : null}

      {startBlockedByActiveSession ? (
        <div
          style={{
            marginTop: 10,
            border: "1px solid rgba(245, 158, 11, 0.45)",
            background: "rgba(245, 158, 11, 0.08)",
            borderRadius: "var(--radius-md)",
            padding: 10,
          }}
        >
          <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>
            You already have another active study session
            {activeSessionResourceTitle
              ? `: “${activeSessionResourceTitle}”.`
              : activeSessionResourceId
                ? " for another resource."
                : "."}
            Close it first if you want to start this resource.
          </p>
          <div style={{ marginTop: 8, display: "flex", gap: 8, flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => void handleCloseCurrentAndStartThis()}
              style={{
                background: "rgba(245, 158, 11, 0.9)",
                color: "white",
                border: "none",
                padding: "8px 12px",
                borderRadius: "var(--radius-md)",
                cursor: "pointer",
              }}
            >
              Close current session and start this
            </button>
            <button
              type="button"
              onClick={() => setStartBlockedByActiveSession(false)}
              style={{
                background: "transparent",
                color: "var(--text)",
                border: "1px solid var(--border-strong)",
                padding: "8px 12px",
                borderRadius: "var(--radius-md)",
                cursor: "pointer",
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      ) : null}

      <div style={{ marginTop: 10, display: "flex", gap: 8, flexWrap: "wrap" }}>
        <button
          type="button"
          onClick={() => void handleStart()}
          disabled={isStarting || hasStarted}
          style={{
            background: "var(--color-recommend-600)",
            color: "white",
            border: "none",
            padding: "10px 14px",
            borderRadius: "var(--radius-md)",
            cursor: isStarting || hasStarted ? "not-allowed" : "pointer",
            opacity: isStarting || hasStarted ? 0.7 : 1,
            fontWeight: 700,
          }}
        >
          {isStarting ? "Starting..." : hasStarted ? "Started" : "Start"}
        </button>

        <button
          type="button"
          onClick={() => setIsOpen((v) => !v)}
          style={{
            background: "transparent",
            color: "var(--text)",
            border: "1px solid var(--border-strong)",
            padding: "10px 14px",
            borderRadius: "var(--radius-md)",
            cursor: "pointer",
          }}
        >
          {isOpen ? "Hide study controls" : "Show study controls"}
        </button>
      </div>

      {hasStarted ? (
        <p style={{ margin: "8px 0 0 0", color: "var(--muted)", fontSize: 12 }}>
          {hasCompleted
            ? `Session completed. Tracked time: ${formatDuration(elapsedSeconds)}.`
            : `Session timer running: ${formatDuration(elapsedSeconds)}`}
        </p>
      ) : null}

      {isOpen ? (
        <div
          style={{
            marginTop: 12,
            borderTop: compact ? undefined : "1px solid var(--border)",
            paddingTop: 12,
            display: "flex",
            flexDirection: "column",
            gap: 10,
          }}
        >
          {!compact ? (
            <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>
              Study this resource, then mark it completed and give feedback.
            </p>
          ) : null}

          <label style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <span style={{ color: "var(--muted)", fontSize: 13 }}>Time spent (minutes)</span>
            <input
              type="number"
              min={1}
              value={timeSpentMinutes}
              onChange={(e) => setTimeSpentMinutes(Number(e.target.value))}
              disabled={hasStarted && !hasCompleted}
              style={{
                width: 92,
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--border-strong)",
                background: "rgba(255,255,255,0.03)",
                color: "var(--text)",
                padding: "6px 8px",
                opacity: hasStarted && !hasCompleted ? 0.7 : 1,
              }}
            />
            {hasStarted && !hasCompleted ? (
              <span style={{ color: "var(--muted)", fontSize: 12 }}>Auto-tracked while session is active</span>
            ) : null}
          </label>

          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => void handleComplete()}
              disabled={isCompleting || hasCompleted}
              style={{
                background: "rgba(34, 197, 94, 0.9)",
                color: "white",
                border: "none",
                padding: "9px 12px",
                borderRadius: "var(--radius-md)",
                cursor: isCompleting || hasCompleted ? "not-allowed" : "pointer",
                opacity: isCompleting || hasCompleted ? 0.7 : 1,
                fontWeight: 700,
              }}
            >
              {isCompleting ? "Saving..." : hasCompleted ? "Completed" : "Mark completed"}
            </button>
          </div>

          <label style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <span style={{ color: "var(--muted)", fontSize: 13 }}>Usefulness (1-5)</span>
            <input
              type="number"
              min={1}
              max={5}
              value={ratingValue}
              onChange={(e) => setRatingValue(Number(e.target.value))}
              style={{
                width: 70,
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--border-strong)",
                background: "rgba(255,255,255,0.03)",
                color: "var(--text)",
                padding: "6px 8px",
              }}
            />
            <button
              type="button"
              onClick={() => void handleRate()}
              disabled={isRating}
              style={{
                background: "transparent",
                color: "var(--text)",
                border: "1px solid var(--border-strong)",
                padding: "8px 12px",
                borderRadius: "var(--radius-md)",
                cursor: isRating ? "not-allowed" : "pointer",
                opacity: isRating ? 0.7 : 1,
              }}
            >
              {isRating ? "Saving..." : "Save feedback"}
            </button>
          </label>
        </div>
      ) : null}
    </div>
  );
}
