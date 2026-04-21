import { useEffect, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, GitBranch, Download, Cpu, TestTube, Upload, GitPullRequest, X } from 'lucide-react';
import Layout from '../components/common/Layout';
import { StatusBadge } from '../components/common/StatusBadge';
import { workflowApi } from '../api/workflowApi';
import { useWorkflowStore } from '../store/workflowStore';
import { useWorkflowStream } from '../hooks/useWorkflowStream';
import type { WorkSession, TicketWorkStatus } from '../types';

const STEP_ICONS: Record<string, { icon: typeof Download; label: string }> = {
  CloningRepo:    { icon: Download,       label: 'Cloning' },
  RepoReady:      { icon: Download,       label: 'Repo Ready' },
  CreatingBranch: { icon: GitBranch,      label: 'Branching' },
  BranchCreated:  { icon: GitBranch,      label: 'Branch Ready' },
  FetchingFiles:  { icon: Download,       label: 'Fetching Files' },
  FilesReady:     { icon: Download,       label: 'Files Ready' },
  RunningClaude:  { icon: Cpu,            label: 'AI Working' },
  ChangesReady:   { icon: Cpu,            label: 'Changes Ready' },
  Committed:      { icon: Upload,         label: 'Committed' },
  VerifyingTests: { icon: TestTube,       label: 'Testing' },
  PushingBranch:  { icon: Upload,         label: 'Pushing' },
  CreatingPR:     { icon: GitPullRequest, label: 'Creating PR' },
};

