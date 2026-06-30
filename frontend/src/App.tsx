import AppRoutes from "./routes/AppRoutes";
import { UserProvider } from "./context/UserContext";
import { AuthProvider } from "./context/AuthProvider";

function App() {
  return (
    <AuthProvider>
      <UserProvider>
        <AppRoutes />
      </UserProvider>
    </AuthProvider>
  );
}

export default App;