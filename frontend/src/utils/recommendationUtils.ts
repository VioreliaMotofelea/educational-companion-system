export function humanizeResourceTitle(title: string): string {
  let t = title.trim();
  t = t.replace(/\s+#([0-9a-f]{6,}|[0-9]+)\s*$/i, "");
  return t.trim() || title.trim();
}

export function humanizeTopicLine(topic: string | undefined | null): string {
  if (!topic?.trim()) return "General learning";
  let t = topic.trim();
  t = t.replace(/\bOULAD\s*#?[0-9a-f-]*\b/gi, "");
  t = t.replace(/#[0-9a-f]{4,}/gi, "");
  t = t.replace(/\s{2,}/g, " ");
  t = t.replace(/^\s*·\s*|\s*·\s*$/g, "").trim();
  if (!t) return "General learning";
  t = t.replace(/\s*·\s*$/g, "").replace(/^\s*·\s*/g, "").trim();
  if (!t) return "General learning";
  return t;
}

export type MatchStrength = {
  label: string;
  hint: string;
};

// map hybrid score (typically 0–1) to friendly labels — no raw decimals in the main UI
export function matchStrengthForLearner(score: number): MatchStrength {
  const s = Math.max(0, Math.min(1, Number.isFinite(score) ? score : 0));
  if (s >= 0.35) {
    return {
      label: "Strong match",
      hint: "This resource lines up closely with your topics, level, and what similar learners found useful.",
    };
  }
  if (s >= 0.22) {
    return {
      label: "Great fit",
      hint: "Balanced pick for your profile — worth prioritizing in your next study block.",
    };
  }
  if (s >= 0.12) {
    return {
      label: "Good option",
      hint: "A solid next step to diversify your path while staying close to your goals.",
    };
  }
  return {
    label: "Worth trying",
    hint: "A discovery suggestion — explore it if you want something a bit different today.",
  };
}

export type ScoreBand = "strong" | "moderate" | "low" | "none";

function scoreBand(score: number): ScoreBand {
  const s = Math.max(0, Math.min(1, Number.isFinite(score) ? score : 0));
  if (s >= 0.7) return "strong";
  if (s >= 0.35) return "moderate";
  if (s > 0) return "low";
  return "none";
}


const HYBRID_FULL_REASON =
  /^(?:Semantic content match|Content match)\s+([0-9.]+)(?:\s*\([^)]+\))?,\s*similar users\s+([0-9.]+),\s*difficulty fit\s+([0-9.]+)\s*\(suggested level\s+([0-9]+)\)\.?$/i;

const HYBRID_NO_DIFFICULTY_REASON =
  /^(?:Semantic content match|Content match)\s+([0-9.]+)(?:\s*\([^)]+\))?,\s*similar users\s+([0-9.]+)\.\s*Difficulty is not used/i;

export type ParsedHybridReason = {
  contentScore: number;
  collabScore: number;
  difficultyScore: number | null;
  suggestedLevel: number | null;
  usesSemanticContent: boolean;
};

export function parseHybridExplanation(reason: string): ParsedHybridReason | null {
  const full = reason.match(HYBRID_FULL_REASON);
  if (full) {
    return {
      contentScore: Number(full[1]),
      collabScore: Number(full[2]),
      difficultyScore: Number(full[3]),
      suggestedLevel: Number(full[4]),
      usesSemanticContent: /^Semantic content match/i.test(reason),
    };
  }

  const noDifficulty = reason.match(HYBRID_NO_DIFFICULTY_REASON);
  if (noDifficulty) {
    return {
      contentScore: Number(noDifficulty[1]),
      collabScore: Number(noDifficulty[2]),
      difficultyScore: null,
      suggestedLevel: null,
      usesSemanticContent: /^Semantic content match/i.test(reason),
    };
  }

  return null;
}

export function learnerFacingRecommendationReason(reason: string): string {
  const parsed = parseHybridExplanation(reason);
  if (parsed) return friendlyHybridReason(parsed);
  return reason.trim() || "A personalized pick based on your profile and activity.";
}

export function friendlyRecommendationLoadError(raw: string): string {
  const text = raw.trim();
  if (!text) return "We could not load your recommendations. Please try again.";

  if (/AI service returned 5\d\d/i.test(text) || /unexpected internal server error/i.test(text)) {
    return "Your picks are still warming up — the assistant can take a few seconds on first load. Tap Try again.";
  }

  if (/generation failed/i.test(text)) {
    return "We could not refresh your recommendations just now. Please try again in a moment.";
  }

  if (text.includes('{"detail"') || text.includes('"detail":')) {
    return "We could not reach the recommendation service. Please try again.";
  }

  return text;
}

export function friendlyHybridReason(parsed: ParsedHybridReason): string {
  if (parsed.suggestedLevel !== null) {
    return parsed.usesSemanticContent
      ? `Recommended because it is semantically similar to resources you completed and fits your suggested level ${parsed.suggestedLevel}.`
      : `Recommended because it lines up with topics you have engaged with and fits your suggested level ${parsed.suggestedLevel}.`;
  }

  return parsed.usesSemanticContent
    ? "Recommended because it is semantically similar to resources you completed and what similar learners found useful."
    : "Recommended because it lines up with topics you have engaged with and what similar learners found useful.";
}

export function hybridTechnicalReason(parsed: ParsedHybridReason): string {
  const content = parsed.usesSemanticContent
    ? scoreBandLabel(parsed.contentScore, "content").replace("Content match:", "Semantic content match:")
    : scoreBandLabel(parsed.contentScore, "content");
  const peers = scoreBandLabel(parsed.collabScore, "collab");

  if (parsed.difficultyScore !== null) {
    return `${content} · ${peers} · ${scoreBandLabel(parsed.difficultyScore, "difficulty")}`;
  }

  return `${content} · ${peers}`;
}

export function hybridModelBreakdown(parsed: ParsedHybridReason): string {
  if (parsed.difficultyScore !== null) {
    return `Raw model values: content ${parsed.contentScore.toFixed(2)}, peers ${parsed.collabScore.toFixed(2)}, difficulty ${parsed.difficultyScore.toFixed(2)}`;
  }

  return `Raw model values: content ${parsed.contentScore.toFixed(2)}, peers ${parsed.collabScore.toFixed(2)}`;
}

export function scoreBandLabel(score: number, dimension: "content" | "collab" | "difficulty"): string {
  const band = scoreBand(score);
  if (dimension === "content") {
    if (band === "strong") return "Content match: strong";
    if (band === "moderate") return "Content match: moderate";
    if (band === "low") return "Content match: light";
    return "Content match: still learning";
  }
  if (dimension === "collab") {
    if (band === "strong") return "Peer signal: strong";
    if (band === "moderate") return "Peer signal: moderate";
    if (band === "low") return "Peer signal: light";
    return "Peer signal: building up";
  }
  if (band === "strong") return "Difficulty: perfectly aligned";
  if (band === "moderate") return "Difficulty: well aligned";
  if (band === "low") return "Difficulty: somewhat aligned";
  return "Difficulty: lower alignment";
}
