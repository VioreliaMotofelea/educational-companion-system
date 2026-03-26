import Sidebar from "./Sidebar";
import Topbar from "./Topbar";

type Props = {
  children: React.ReactNode;
};

export default function AppLayout({ children }: Props) {
  return (
    <div style={{ display: "flex", minHeight: "100vh", width: "100%" }}>
      <Sidebar />
      <div style={{ flex: 1, display: "flex", flexDirection: "column" }}>
        <Topbar />
        <main style={{ padding: "20px", flex: 1 }}>{children}</main>
      </div>
    </div>
  );
}