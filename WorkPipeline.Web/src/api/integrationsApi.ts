import api from './client';
import type { IntegrationStatus, JiraBoard, GitHubRepo, GitHubBranch } from '../types';

export const integrationsApi = {
  getStatus: () => api.get<IntegrationStatus>('/integrations/status').then(r => r.data),

  // Jira — no OAuth, just email + API token + site URL
  saveJiraCredentials: (email: string, apiToken: string) =>
    api.post('/integrations/jira/credentials', { email, apiToken }),
  saveJiraSite: (siteUrl: string) =>
    api.post('/integrations/jira/site', { siteUrl }),
  getJiraBoards: () => api.get<JiraBoard[]>('/integrations/jira/boards').then(r => r.data),
  selectJira: (boardId: string, boardName: string) =>
    api.post('/integrations/jira/select', { boardId, boardName, cloudId: '', workspaceUrl: '' }),

  // GitHub — no OAuth, just a PAT
  saveGitHubToken: (personalAccessToken: string) =>
    api.post('/integrations/github/token', { personalAccessToken }),
  getGitHubRepos: () => api.get<GitHubRepo[]>('/integrations/github/repos').then(r => r.data),
  getGitHubBranches: (owner: string, repo: string) =>
    api.get<GitHubBranch[]>(`/integrations/github/repos/${owner}/${repo}/branches`).then(r => r.data),
  selectGitHub: (repoFullName: string, branch: string, localPath: string) =>
    api.post('/integrations/github/select', { repoFullName, branch, localPath }),

  // Claude
  saveClaudeKey: (apiKey: string) =>
    api.post<{ valid: boolean }>('/integrations/claude/apikey', { apiKey }).then(r => r.data),
};
