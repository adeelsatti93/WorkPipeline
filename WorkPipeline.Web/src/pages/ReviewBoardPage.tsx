import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, ChevronDown, ChevronUp, Download } from 'lucide-react';
import Layout from '../components/common/Layout';
import { PriorityBadge } from '../components/common/StatusBadge';
import { reviewApi, type ReviewCard } from '../api/reviewApi';
import type { TranscriptEntry } from '../types';

const COLUMN_CONFIG = [
  { key: 'awaitingReview' as const, label: 'Awaiting Review', color: 'border-violet-500' },
  { key: 'changesRequested' as const, label: 'Changes Requested', color: 'border-red-500' },
  { key: 'merged' as const, label: 'Approved / Merged', color: 'border-green-500' },
];

export default function ReviewBoardPage() {
  const { data: board, isLoading } = useQuery({
    queryKey: ['review', 'board'],
    queryFn: reviewApi.getBoard,
    refetchInterval: 10_000,
  });

  const breadcrumbs = [{ label: 'Review Board' }];

  if (isLoading) return <Layout breadcrumbs={breadcrumbs}><div className="text-slate-400">Loading board…</div></Layout>;

  return (
    <Layout breadcrumbs={breadcrumbs}>
      <div className="review-board container mt-6">
        <h1 className="page-heading">Review Board</h1>
        <div className="board-grid">
          {COLUMN_CONFIG.map(({ key, label }) => (
            <div key={key} className={`board-column col-${key.replace('awaitingReview', 'awaiting').replace('changesRequested', 'changes').replace('merged', 'merged')}`}>
              <div className="col-header">
                <h2>{label}</h2>
                <span className="count-badge">
                  {board?.[key]?.length ?? 0}
                </span>
              </div>
              <div className="col-content">
                {(board?.[key] ?? []).map((card) => (
                  <ReviewCardComponent key={card.id} card={card} />
                ))}
                {(board?.[key]?.length ?? 0) === 0 && (
                  <p className="empty-text">No tickets</p>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </Layout>
  );
}

function ReviewCardComponent({ card }: { card: ReviewCard }) {
  const [showTranscript, setShowTranscript] = useState(false);
  const [transcript, setTranscript] = useState<TranscriptEntry[] | null>(null);
  const [loadingTranscript, setLoadingTranscript] = useState(false);

  const toggleTranscript = async () => {
    if (!showTranscript && !transcript) {
      setLoadingTranscript(true);
      const data = await reviewApi.getTranscript(card.id);
      setTranscript(data);
      setLoadingTranscript(false);
    }
    setShowTranscript((v) => !v);
  };

  const handleExport = async () => {
    const blob = await reviewApi.exportSession(card.sessionId, 'json');
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `session-${card.sessionId}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="review-card">
      <div className="card-top">
        <div className="badges-group">
          <a
            href={`https://jira.atlassian.com/browse/${card.jiraTicketKey}`}
            target="_blank"
            rel="noreferrer"
            className="key-link"
          >
            {card.jiraTicketKey} <ExternalLink size={10} />
          </a>
          <PriorityBadge priority={card.priority} />
        </div>
        {card.pullRequestUrl && (
          <a
            href={card.pullRequestUrl}
            target="_blank"
            rel="noreferrer"
            className="pr-btn"
          >
            PR #{card.pullRequestNumber} <ExternalLink size={10} />
          </a>
        )}
      </div>

      <p className="card-title">{card.jiraTicketTitle}</p>

      <div className="card-meta">
        <span>{card.jiraIssueType}</span>
        {card.assignee && <span>· {card.assignee}</span>}
        {card.branchName && <span className="branch-name" title={card.branchName}>{card.branchName}</span>}
      </div>

      <div className="card-actions">
        {card.pullRequestUrl && (
          <a
            href={card.pullRequestUrl}
            target="_blank"
            rel="noreferrer"
            className="btn-github"
          >
            Open in GitHub
          </a>
        )}
        <button onClick={toggleTranscript} className="btn-chat">
          {showTranscript ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          Chat
        </button>
        <button onClick={handleExport} className="btn-export" title="Export session">
          <Download size={12} />
        </button>
      </div>

      {showTranscript && (
        <div className="transcript-viewer">
          {loadingTranscript && <p className="transcript-loading">Loading transcript…</p>}
          {transcript?.map((entry, i) => {
            const isUser = entry.role === 'user';
            const isSystem = entry.role === 'system';
            const entryClass = isUser ? 'user' : isSystem ? 'system' : 'assistant';
            
            return (
              <div key={entry.id}>
                {(i === 0 || transcript[i - 1].roundNumber !== entry.roundNumber) && (
                  <div className="round-divider">
                    {entry.isRetryRound ? `— Round ${entry.roundNumber} (retry) —` : `— Round ${entry.roundNumber} —`}
                  </div>
                )}
                <div className={`transcript-entry ${entryClass}`}>
                  <span className="role-label">{entry.role}:</span>
                  <span>{entry.content}</span>
                </div>
              </div>
            );
          })}
          {transcript?.length === 0 && <p className="transcript-empty">No transcript available.</p>}
        </div>
      )}
    </div>
  );
}
