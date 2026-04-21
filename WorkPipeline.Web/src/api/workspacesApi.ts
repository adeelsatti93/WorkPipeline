import api from './client';
import type { Workspace, GitHubRepo, GitHubBranch, JiraBoard } from '../types';

export const workspacesApi = {
  getAll: () => api.get<Workspace[]>('/workspaces').then(r => r.data),
  get: (id: string) => api.get<Workspace>(`/workspaces/${id}`).then(r => r.data),
  create: (name: string) => api.post<Workspace>('/workspaces', { name }).then(r => r.data),
  delete: (id: string) => api.delete(`/workspaces/${id}`),

  // GitHub
  getRepos: (id: string, token: string) =>
    api.get<GitHubRepo[]>(`/workspaces/${id}/github/repos`, { params: { token } }).then(r => r.data),
  getBranches: (id: string, token: string, owner: string, repo: string) =>
    api.get<GitHubBranch[]>(`/workspaces/${id}/github/repos/${owner}/${repo}/branches`, { params: { token } }).then(r => r.data),
  saveGitHub: (id: string, personalAccessToken: string, repoFullName: string, branch: string, localPath: string) =>
    api.post<Workspace>(`/workspaces/${id}/github`, { personalAccessToken, repoFullName, branch, localPath }).then(r => r.data),

  // Jira
  getJiraBoards: (id: string, email: string, apiToken: string, siteUrl: string) =>
    api.get<JiraBoard[]>(`/workspaces/${id}/jira/boards`, { params: { email, apiToken, siteUrl } }).then(r => r.data),
  getJiraStatuses: (id: string, email: string, apiToken: string, siteUrl: string, boardId: string) =>
    api.get<string[]>(`/workspaces/${id}/jira/statuses`, { params: { email, apiToken, siteUrl, boardId } }).then(r => r.data),
  saveJira: (id: string, email: string, apiToken: string, siteUrl: string, boardId: string, boardName: string, inProgressStatus?: string, mergedStatus?: string) =>
    api.post<Workspace>(`/workspaces/${id}/jira`, { email, apiToken, siteUrl, boardId, boardName, inProgressStatus, mergedStatus }).then(r => r.data),

  // Claude
  saveClaude: (id: string, apiKey: string) =>
    api.post<Workspace>(`/workspaces/${id}/claude`, { apiKey }).then(r => r.data),
};
