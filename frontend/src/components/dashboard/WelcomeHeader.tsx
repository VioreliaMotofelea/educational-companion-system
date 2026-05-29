export default function WelcomeHeader() {
  return (
    <header style={{ marginBottom: 8 }}>
      <h1 style={{ margin: "0 0 6px 0" }}>Welcome back</h1>
      <p style={{ margin: 0, color: "var(--muted)", fontSize: 15, lineHeight: 1.5, maxWidth: 560 }}>
        Here is a calm overview of your progress, today&apos;s plan, and a few hand-picked resources to keep momentum.
      </p>
    </header>
  );
}
