import api from './client';
import type { JiraTicket } from '../types';

export const ticketsApi = {
  getTodoTickets: (workspaceId: string, maxResults = 100) =>
    api.get<JiraTicket[]>('/tickets/todo', { params: { workspaceId, maxResults } }).then(r => r.data),
  getTicket: (key: string, workspaceId: string) =>
    api.get<JiraTicket>(`/tickets/${key}`, { params: { workspaceId } }).then(r => r.data),
};