export default function ExecutionPage() {
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const streamEndRef = useRef<HTMLDivElement>(null);

  const { ticketMap, streamChunks, stepProgress, selectedStreamTicketId, setTickets, setSelectedStreamTicket } = useWorkflowStore();

  useWorkflowStream(sessionId ?? null);

  const { data: session } = useQuery<WorkSession>({
    queryKey: ['workflow', 'session', sessionId],
    queryFn: () => workflowApi.getSession(sessionId!),
    refetchInterval: 5000,
    enabled: !!sessionId,
  });

  // Sync tickets into store when session data arrives
  useEffect(() => {
    if (session) setTickets(session.tickets);
  }, [session]);

  const retryMutation = useMutation({
    mutationFn: workflowApi.retryTicket,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['workflow', 'session', sessionId] }),
  });

  const deleteMutation = useMutation({
    mutationFn: workflowApi.deleteSession,
    onSuccess: (_, deletedId) => {
      qc.invalidateQueries({ queryKey: ['ticket-sessions'] });
      qc.invalidateQueries({ queryKey: ['workflow', 'sessions'] });
      if (deletedId === sessionId) navigate('/');
    },
  });

  const lastViewedJiraKey = useRef<string | null>(null);

  const tickets = session?.tickets ?? [];
  let activeTicket = selectedStreamTicketId ? tickets.find((t) => t.id === selectedStreamTicketId) : undefined;

  // Prevent UI breaking on session switch: fallback to the same Jira key we were just viewing
  if (!activeTicket && lastViewedJiraKey.current) {
    activeTicket = tickets.find(t => t.jiraTicketKey === lastViewedJiraKey.current);
  }

  // Final fallback
  if (!activeTicket) {
    activeTicket = tickets.find((t) => t.status === 'InProgress') ?? tickets[0];
  }

  // Sync selected ID to store to avoid multiple renders
  useEffect(() => {
    if (activeTicket) {
      lastViewedJiraKey.current = activeTicket.jiraTicketKey;
      if (activeTicket.id !== selectedStreamTicketId) {
        setSelectedStreamTicket(activeTicket.id);
      }
    }
  }, [activeTicket, selectedStreamTicketId, setSelectedStreamTicket]);

  const chunks = activeTicket ? (streamChunks[activeTicket.id] ?? []) : [];
  const activeStep = activeTicket ? stepProgress[activeTicket.id] : undefined;
  const progress = session ? (session.processedTickets / session.totalTickets) * 100 : 0;

  // Fetch full session history for the active ticket
  const ticketKeys = activeTicket ? [activeTicket.jiraTicketKey] : [];
  const { data: ticketSessions = {} } = useQuery({
    queryKey: ['ticket-sessions', ...ticketKeys],
    queryFn: () => workflowApi.getSessionsByTicketKeys(ticketKeys),
    enabled: ticketKeys.length > 0,
    refetchInterval: 30_000,
  });
  const activeTicketSessions = activeTicket ? ticketSessions[activeTicket.jiraTicketKey] ?? [] : [];

  useEffect(() => {
    streamEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [chunks.length]);

  return (
    <Layout>
      <div className="execution-page">
        {/* Progress bar */}
        <div className="progress-section">
          <div className="progress-header">
            <h1>{session?.name ?? 'Loading…'}</h1>
            <span className="progress-text">
              {session?.processedTickets ?? 0} / {session?.totalTickets ?? 0} complete
            </span>
          </div>
          <div className="progress-bar-container">
            <div
              className="progress-bar"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>

        <div className="execution-workspace">
          {/* Left: queue list */}
          <div className="queue-panel">
            {tickets.map((ticket) => {
              const liveTicket = ticketMap[ticket.id] ?? ticket;
              const ticketStep = stepProgress[ticket.id];
              const StepIcon = ticketStep ? STEP_ICONS[ticketStep.step]?.icon : undefined;
              return (
                <button
                  key={ticket.id}
                  onClick={() => setSelectedStreamTicket(ticket.id)}
                  className={`queue-item ${activeTicket?.id === ticket.id ? 'active' : ''}`}
                >
                  <div className="item-header">
                    <span className="item-key">{ticket.jiraTicketKey}</span>
                    <StatusBadge status={liveTicket.status} />
                  </div>
                  <p className="item-title">{ticket.jiraTicketTitle}</p>
                  {/* Step indicator */}
                  {ticketStep && liveTicket.status === 'InProgress' && (
                    <div className="step-indicator">
                      {StepIcon && <StepIcon size={12} />}
                      <span>{ticketStep.message}</span>
                    </div>
                  )}
                  {liveTicket.status === 'Failed' && (
                    <button
                      onClick={(e) => { e.stopPropagation(); retryMutation.mutate(ticket.id); }}
                      className="btn-retry"
                    >
                      Retry
                    </button>
                  )}
                  {liveTicket.pullRequestUrl && (
                    <a
                      href={liveTicket.pullRequestUrl}
                      target="_blank"
                      rel="noreferrer"
                      onClick={(e) => e.stopPropagation()}
                      className="pr-link"
                    >
                      <ExternalLink size={10} /> View PR
                    </a>
                  )}
                </button>
              );
            })}
          </div>

          {/* Right: stream panel */}
          <div className="stream-panel">
            <div className="stream-header">
              <div className="stream-header-top">
                <div>
                  {activeTicket ? (
                    <span className="stream-title">
                      <span className="key">{activeTicket.jiraTicketKey}</span>
                      <span className="title">— {activeTicket.jiraTicketTitle}</span>
                    </span>
                  ) : (
                    <span className="text-muted">No ticket selected</span>
                  )}
                </div>
                {activeTicket && <StatusBadge status={(ticketMap[activeTicket.id] ?? activeTicket).status} />}
              </div>
              
              {/* Session History Tabs */}
              {activeTicketSessions.length > 0 && (
                <div className="stream-sessions-tabs">
                  <span className="tabs-label">Sessions for this ticket:</span>
                  <div className="tabs-list">
                    {activeTicketSessions.map((s) => {
                      const isActive = s.sessionId === sessionId;
                      const dateStr = new Date(s.createdAt).toLocaleDateString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
                      return (
                        <div key={s.sessionId} className={`session-tab ${isActive ? 'active' : ''}`}>
                          <Link
                            to={`/execution/${s.sessionId}`}
                            className="session-tab-link"
                            title={`Status: ${s.status}`}
                          >
                            {isActive && <div className="active-dot" />}
                            {dateStr}
                          </Link>
                          <button
                            className="session-tab-delete"
                            title="Delete session"
                            onClick={() => deleteMutation.mutate(s.sessionId)}
                            disabled={deleteMutation.isPending}
                          >
                            <X size={10} />
                          </button>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>

            <div className="stream-content">
              {chunks.map((chunk, i) => {
                const isUser = chunk.role === 'user';
                const isSystem = chunk.role === 'system';
                const chunkClass = isUser ? 'chunk user' : isSystem ? 'chunk system' : 'chunk assistant';
                return (
                  <div key={i} className={chunkClass}>
                    {isUser && <span className="round-badge">[Round {chunk.roundNumber} prompt]</span>}
                    <span>{chunk.content}</span>
                  </div>
                );
              })}
              {chunks.length === 0 && activeTicket && activeStep && (
                <div className="step-progress-display">
                  <div className="step-progress-icon-wrapper">
                    {(() => {
                      const Icon = STEP_ICONS[activeStep.step]?.icon;
                      return Icon ? <Icon size={24} /> : null;
                    })()}
                  </div>
                  <div className="step-progress-message">{activeStep.message}</div>
                  <div className="step-progress-dots">
                    <span className="dot" /><span className="dot" /><span className="dot" />
                  </div>
                </div>
              )}
              {chunks.length === 0 && activeTicket && !activeStep && (() => {
                const sessionProcessed = session?.processedTickets ?? 0;
                const activeStatuses = ['Queued', 'InProgress', 'VerifyingTests', 'PushingBranch', 'CreatingPR'] as TicketWorkStatus[];
                const isActive = activeStatuses.includes(activeTicket.status);
                const isConnecting = !isActive && activeTicket.status !== 'Failed' && activeTicket.status !== 'Skipped' && sessionProcessed === 0;

                const message =
                  activeTicket.status === 'Queued'          ? 'Waiting in queue…'
                  : activeTicket.status === 'InProgress'    ? 'Initializing workflow…'
                  : activeTicket.status === 'VerifyingTests' ? 'Verifying tests…'
                  : activeTicket.status === 'PushingBranch' ? 'Pushing branch…'
                  : activeTicket.status === 'CreatingPR'    ? 'Creating pull request…'
                  : activeTicket.status === 'Failed'        ? 'Workflow failed.'
                  : activeTicket.status === 'Skipped'       ? 'Ticket skipped.'
                  : isConnecting                            ? 'Connecting to workflow…'
                  : 'Workflow complete.';

                return (
                  <div className="step-progress-display">
                    <div className="step-progress-message waiting-text">{message}</div>
                    {(isActive || isConnecting) && (
                      <div className="step-progress-dots">
                        <span className="dot" /><span className="dot" /><span className="dot" />
                      </div>
                    )}
                  </div>
                );
              })()}
              <div ref={streamEndRef} />
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
}
