import api from './client';
import type { TranscriptEntry } from '../types';

export interface ReviewBoard {
  awaitingReview: ReviewCard[];
  changesRequested: ReviewCard[];
  merged: ReviewCard[];
}

export interface ReviewCard {
  id: string;
  sessionId: string;
  jiraTicketKey: string;
  jiraTicketTitle: string;
  jiraIssueType: string;
  priority: string;
  assignee?: string;
  status: string;
  branchName?: string;
  pullRequestUrl?: string;
  pullRequestNumber?: number;
  updatedAt: string;
}

export const reviewApi = {
  getBoard: () => api.get<ReviewBoard>('/review/board').then(r => r.data),
  getTranscript: (ticketWorkId: string) =>
    api.get<TranscriptEntry[]>(`/review/tickets/${ticketWorkId}/transcript`).then(r => r.data),
  exportSession: (sessionId: string, format: 'json' | 'csv' = 'json') =>
    api.get(`/export/sessions/${sessionId}`, { params: { format }, responseType: 'blob' }).then(r => r.data),
};
