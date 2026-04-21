import React, { useState } from 'react';

function renderDescription(raw: string): React.ReactNode {
  // Strip Jira wiki markup images: !filename.png|...! → [Image: filename]
  const cleaned = raw
    .replace(/!([^|!\n]+?)(?:\|[^!]*)?\!/g, (_, filename) => `📷 [Image: ${filename}]`)
    // Strip bold *text* → text, italic _text_ → text, monospace {{text}} → text
    .replace(/\*([^*\n]+)\*/g, '$1')
    .replace(/_([^_\n]+)_/g, '$1')
    .replace(/\{\{([^}]+)\}\}/g, '$1')
    // Strip Jira color macros {color:...}text{color}
    .replace(/\{color:[^}]+\}(.*?)\{color\}/g, '$1')
    // Clean up multiple blank lines
    .replace(/\n{3,}/g, '\n\n')
    .trim();
  return cleaned || 'No description provided.';
}
import { useQuery, useMutation } from '@tanstack/react-query';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { RefreshCw, ChevronDown, ChevronRight, ExternalLink } from 'lucide-react';
import toast from 'react-hot-toast';
import Layout from '../components/common/Layout';
import { PriorityBadge } from '../components/common/StatusBadge';
import SearchableSelect from '../components/common/SearchableSelect';
import { ticketsApi } from '../api/ticketsApi';
import { workflowApi, type TicketSessionInfo } from '../api/workflowApi';
import { useWorkflowStore } from '../store/workflowStore';
import type { JiraTicket } from '../types';

