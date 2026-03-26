export default function Topbar() {
  return (
    <div
      style={{
        height: "60px",
        borderBottom: "1px solid #ddd",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 20px",
      }}
    >
      <h3>Dashboard</h3>
      <div>User</div>
    </div>
  );
}