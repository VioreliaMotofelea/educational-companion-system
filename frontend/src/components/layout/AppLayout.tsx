import Sidebar from "./Sidebar";
import Topbar from "./Topbar";

type Props = {
  children: React.ReactNode;
};

export default function AppLayout({ children }: Props) {
  return (
    <div style={{ display: "flex", height: "100vh" }}>
      <Sidebar />
      <div style={{ flex: 1, display: "flex", flexDirection: "column" }}>
        <Topbar />
        <main style={{ padding: "20px" }}>{children}</main>
      </div>
    </div>
  );
}