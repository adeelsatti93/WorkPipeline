import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle, Circle, ExternalLink } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import Layout from '../components/common/Layout';
import { integrationsApi } from '../api/integrationsApi';
import type { JiraBoard, GitHubRepo, GitHubBranch } from '../types';

export default function IntegrationsPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();

  const { data: status } = useQuery({
    queryKey: ['integrations', 'status'],
    queryFn: integrationsApi.getStatus,
    refetchInterval: 3000,
  });

  // ── Jira state ───────────────────────────────────────────────────────────
  const [jiraEmail, setJiraEmail] = useState('');
  const [jiraToken, setJiraToken] = useState('');
  const [jiraSiteUrl, setJiraSiteUrl] = useState('');
  const [jiraStep, setJiraStep] = useState<'credentials' | 'site' | 'board'>('credentials');
  const [jiraBoards, setJiraBoards] = useState<JiraBoard[]>([]);
  const [selectedBoard, setSelectedBoard] = useState<JiraBoard | null>(null);

  // ── GitHub state ─────────────────────────────────────────────────────────
  const [ghToken, setGhToken] = useState('');
  const [repos, setRepos] = useState<GitHubRepo[]>([]);
  const [branches, setBranches] = useState<GitHubBranch[]>([]);
  const [selectedRepo, setSelectedRepo] = useState<GitHubRepo | null>(null);
  const [selectedBranch, setSelectedBranch] = useState('');
  const [localPath, setLocalPath] = useState('');

  // ── Claude state ─────────────────────────────────────────────────────────
  const [claudeKey, setClaudeKey] = useState('');

  // ── Jira handlers ─────────────────────────────────────────────────────────
  const saveJiraCredentials = async () => {
    if (!jiraEmail || !jiraToken) { toast.error('Enter email and token.'); return; }
    try {
      await integrationsApi.saveJiraCredentials(jiraEmail, jiraToken);
      setJiraStep('site');
      toast.success('Credentials saved!');
    } catch { toast.error('Failed to save Jira credentials.'); }
  };

  const saveJiraSite = async () => {
    if (!jiraSiteUrl) { toast.error('Enter your Jira site URL.'); return; }
    const url = jiraSiteUrl.startsWith('http') ? jiraSiteUrl : `https://${jiraSiteUrl}`;
    try {
      await integrationsApi.saveJiraSite(url);
      const boards = await integrationsApi.getJiraBoards();
      setJiraBoards(boards);
      setJiraStep('board');
    } catch { toast.error('Could not fetch boards — check your site URL and credentials.'); }
  };

  const saveJiraBoard = async () => {
    if (!selectedBoard) { toast.error('Select a board.'); return; }
    await integrationsApi.selectJira(selectedBoard.id, selectedBoard.name);
    qc.invalidateQueries({ queryKey: ['integrations', 'status'] });
    toast.success('Jira configured!');
  };

  // ── GitHub handlers ───────────────────────────────────────────────────────
  const saveGhToken = async () => {
    if (!ghToken.startsWith('ghp_') && !ghToken.startsWith('github_pat_')) {
      toast.error('Token should start with ghp_ or github_pat_');
      return;
    }
    try {
      await integrationsApi.saveGitHubToken(ghToken);
      const r = await integrationsApi.getGitHubRepos();
      setRepos(r);
      toast.success('Token saved! Select a repo below.');
    } catch { toast.error('Failed to connect to GitHub — check your token.'); }
  };

  const selectRepo = async (fullName: string) => {
    const repo = repos.find(r => r.fullName === fullName)!;
    setSelectedRepo(repo);
    const b = await integrationsApi.getGitHubBranches(repo.owner, repo.name);
    setBranches(b);
    setSelectedBranch(b.find(br => br.isDefault)?.name ?? b[0]?.name ?? '');
  };

  const saveGitHub = async () => {
    if (!selectedRepo || !selectedBranch || !localPath) {
      toast.error('Select repo, branch, and provide a local path.'); return;
    }
    await integrationsApi.selectGitHub(selectedRepo.fullName, selectedBranch, localPath);
    qc.invalidateQueries({ queryKey: ['integrations', 'status'] });
    toast.success('GitHub configured!');
  };

  // ── Claude handler ────────────────────────────────────────────────────────
  const saveClaude = async () => {
    if (!claudeKey.startsWith('sk-ant-')) { toast.error('Key must start with sk-ant-'); return; }
    await integrationsApi.saveClaudeKey(claudeKey);
    qc.invalidateQueries({ queryKey: ['integrations', 'status'] });
    setClaudeKey('');
    toast.success('Claude API key saved!');
  };

  const allConnected = status?.jiraConnected && status?.gitHubConnected && status?.claudeConnected;

  return (
    <Layout>
      <div className="integrations-page container">
        <div className="page-header">
          <h1>Integrations</h1>
          <p>Connect your tools — no app registration needed</p>
        </div>

        {/* ── Jira ──────────────────────────────────────────────────────────── */}
        <Card
          title="Jira"
          connected={status?.jiraConnected ?? false}
          connectedLabel={status?.jiraBoardName ? `Board: ${status.jiraBoardName}` : 'Connected'}
        >
          {!status?.jiraConnected && (
            <div className="card-content">
              <HelpLink
                href="https://id.atlassian.com/manage-profile/security/api-tokens"
                label="Generate Atlassian API Token →"
              />

              {jiraStep === 'credentials' && (
                <>
                  <Field label="Atlassian account email">
                    <input type="email" value={jiraEmail} onChange={e => setJiraEmail(e.target.value)}
                      placeholder="you@company.com" />
                  </Field>
                  <Field label="API Token (from id.atlassian.com)">
                    <input type="password" value={jiraToken} onChange={e => setJiraToken(e.target.value)}
                      placeholder="ATATT3xFfGF0..." />
                  </Field>
                  <button onClick={saveJiraCredentials} className="btn-primary">Next →</button>
                </>
              )}

              {jiraStep === 'site' && (
                <>
                  <Field label="Your Jira site URL">
                    <input type="text" value={jiraSiteUrl} onChange={e => setJiraSiteUrl(e.target.value)}
                      placeholder="https://yourcompany.atlassian.net" />
                  </Field>
                  <button onClick={saveJiraSite} className="btn-primary">Load Boards →</button>
                </>
              )}

              {jiraStep === 'board' && (
                <>
                  <Field label="Select board">
                    <select value={selectedBoard?.id ?? ''} onChange={e => setSelectedBoard(jiraBoards.find(b => b.id === e.target.value)!)}>
                      <option value="" disabled>Choose a board…</option>
                      {jiraBoards.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
                    </select>
                  </Field>
                  <button onClick={saveJiraBoard} disabled={!selectedBoard} className="btn-primary">
                    Save Board
                  </button>
                </>
              )}
            </div>
          )}
        </Card>

        {/* ── GitHub ────────────────────────────────────────────────────────── */}
        <Card
          title="GitHub"
          connected={status?.gitHubConnected ?? false}
          connectedLabel={status?.gitHubRepo ? `${status.gitHubRepo} @ ${status.gitHubBranch}` : 'Connected'}
        >
          {!status?.gitHubConnected && (
            <div className="card-content">
              <HelpLink
                href="https://github.com/settings/tokens/new?scopes=repo&description=Work.Pipeline"
                label="Generate GitHub Personal Access Token (repo scope) →"
              />
              <Field label="Personal Access Token">
                <input type="password" value={ghToken} onChange={e => setGhToken(e.target.value)}
                  placeholder="ghp_xxxxxxxxxxxx" />
              </Field>
              <button onClick={saveGhToken} disabled={!ghToken} className="btn-primary">
                Connect →
              </button>
            </div>
          )}

          {status?.gitHubConnected && !status?.gitHubRepo && repos.length === 0 && (
            <button onClick={() => integrationsApi.getGitHubRepos().then(setRepos)} className="btn-secondary">
              Load Repositories
            </button>
          )}

          {repos.length > 0 && !status?.gitHubRepo && (
            <div className="card-content">
              <Field label="Repository">
                <select value={selectedRepo?.fullName ?? ''} onChange={e => selectRepo(e.target.value)}>
                  <option value="" disabled>Choose a repo…</option>
                  {repos.map(r => <option key={r.fullName} value={r.fullName}>{r.fullName}{r.private ? ' 🔒' : ''}</option>)}
                </select>
              </Field>
              {branches.length > 0 && (
                <>
                  <Field label="Base branch">
                    <select value={selectedBranch} onChange={e => setSelectedBranch(e.target.value)}>
                      {branches.map(b => <option key={b.name} value={b.name}>{b.name}{b.isDefault ? ' (default)' : ''}</option>)}
                    </select>
                  </Field>
                  <Field label="Local folder to clone into">
                    <input type="text" value={localPath} onChange={e => setLocalPath(e.target.value)}
                      placeholder="C:\Users\you\repos" />
                    {selectedRepo && localPath && (
                      <p className="helper-text">
                        Will clone to: {localPath}\{selectedRepo.owner}\{selectedRepo.name}
                      </p>
                    )}
                  </Field>
                  <button onClick={saveGitHub} disabled={!selectedBranch || !localPath} className="btn-primary">
                    Save Configuration
                  </button>
                </>
              )}
            </div>
          )}
        </Card>

        {/* ── Claude ────────────────────────────────────────────────────────── */}
        <Card
          title="Claude Code"
          connected={status?.claudeConnected ?? false}
          connectedLabel="API key saved"
        >
          {!status?.claudeConnected && (
            <div className="card-content">
              <HelpLink href="https://console.anthropic.com/settings/keys" label="Get Anthropic API key →" />
              <Field label="API Key">
                <input type="password" value={claudeKey} onChange={e => setClaudeKey(e.target.value)}
                  placeholder="sk-ant-api03-..." />
              </Field>
              <button onClick={saveClaude} disabled={!claudeKey} className="btn-primary">Save Key</button>
            </div>
          )}
        </Card>

        {allConnected && (
          <button
            onClick={() => navigate('/todos')}
            className="btn-submit-all"
          >
            Start Working →
          </button>
        )}
      </div>
    </Layout>
  );
}

// ── Shared sub-components ──────────────────────────────────────────────────────

function Card({ title, connected, connectedLabel, children }: {
  title: string; connected: boolean; connectedLabel: string; children?: React.ReactNode;
}) {
  return (
    <div className="integration-card">
      <div className="card-header">
        <h2>{title}</h2>
        <span className={`status-badge ${connected ? 'connected' : 'disconnected'}`}>
          {connected ? <CheckCircle size={15} /> : <Circle size={15} />}
          {connected ? connectedLabel : 'Not connected'}
        </span>
      </div>
      {children}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="form-field">
      <label>{label}</label>
      {children}
    </div>
  );
}

function HelpLink({ href, label }: { href: string; label: string }) {
  return (
    <a href={href} target="_blank" rel="noreferrer" className="help-link">
      <ExternalLink size={12} /> {label}
    </a>
  );
}
