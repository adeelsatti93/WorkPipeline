import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Play, Trash2, CheckCircle, Clock, AlertCircle, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import Layout from '../components/common/Layout';
import ConfirmModal from '../components/common/ConfirmModal';
import { workflowApi } from '../api/workflowApi';
import { workspacesApi } from '../api/workspacesApi';
import type { WorkSession, Workspace } from '../types';

function sessionStatusSummary(session: WorkSession): { label: string; icon: typeof CheckCircle; cls: string } {
  const tickets = session.tickets ?? [];
  const hasInProgress = tickets.some(t => t.status === 'InProgress' || t.status === 'Queued');
  const hasFailed = tickets.some(t => t.status === 'Failed');
  const allDone = session.completedAt != null;

  if (hasInProgress) return { label: 'Running', icon: Loader2, cls: 'status-running' };
  if (hasFailed) return { label: 'Has failures', icon: AlertCircle, cls: 'status-failed' };
  if (allDone) return { label: 'Completed', icon: CheckCircle, cls: 'status-done' };
  return { label: 'Pending', icon: Clock, cls: 'status-pending' };
}

export default function SessionsPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [deleteTarget, setDeleteTarget] = useState<WorkSession | null>(null);

  const { data: sessions = [], isLoading: loadingSessions } = useQuery<WorkSession[]>({
    queryKey: ['workflow', 'sessions'],
    queryFn: workflowApi.getSessions,
    refetchInterval: 15_000,
  });

  const { data: workspaces = [] } = useQuery<Workspace[]>({
    queryKey: ['workspaces'],
    queryFn: workspacesApi.getAll,
  });

  const wsMap = Object.fromEntries(workspaces.map(w => [w.id, w]));

  const deleteMutation = useMutation({
    mutationFn: workflowApi.deleteSession,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['workflow', 'sessions'] });
      toast.success('Session deleted.');
      setDeleteTarget(null);
    },
    onError: () => toast.error('Failed to delete session.'),
  });

  return (
    <Layout breadcrumbs={[{ label: 'Sessions' }]}>
      <div className="sessions-page container mt-6">
        <div className="page-header">
          <div>
            <h1>Sessions</h1>
            <p>History of all AI work sessions across your workspaces.</p>
          </div>
        </div>

        {loadingSessions ? (
          <div className="loading-state">Loading sessions…</div>
        ) : sessions.length === 0 ? (
          <div className="empty-sessions">
            <div className="empty-icon">📋</div>
            <h2>No sessions yet</h2>
            <p>Start a session from your workspace's Todo Board.</p>
            <button onClick={() => navigate('/workspaces')} className="btn-primary">
              Go to Workspaces →
            </button>
          </div>
        ) : (
          <div className="sessions-list">
            {sessions.map(session => {
              const { label, icon: Icon, cls } = sessionStatusSummary(session);
              const ws = wsMap[session.workspaceId];
              const processed = session.processedTickets;
              const total = session.totalTickets;
              const pct = total > 0 ? Math.round((processed / total) * 100) : 0;
              const date = new Date(session.createdAt).toLocaleString();

              return (
                <div key={session.id} className="session-row">
                  <div className="session-main">
                    <div className="session-title-row">
                      <span className="session-name">{session.name}</span>
                      <span className={`session-status ${cls}`}>
                        <Icon size={13} className={label === 'Running' ? 'spin' : ''} />
                        {label}
                      </span>
                    </div>

                    <div className="session-meta">
                      {ws && <span className="session-ws">{ws.name}</span>}
                      <span className="session-date">{date}</span>
                      <span className="session-tickets">{processed}/{total} tickets</span>
                    </div>

                    <div className="session-progress-bar">
                      <div className="progress-fill" style={{ width: `${pct}%` }} />
                    </div>
                  </div>

                  <div className="session-actions">
                    <button
                      onClick={() => navigate(`/execution/${session.id}`)}
                      className="btn-secondary"
                      title="View session"
                    >
                      <Play size={13} /> View
                    </button>
                    <button
                      onClick={() => setDeleteTarget(session)}
                      className="btn-icon-danger"
                      title="Delete session"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {deleteTarget && (
        <ConfirmModal
          title="Delete Session"
          message={`"${deleteTarget.name}" and all its ticket data will be permanently deleted.`}
          confirmLabel="Delete"
          variant="danger"
          onConfirm={() => deleteMutation.mutate(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </Layout>
  );
}