export default function TodoBoardPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const workspaceId = searchParams.get('workspaceId') ?? '';

  const { selectedTicketKeys, toggleTicket, selectAll, clearSelection } = useWorkflowStore();
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [filterPriority, setFilterPriority] = useState('');
  const [filterType, setFilterType] = useState('');

  const { data: tickets = [], isLoading, refetch } = useQuery<JiraTicket[]>({
    queryKey: ['tickets', 'todo', workspaceId],
    queryFn: () => ticketsApi.getTodoTickets(workspaceId),
    refetchInterval: 60_000,
    enabled: !!workspaceId,
  });

  // Fetch session info for all ticket keys so we can show "View Session" links
  const ticketKeys = tickets.map((t) => t.key);
  const { data: ticketSessions = {} } = useQuery<Record<string, TicketSessionInfo[]>>({
    queryKey: ['ticket-sessions', ...ticketKeys],
    queryFn: () => workflowApi.getSessionsByTicketKeys(ticketKeys),
    enabled: ticketKeys.length > 0,
    refetchInterval: 30_000,
  });

  const startMutation = useMutation({
    mutationFn: (keys: string[]) => workflowApi.startWorkflow(workspaceId, keys),
    onSuccess: ({ sessionId }) => {
      clearSelection();
      navigate(`/execution/${sessionId}`);
    },
    onError: () => toast.error('Failed to start workflow.'),
  });

  if (!workspaceId) {
    return (
      <Layout>
        <div className="board-page container mt-6">
          <div className="empty-state">
            No workspace selected. <a href="/workspaces" className="link">Go to Workspaces →</a>
          </div>
        </div>
      </Layout>
    );
  }

  const filtered = tickets.filter((t) => {
    if (filterPriority && t.priority !== filterPriority) return false;
    if (filterType && t.issueType !== filterType) return false;
    return true;
  });

  const priorities = [...new Set(tickets.map((t) => t.priority))];
  const types = [...new Set(tickets.map((t) => t.issueType))];
  const allSelected = filtered.length > 0 && filtered.every((t) => selectedTicketKeys.has(t.key));

  return (
    <Layout>
      <div className="board-page container mt-6">
        {/* Header */}
        <div className="board-header">
          <div className="title-group">
            <h1>Todo Board</h1>
            <p>{tickets.length} tickets in To Do</p>
          </div>
          <button onClick={() => refetch()} className="btn-secondary">
            <RefreshCw size={14} /> Refresh
          </button>
        </div>

        {/* Filters */}
        <div className="filters-row">
          <SearchableSelect
            options={[
              { value: '', label: 'All Priorities' },
              ...priorities.map(p => ({ value: p, label: p })),
            ]}
            value={filterPriority}
            onChange={setFilterPriority}
            placeholder="All Priorities"
          />
          <SearchableSelect
            options={[
              { value: '', label: 'All Types' },
              ...types.map(t => ({ value: t, label: t })),
            ]}
            value={filterType}
            onChange={setFilterType}
            placeholder="All Types"
          />
          {selectedTicketKeys.size > 0 && (
            <button onClick={clearSelection} className="btn-clear">
              Clear selection
            </button>
          )}
        </div>

        {/* Table */}
        <div className="table-container">
          {isLoading ? (
            <div className="loading-state">Loading tickets…</div>
          ) : filtered.length === 0 ? (
            <div className="empty-state">No tickets found.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th className="col-checkbox">
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={() => (allSelected ? clearSelection() : selectAll(filtered.map((t) => t.key)))}
                      className="checkbox-styled"
                    />
                  </th>
                  <th className="col-key">Key</th>
                  <th className="col-title">Title</th>
                  <th className="col-type">Type</th>
                  <th className="col-priority">Priority</th>
                  <th className="col-assignee">Assignee</th>
                  <th className="col-session">Session</th>
                  <th className="col-actions"></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((ticket) => {
                  const sessionsForTicket = ticketSessions[ticket.key];
                  return (
                    <React.Fragment key={ticket.key}>
                      <tr>
                        <td>
                          <input
                            type="checkbox"
                            checked={selectedTicketKeys.has(ticket.key)}
                            onChange={() => toggleTicket(ticket.key)}
                            className="checkbox-styled"
                          />
                        </td>
                        <td>
                          <a href={ticket.jiraUrl} target="_blank" rel="noreferrer" className="ticket-key">
                            {ticket.key}
                          </a>
                        </td>
                        <td><span className="ticket-title">{ticket.title}</span></td>
                        <td><span className="ticket-type">{ticket.issueType}</span></td>
                        <td><PriorityBadge priority={ticket.priority} /></td>
                        <td>
                          {ticket.assignee ? (
                            <div className="assignee-info">
                              {ticket.assigneeAvatarUrl && <img src={ticket.assigneeAvatarUrl} alt="" />}
                              <span>{ticket.assignee}</span>
                            </div>
                          ) : <span className="unassigned">Unassigned</span>}
                        </td>
                        <td>
                          {sessionsForTicket && sessionsForTicket.length > 0 ? (
                            <Link
                              to={`/execution/${sessionsForTicket[0].sessionId}`}
                              className="session-link"
                              title={`Latest Session: ${sessionsForTicket[0].status}`}
                            >
                              <ExternalLink size={12} />
                              <span>{sessionsForTicket.length > 1 ? 'View Sessions' : 'View Session'}</span>
                            </Link>
                          ) : (
                            <span className="no-session">—</span>
                          )}
                        </td>
                        <td className="col-actions">
                          <button
                            onClick={() => setExpandedKey(expandedKey === ticket.key ? null : ticket.key)}
                            className="btn-expand"
                          >
                            {expandedKey === ticket.key ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                          </button>
                        </td>
                      </tr>
                      {expandedKey === ticket.key && (
                        <tr className="desc-row">
                          <td colSpan={8}>
                            <p className="desc-content">
                              {renderDescription(ticket.description ?? '')}
                            </p>
                          </td>
                        </tr>
                      )}
                    </React.Fragment>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Sticky bulk action bar */}
      {selectedTicketKeys.size > 0 && (
        <div className="bulk-actions-bar">
          <span className="selection-count">
            {selectedTicketKeys.size} ticket{selectedTicketKeys.size > 1 ? 's' : ''} selected
          </span>
          <button
            onClick={() => startMutation.mutate([...selectedTicketKeys])}
            disabled={startMutation.isPending}
            className="btn-start"
          >
            {startMutation.isPending ? 'Starting…' : 'Start AI Work →'}
          </button>
          <button onClick={clearSelection} className="btn-cancel">Cancel</button>
        </div>
      )}
    </Layout>
  );
}
