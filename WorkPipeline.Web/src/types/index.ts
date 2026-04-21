export interface JiraTicket {
  key: string;
  title: string;
  description?: string;
  issueType: string;
  priority: string;
  assignee?: string;
  assigneeAvatarUrl?: string;
  status: string;
  created: string;
  jiraUrl: string;
}

export interface Workspace {
  id: string;
  name: string;
  isFullyConfigured: boolean;
  hasGitHub: boolean;
  hasJira: boolean;
  hasClaude: boolean;
  gitHubRepoFullName?: string;
  gitHubBranch?: string;
  jiraSiteUrl?: string;
  jiraBoardName?: string;
  createdAt: string;
}

export interface JiraBoard {
  id: string;
  name: string;
  projectKey?: string;
  boardType?: string;
}

export interface GitHubRepo {
  fullName: string;
  name: string;
  owner: string;
  defaultBranch: string;
  private: boolean;
}

export interface GitHubBranch {
  name: string;
  isDefault: boolean;
}

export type TicketWorkStatus =
  | 'Queued' | 'InProgress' | 'VerifyingTests' | 'PushingBranch'
  | 'CreatingPR' | 'AwaitingReview' | 'ChangesRequested' | 'Merged'
  | 'Failed' | 'Skipped';

export interface TicketWork {
  id: string;
  sessionId: string;
  jiraTicketKey: string;
  jiraTicketTitle: string;
  jiraTicketDescription?: string;
  jiraIssueType: string;
  priority: string;
  assignee?: string;
  status: TicketWorkStatus;
  branchName?: string;
  pullRequestUrl?: string;
  pullRequestNumber?: number;
  retryCount: number;
  errorMessage?: string;
  queuePosition: number;
  createdAt: string;
  updatedAt: string;
  transcript?: TranscriptEntry[];
}

export interface WorkSession {
  id: string;
  name: string;
  workspaceId: string;
  totalTickets: number;
  processedTickets: number;
  createdAt: string;
  completedAt?: string;
  tickets: TicketWork[];
}

export interface TranscriptEntry {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  sequence: number;
  roundNumber: number;
  isRetryRound: boolean;
  createdAt: string;
}

// SignalR event payloads
export interface TicketStatusChangedEvent {
  ticketWorkId: string;
  jiraKey: string;
  status: TicketWorkStatus;
}

export interface ClaudeStreamChunkEvent {
  ticketWorkId: string;
  chunk: string;
  role: string;
  roundNumber: number;
}

export interface PrCreatedEvent {
  ticketWorkId: string;
  prUrl: string;
  prNumber: number;
}

export interface TestResultEvent {
  ticketWorkId: string;
  passed: boolean;
  output: string;
}

export interface SessionCompleteEvent {
  sessionId: string;
  total: number;
  succeeded: number;
  failed: number;
}
