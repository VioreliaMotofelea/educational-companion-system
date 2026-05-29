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
