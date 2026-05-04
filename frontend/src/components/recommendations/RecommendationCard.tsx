import { createInteraction } from "../../services/api";
import { useEffect, useState } from "react";

type Props = {
  title: string;
  reason: string;
  description?: string | null;
  topic?: string;
  contentType?: "Article" | "Video" | "Quiz";
  resourceId: string;
  userId: string;
  difficulty: number;
  durationMinutes: number;
  score: number;
};

function difficultyLabel(difficulty: number) {
  if (difficulty <= 1) return "Easy";
  if (difficulty === 2) return "Beginner+";
  if (difficulty === 3) return "Medium";
  if (difficulty === 4) return "Hard";
  return "Advanced";
}

function shortenText(text: string, maxLength: number) {
  if (text.length <= maxLength) return text;
  return `${text.slice(0, maxLength - 1)}...`;
}

function formatDuration(seconds: number) {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}m ${String(secs).padStart(2, "0")}s`;
}

const ACTIVE_SESSION_KEY_PREFIX = "ecs.activeSession.";

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

export default function RecommendationCard({
  title,
  reason,
  description,
  topic,
  contentType,
  resourceId,
  userId,
  difficulty,
  durationMinutes,
  score,
}: Props) {
  const [isStarting, setIsStarting] = useState(false);
  const [isCompleting, setIsCompleting] = useState(false);
  const [isRating, setIsRating] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
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

  const reasonMatch = reason.match(
    /Content match ([0-9.]+), similar users ([0-9.]+), difficulty fit ([0-9.]+) \(suggested level ([0-9]+)\)\.?/
  );
  const friendlyReason = reasonMatch
    ? `Recommended because it matches resources you completed and fits your suggested level ${reasonMatch[4]}.`
    : reason;
  const technicalReason = reasonMatch
    ? `Content ${reasonMatch[1]} · Similar users ${reasonMatch[2]} · Difficulty ${reasonMatch[3]}`
    : null;
  const subtitle = `${topic ?? "General"} · OULAD #${resourceId.slice(0, 8)}${contentType ? ` · ${contentType}` : ""}`;
  const compactDescription = description ? shortenText(description, 140) : null;

  const notifyInteractionUpdated = () => {
    window.dispatchEvent(new CustomEvent("interaction-updated"));
  };

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

      // Backfill legacy active sessions that were created before resourceTitle existed.
      if (!active.resourceTitle && active.resourceId === resourceId) {
        setActiveSession(userId, {
          ...active,
          resourceTitle: title,
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
  }, [userId, resourceId, hasCompleted]);

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
        resourceTitle: title,
        startedAtMs: startedAt,
      });
      setOk("Session started. Now complete it when you finish studying.");
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
      setOk("Resource marked as completed.");
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
    <div
      style={{
        border: "1px solid var(--border)",
        background: "var(--panel)",
        padding: "14px",
        margin: "10px 0",
        borderRadius: "var(--radius-md)",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "flex-start" }}>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <span
            style={{
              border: "1px solid var(--border)",
              background: "rgba(255,255,255,0.03)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--text)",
            }}
          >
            {difficultyLabel(difficulty)} • {difficulty}/5
          </span>
          <span
            style={{
              border: "1px solid var(--border)",
              background: "rgba(255,255,255,0.03)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--text)",
            }}
          >
            {durationMinutes} min
          </span>
          <span
            style={{
              border: "1px solid var(--border)",
              background: "rgba(245, 158, 11, 0.12)",
              padding: "4px 8px",
              borderRadius: 999,
              fontSize: 12,
              color: "var(--color-recommend-500)",
              fontWeight: 800,
            }}
          >
            AI score: {score.toFixed(2)}
          </span>
        </div>
      </div>
      <h3 style={{ margin: 0 }}>{title}</h3>
      <p style={{ margin: "6px 0 0 0", color: "var(--muted)", fontSize: 13 }}>{subtitle}</p>
      <p style={{ margin: "8px 0 0 0", color: "var(--muted)" }}>💡 {friendlyReason}</p>
      {technicalReason ? (
        <p style={{ margin: "6px 0 0 0", color: "var(--muted)", fontSize: 12, opacity: 0.9 }}>{technicalReason}</p>
      ) : null}
      {compactDescription ? (
        <p style={{ margin: "6px 0 0 0", color: "var(--muted)", fontSize: 12, opacity: 0.85 }}>{compactDescription}</p>
      ) : null}
      {error ? <p style={{ color: "rgba(239, 68, 68, 0.95)", margin: "10px 0 0 0" }}>{error}</p> : null}
      {ok ? <p style={{ color: "rgba(34, 197, 94, 0.95)", margin: "10px 0 0 0" }}>{ok}</p> : null}
      {startBlockedByActiveSession ? (
        <div style={{ marginTop: 10, border: "1px solid rgba(245, 158, 11, 0.45)", background: "rgba(245, 158, 11, 0.08)", borderRadius: "var(--radius-md)", padding: 10 }}>
          <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>
            You already have another active study session
            {activeSessionResourceTitle
              ? `: "${activeSessionResourceTitle}".`
              : activeSessionResourceId
                ? ` (${activeSessionResourceId.slice(0, 8)}).`
                : "."}
            Close it first if you want to start this resource.
          </p>
          <div style={{ marginTop: 8, display: "flex", gap: 8 }}>
            <button
              onClick={handleCloseCurrentAndStartThis}
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

      <div style={{ marginTop: 12, display: "flex", gap: 8, flexWrap: "wrap" }}>
        <button
          onClick={handleStart}
          disabled={isStarting || hasStarted}
          style={{
            background: "var(--color-recommend-600)",
            color: "white",
            border: "none",
            padding: "10px 14px",
            borderRadius: "var(--radius-md)",
            cursor: isStarting || hasStarted ? "not-allowed" : "pointer",
            opacity: isStarting || hasStarted ? 0.7 : 1,
          }}
        >
          {isStarting ? "Starting..." : hasStarted ? "Started" : "Start"}
        </button>

        <button
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
        <div style={{ marginTop: 12, borderTop: "1px solid var(--border)", paddingTop: 12, display: "flex", flexDirection: "column", gap: 10 }}>
          <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>
            Study this resource, then mark it completed and give feedback.
          </p>

          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
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
              onClick={handleComplete}
              disabled={isCompleting}
              style={{
                background: "rgba(34, 197, 94, 0.9)",
                color: "white",
                border: "none",
                padding: "9px 12px",
                borderRadius: "var(--radius-md)",
                cursor: isCompleting ? "not-allowed" : "pointer",
                opacity: isCompleting ? 0.7 : 1,
              }}
            >
              {isCompleting ? "Saving..." : hasCompleted ? "Completed" : "Mark completed"}
            </button>
          </div>

          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
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
              onClick={handleRate}
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