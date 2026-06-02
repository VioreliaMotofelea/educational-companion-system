import { describe, expect, it } from "vitest";
import {
  friendlyHybridReason,
  friendlyRecommendationLoadError,
  humanizeResourceTitle,
  humanizeTopicLine,
  learnerFacingRecommendationReason,
  matchStrengthForLearner,
  parseHybridExplanation,
} from "./recommendationUtils";

describe("humanizeResourceTitle", () => {
  it("removes trailing numeric hash suffix", () => {
    expect(humanizeResourceTitle("Course homepage - Module BBB #913473")).toBe("Course homepage - Module BBB");
  });

  it("removes trailing hex hash suffix", () => {
    expect(humanizeResourceTitle("Intro to graphs #abc123def")).toBe("Intro to graphs");
  });

  it("trims whitespace and preserves title when no suffix", () => {
    expect(humanizeResourceTitle("  Plain title  ")).toBe("Plain title");
  });

  it("returns original trimmed title when stripping would leave empty", () => {
    expect(humanizeResourceTitle("#999")).toBe("#999");
  });
});

describe("humanizeTopicLine", () => {
  it("returns General learning for empty input", () => {
    expect(humanizeTopicLine("")).toBe("General learning");
    expect(humanizeTopicLine(null)).toBe("General learning");
    expect(humanizeTopicLine(undefined)).toBe("General learning");
  });

  it("strips OULAD references and stray hashes", () => {
    expect(humanizeTopicLine("BBB · OULAD #45a5134d")).toBe("BBB");
  });

  it("strips standalone hex fragments", () => {
    expect(humanizeTopicLine("Topic #abcdef12 rest")).toBe("Topic rest");
  });

  it("returns General learning when nothing meaningful remains", () => {
    expect(humanizeTopicLine("OULAD #45a5134d")).toBe("General learning");
  });
});

describe("matchStrengthForLearner", () => {
  it("labels strong match at upper threshold", () => {
    const r = matchStrengthForLearner(0.35);
    expect(r.label).toBe("Strong match");
    expect(r.hint.length).toBeGreaterThan(10);
  });

  it("uses Great fit between 0.22 and 0.35", () => {
    expect(matchStrengthForLearner(0.34).label).toBe("Great fit");
    expect(matchStrengthForLearner(0.22).label).toBe("Great fit");
  });

  it("uses Good option between 0.12 and 0.22", () => {
    expect(matchStrengthForLearner(0.21).label).toBe("Good option");
    expect(matchStrengthForLearner(0.12).label).toBe("Good option");
  });

  it("uses Worth trying below 0.12", () => {
    expect(matchStrengthForLearner(0.11).label).toBe("Worth trying");
    expect(matchStrengthForLearner(0).label).toBe("Worth trying");
  });

  it("clamps scores outside 0–1", () => {
    expect(matchStrengthForLearner(2).label).toBe("Strong match");
    expect(matchStrengthForLearner(-1).label).toBe("Worth trying");
  });

  it("treats non-finite scores as zero", () => {
    expect(matchStrengthForLearner(Number.NaN).label).toBe("Worth trying");
    expect(matchStrengthForLearner(Number.POSITIVE_INFINITY).label).toBe("Worth trying");
  });
});

describe("parseHybridExplanation", () => {
  it("parses classic TF-IDF hybrid explanations", () => {
    const parsed = parseHybridExplanation(
      "Content match 1.00, similar users 0.00, difficulty fit 1.00 (suggested level 2)."
    );
    expect(parsed).toEqual({
      contentScore: 1,
      collabScore: 0,
      difficultyScore: 1,
      suggestedLevel: 2,
      usesSemanticContent: false,
    });
  });

  it("parses semantic-only hybrid explanations", () => {
    const parsed = parseHybridExplanation(
      "Semantic content match 0.98, similar users 0.00, difficulty fit 1.00 (suggested level 2)."
    );
    expect(parsed?.usesSemanticContent).toBe(true);
    expect(parsed?.contentScore).toBe(0.98);
  });

  it("parses fused TF-IDF+semantic explanations", () => {
    const parsed = parseHybridExplanation(
      "Content match 0.82 (TF-IDF+semantic), similar users 0.15, difficulty fit 0.90 (suggested level 2)."
    );
    expect(parsed?.contentScore).toBe(0.82);
    expect(parsed?.usesSemanticContent).toBe(false);
  });
});

describe("friendlyHybridReason", () => {
  it("uses semantic wording when semantic content is active", () => {
    const parsed = parseHybridExplanation(
      "Semantic content match 1.00, similar users 0.00, difficulty fit 1.00 (suggested level 2)."
    );
    expect(parsed).not.toBeNull();
    expect(friendlyHybridReason(parsed!)).toContain("semantically similar");
  });
});

describe("learnerFacingRecommendationReason", () => {
  it("converts hybrid technical explanation to friendly text", () => {
    const friendly = learnerFacingRecommendationReason(
      "Semantic content match 1.00, similar users 0.00, difficulty fit 1.00 (suggested level 2)."
    );
    expect(friendly).toContain("semantically similar");
    expect(friendly).not.toContain("similar users 0.00");
  });
});

describe("friendlyRecommendationLoadError", () => {
  it("hides raw JSON from AI 500 errors", () => {
    const msg = friendlyRecommendationLoadError(
      'Recommendation generation failed: AI service returned 500: {"detail":"An unexpected internal server error occurred."}'
    );
    expect(msg).not.toContain("{");
    expect(msg).toContain("Try again");
  });
});
