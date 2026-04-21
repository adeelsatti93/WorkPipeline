import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import LoginPage from '../pages/LoginPage';
import WorkspacesPage from '../pages/WorkspacesPage';
import TodoBoardPage from '../pages/TodoBoardPage';
import ExecutionPage from '../pages/ExecutionPage';
import ReviewBoardPage from '../pages/ReviewBoardPage';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const token = useAuthStore((s) => s.token);
  return token ? <>{children}</> : <Navigate to="/login" replace />;
}

export default function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/workspaces" element={<ProtectedRoute><WorkspacesPage /></ProtectedRoute>} />
      <Route path="/todos" element={<ProtectedRoute><TodoBoardPage /></ProtectedRoute>} />
      <Route path="/execution/:sessionId" element={<ProtectedRoute><ExecutionPage /></ProtectedRoute>} />
      <Route path="/review" element={<ProtectedRoute><ReviewBoardPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/workspaces" replace />} />
    </Routes>
  );
}
