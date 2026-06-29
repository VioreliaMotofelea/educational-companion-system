type Props = {
  onClick: () => void;
  disabled?: boolean;
  variant?: "primary" | "secondary";
};

export default function RegenerateRecommendationsButton({
  onClick,
  disabled = false,
  variant = "primary",
}: Props) {
  const isPrimary = variant === "primary";

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      style={{
        background: isPrimary ? "var(--color-ai-600)" : "transparent",
        color: isPrimary ? "#fff" : "var(--text)",
        border: isPrimary ? "none" : "1px solid var(--border-strong)",
        borderRadius: "var(--radius-md)",
        padding: "8px 14px",
        cursor: disabled ? "not-allowed" : "pointer",
        fontWeight: isPrimary ? 700 : 600,
        opacity: disabled ? 0.65 : 1,
      }}
    >
      {disabled ? "Regenerating…" : "Regenerate recommendations"}
    </button>
  );
}
