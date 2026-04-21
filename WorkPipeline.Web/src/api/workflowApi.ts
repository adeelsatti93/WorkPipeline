import api from './client';
import type { WorkSession } from '../types';

export interface TicketSessionInfo {
  sessionId: string;
  status: string;
  createdAt: string;
}

export const workflowApi = {
  startWorkflow: (workspaceId: string, ticketKeys: string[]) =>
    api.post<{ sessionId: string }>('/workflow/start', { workspaceId, ticketKeys }).then(r => r.data),
  getSessions: () => api.get<WorkSession[]>('/workflow/sessions').then(r => r.data),
  getSession: (sessionId: string) =>
    api.get<WorkSession>(`/workflow/sessions/${sessionId}`).then(r => r.data),
  getSessionsByTicketKeys: (ticketKeys: string[]) =>
    api.post<Record<string, TicketSessionInfo[]>>('/workflow/sessions/by-tickets', ticketKeys).then(r => r.data),
  retryTicket: (ticketWorkId: string) =>
    api.post(`/workflow/tickets/${ticketWorkId}/retry`),
  deleteSession: (sessionId: string) =>
    api.delete(`/workflow/sessions/${sessionId}`),
};
