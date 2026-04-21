import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus, Trash2, CheckCircle, Circle, ExternalLink, ChevronRight, X, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import Layout from '../components/common/Layout';
import SearchableSelect from '../components/common/SearchableSelect';
import ConfirmModal from '../components/common/ConfirmModal';
import { workspacesApi } from '../api/workspacesApi';
import type { Workspace, GitHubRepo, GitHubBranch, JiraBoard } from '../types';

type DrawerStep = 'name' | 'github' | 'jira' | 'claude' | 'done';

interface DrawerState {
  workspaceId: string | null;
  step: DrawerStep;
  // github
  ghToken: string;
  repos: GitHubRepo[];
  selectedRepo: GitHubRepo | null;
  branches: GitHubBranch[];
  selectedBranch: string;
  localPath: string;
  // jira
  jiraEmail: string;
  jiraToken: string;
  jiraSiteUrl: string;
  jiraBoards: JiraBoard[];
  selectedBoard: JiraBoard | null;
  // claude
  claudeKey: string;
}

const EMPTY_DRAWER: DrawerState = {
  workspaceId: null,
  step: 'name',
  ghToken: '', repos: [], selectedRepo: null, branches: [], selectedBranch: '', localPath: '',
  jiraEmail: '', jiraToken: '', jiraSiteUrl: '', jiraBoards: [], selectedBoard: null,
  claudeKey: '',
};

export default function WorkspacesPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [drawer, setDrawer] = useState<DrawerState | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Workspace | null>(null);
  const [newName, setNewName] = useState('');
  const [busy, setBusy] = useState(false);

  const { data: workspaces = [], isLoading } = useQuery({
    queryKey: ['workspaces'],
    queryFn: workspacesApi.getAll,
  });

  const deleteMutation = useMutation({
    mutationFn: workspacesApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['workspaces'] }),
    onError: () => toast.error('Failed to delete workspace.'),
  });

  const openCreate = () => {
    setNewName('');
    setDrawer({ ...EMPTY_DRAWER });
  };

  const closeDrawer = () => setDrawer(null);

  // ── Step: name ────────────────────────────────────────────────────────────
  const handleCreateName = async () => {
    if (!newName.trim()) { toast.error('Enter a workspace name.'); return; }
    setBusy(true);
    try {
      const ws = await workspacesApi.create(newName.trim());
      qc.invalidateQueries({ queryKey: ['workspaces'] });
      setDrawer(d => d ? { ...d, workspaceId: ws.id, step: 'github' } : d);
    } catch { toast.error('Failed to create workspace.'); }
    finally { setBusy(false); }
  };

  // ── Step: GitHub ──────────────────────────────────────────────────────────
  const handleLoadRepos = async () => {
    if (!drawer?.workspaceId) return;
    if (!drawer.ghToken) { toast.error('Enter your GitHub token.'); return; }
    setBusy(true);
    try {
      const repos = await workspacesApi.getRepos(drawer.workspaceId, drawer.ghToken);
      setDrawer(d => d ? { ...d, repos, selectedRepo: null, branches: [], selectedBranch: '' } : d);
    } catch { toast.error('Invalid token or network error.'); }
    finally { setBusy(false); }
  };

  const handleSelectRepo = async (fullName: string) => {
    if (!drawer?.workspaceId) return;
    const repo = drawer.repos.find(r => r.fullName === fullName)!;
    setBusy(true);
    try {
      const branches = await workspacesApi.getBranches(drawer.workspaceId, drawer.ghToken, repo.owner, repo.name);
      const defaultBranch = branches.find(b => b.isDefault)?.name ?? branches[0]?.name ?? '';
      setDrawer(d => d ? { ...d, selectedRepo: repo, branches, selectedBranch: defaultBranch } : d);
    } catch { toast.error('Failed to load branches.'); }
    finally { setBusy(false); }
  };

  const handleSaveGitHub = async () => {
    if (!drawer?.workspaceId || !drawer.selectedRepo || !drawer.selectedBranch || !drawer.localPath) {
      toast.error('Select repo, branch, and enter local path.'); return;
    }
    setBusy(true);
    try {
      await workspacesApi.saveGitHub(drawer.workspaceId, drawer.ghToken, drawer.selectedRepo.fullName, drawer.selectedBranch, drawer.localPath);
      qc.invalidateQueries({ queryKey: ['workspaces'] });
      setDrawer(d => d ? { ...d, step: 'jira' } : d);
      toast.success('GitHub configured!');
    } catch { toast.error('Failed to save GitHub settings.'); }
    finally { setBusy(false); }
  };

  // ── Step: Jira ────────────────────────────────────────────────────────────
  const handleLoadBoards = async () => {
    if (!drawer?.workspaceId) return;
    if (!drawer.jiraEmail || !drawer.jiraToken || !drawer.jiraSiteUrl) {
      toast.error('Fill in all Jira fields.'); return;
    }
    const url = drawer.jiraSiteUrl.startsWith('http') ? drawer.jiraSiteUrl : `https://${drawer.jiraSiteUrl}`;
    setBusy(true);
    try {
      const boards = await workspacesApi.getJiraBoards(drawer.workspaceId, drawer.jiraEmail, drawer.jiraToken, url);
      setDrawer(d => d ? { ...d, jiraBoards: boards, jiraSiteUrl: url, selectedBoard: null } : d);
      if (boards.length === 0) toast('No boards found for this site.', { icon: '⚠️' });
    } catch { toast.error('Could not connect to Jira. Check your credentials and site URL.'); }
    finally { setBusy(false); }
  };

  const handleSaveJira = async () => {
    if (!drawer?.workspaceId || !drawer.selectedBoard) { toast.error('Select a board.'); return; }
    setBusy(true);
    try {
      await workspacesApi.saveJira(drawer.workspaceId, drawer.jiraEmail, drawer.jiraToken, drawer.jiraSiteUrl, drawer.selectedBoard.id, drawer.selectedBoard.name);
      qc.invalidateQueries({ queryKey: ['workspaces'] });
      setDrawer(d => d ? { ...d, step: 'claude' } : d);
      toast.success('Jira configured!');
    } catch { toast.error('Failed to save Jira settings.'); }
    finally { setBusy(false); }
  };

  // ── Step: Claude ──────────────────────────────────────────────────────────
  const handleSaveClaude = async () => {
    if (!drawer?.workspaceId) return;
    if (!drawer.claudeKey.startsWith('sk-ant-')) { toast.error('Key must start with sk-ant-'); return; }
    setBusy(true);
    try {
      await workspacesApi.saveClaude(drawer.workspaceId, drawer.claudeKey);
      qc.invalidateQueries({ queryKey: ['workspaces'] });
      setDrawer(d => d ? { ...d, step: 'done' } : d);
      toast.success('Workspace ready!');
    } catch { toast.error('Failed to save Claude key.'); }
    finally { setBusy(false); }
  };

  return (
    <Layout>
      <div className="workspaces-page container">
        <div className="page-header">
          <div>
            <h1>Workspaces</h1>
            <p>Each workspace connects one GitHub repo + one Jira board + Claude.</p>
          </div>
          <button onClick={openCreate} className="btn-primary">
            <Plus size={16} /> New Workspace
          </button>
        </div>

        {isLoading ? (
          <div className="loading-state">Loading workspaces…</div>
        ) : workspaces.length === 0 ? (
          <div className="empty-workspaces">
            <div className="empty-icon">⚡</div>
            <h2>No workspaces yet</h2>
            <p>Create your first workspace to start automating tickets.</p>
            <button onClick={openCreate} className="btn-primary">
              <Plus size={16} /> Create Workspace
            </button>
          </div>
        ) : (
          <div className="workspaces-grid">
            {workspaces.map(ws => (
              <WorkspaceCard
                key={ws.id}
                workspace={ws}
                onDelete={() => setDeleteTarget(ws)}
                onStart={() => navigate(`/todos?workspaceId=${ws.id}`)}
              />
            ))}
          </div>
        )}
      </div>

      {deleteTarget && (
        <ConfirmModal
          title="Delete Workspace"
          message={`"${deleteTarget.name}" and all its sessions will be permanently deleted. This cannot be undone.`}
          confirmLabel="Delete"
          variant="danger"
          onConfirm={() => { deleteMutation.mutate(deleteTarget.id); setDeleteTarget(null); }}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {drawer && (
        <div className="drawer-overlay" onClick={closeDrawer}>
          <div className="drawer-panel" onClick={e => e.stopPropagation()}>
            <div className="drawer-header">
              <h2>{drawer.step === 'name' ? 'New Workspace' : `Setup: ${newName}`}</h2>
              <button onClick={closeDrawer} className="drawer-close"><X size={18} /></button>
            </div>

            <StepIndicator step={drawer.step} />

            <div className="drawer-body">
              {drawer.step === 'name' && (
                <NameStep
                  name={newName}
                  onNameChange={setNewName}
                  onNext={handleCreateName}
                  busy={busy}
                />
              )}
              {drawer.step === 'github' && (
                <GitHubStep
                  state={drawer}
                  onChange={(patch) => setDrawer(d => d ? { ...d, ...patch } : d)}
                  onLoadRepos={handleLoadRepos}
                  onSelectRepo={handleSelectRepo}
                  onSave={handleSaveGitHub}
                  busy={busy}
                />
              )}
              {drawer.step === 'jira' && (
                <JiraStep
                  state={drawer}
                  onChange={(patch) => setDrawer(d => d ? { ...d, ...patch } : d)}
                  onLoadBoards={handleLoadBoards}
                  onSave={handleSaveJira}
                  busy={busy}
                />
              )}
              {drawer.step === 'claude' && (
                <ClaudeStep
                  claudeKey={drawer.claudeKey}
                  onChange={(claudeKey) => setDrawer(d => d ? { ...d, claudeKey } : d)}
                  onSave={handleSaveClaude}
                  busy={busy}
                />
              )}
              {drawer.step === 'done' && (
                <DoneStep
                  workspaceId={drawer.workspaceId!}
                  onClose={closeDrawer}
                  onGoToTodos={() => { closeDrawer(); navigate(`/todos?workspaceId=${drawer.workspaceId}`); }}
                />
              )}
            </div>
          </div>
        </div>
      )}
    </Layout>
  );
}

// ── Workspace Card ─────────────────────────────────────────────────────────────

function WorkspaceCard({ workspace, onDelete, onStart }: {
  workspace: Workspace;
  onDelete: () => void;
  onStart: () => void;
}) {
  return (
    <div className="workspace-card">
      <div className="wc-header">
        <h3>{workspace.name}</h3>
        <button onClick={onDelete} className="btn-icon-danger" title="Delete workspace">
          <Trash2 size={15} />
        </button>
      </div>

      <div className="wc-integrations">
        <IntegrationRow
          label="GitHub"
          connected={workspace.hasGitHub}
          detail={workspace.gitHubRepoFullName
            ? `${workspace.gitHubRepoFullName} @ ${workspace.gitHubBranch}`
            : undefined}
        />
        <IntegrationRow
          label="Jira"
          connected={workspace.hasJira}
          detail={workspace.jiraBoardName ?? undefined}
        />
        <IntegrationRow
          label="Claude"
          connected={workspace.hasClaude}
        />
      </div>

      <div className="wc-footer">
        {workspace.isFullyConfigured ? (
          <button onClick={onStart} className="btn-start">
            Start Working <ChevronRight size={14} />
          </button>
        ) : (
          <span className="wc-incomplete">Configure all 3 integrations to start</span>
        )}
      </div>
    </div>
  );
}

function IntegrationRow({ label, connected, detail }: { label: string; connected: boolean; detail?: string }) {
  return (
    <div className={`integration-row ${connected ? 'connected' : 'pending'}`}>
      {connected ? <CheckCircle size={14} /> : <Circle size={14} />}
      <span className="int-label">{label}</span>
      {detail && <span className="int-detail">{detail}</span>}
    </div>
  );
}

// ── Step Indicator ─────────────────────────────────────────────────────────────

const STEPS: { key: DrawerStep; label: string }[] = [
  { key: 'name', label: 'Name' },
  { key: 'github', label: 'GitHub' },
  { key: 'jira', label: 'Jira' },
  { key: 'claude', label: 'Claude' },
  { key: 'done', label: 'Done' },
];

function StepIndicator({ step }: { step: DrawerStep }) {
  const current = STEPS.findIndex(s => s.key === step);
  return (
    <div className="step-indicator">
      {STEPS.map((s, i) => (
        <div key={s.key} className={`step-item ${i < current ? 'done' : i === current ? 'active' : ''}`}>
          <div className="step-dot">{i < current ? '✓' : i + 1}</div>
          <span>{s.label}</span>
          {i < STEPS.length - 1 && <div className="step-line" />}
        </div>
      ))}
    </div>
  );
}

// ── Step Components ────────────────────────────────────────────────────────────

function NameStep({ name, onNameChange, onNext, busy }: {
  name: string; onNameChange: (v: string) => void; onNext: () => void; busy: boolean;
}) {
  return (
    <div className="step-content">
      <p className="step-desc">Give this workspace a memorable name.</p>
      <div className="form-field">
        <label>Workspace Name</label>
        <input
          type="text"
          value={name}
          onChange={e => onNameChange(e.target.value)}
          placeholder="e.g. My React App"
          onKeyDown={e => e.key === 'Enter' && onNext()}
          autoFocus
        />
      </div>
      <button onClick={onNext} disabled={busy || !name.trim()} className="btn-primary">
        {busy ? <Loader2 size={14} className="spin" /> : null} Create & Continue →
      </button>
    </div>
  );
}

function GitHubStep({ state, onChange, onLoadRepos, onSelectRepo, onSave, busy }: {
  state: DrawerState;
  onChange: (patch: Partial<DrawerState>) => void;
  onLoadRepos: () => void;
  onSelectRepo: (fullName: string) => void;
  onSave: () => void;
  busy: boolean;
}) {
  const repoOptions = state.repos.map(r => ({
    value: r.fullName,
    label: r.fullName,
    sublabel: r.private ? '🔒 private' : undefined,
  }));

  const branchOptions = state.branches.map(b => ({
    value: b.name,
    label: b.name,
    badge: b.isDefault ? 'default' : undefined,
  }));

  return (
    <div className="step-content">
      <p className="step-desc">
        Connect a GitHub repository. Generate a PAT with <code>repo</code> scope.
      </p>
      <a href="https://github.com/settings/tokens/new?scopes=repo&description=Work.Pipeline"
        target="_blank" rel="noreferrer" className="help-link">
        <ExternalLink size={12} /> Generate GitHub Token →
      </a>

      <div className="form-field">
        <label>Personal Access Token</label>
        <div className="input-row">
          <input
            type="password"
            value={state.ghToken}
            onChange={e => onChange({ ghToken: e.target.value })}
            placeholder="ghp_xxxxxxxxxxxx"
          />
          <button onClick={onLoadRepos} disabled={busy || !state.ghToken} className="btn-secondary">
            {busy ? <Loader2 size={13} className="spin" /> : 'Load Repos'}
          </button>
        </div>
      </div>

      {state.repos.length > 0 && (
        <div className="form-field">
          <label>Repository ({state.repos.length} found)</label>
          <SearchableSelect
            options={repoOptions}
            value={state.selectedRepo?.fullName ?? ''}
            onChange={onSelectRepo}
            placeholder="Search repositories…"
          />
        </div>
      )}

      {state.branches.length > 0 && (
        <>
          <div className="form-field">
            <label>Base Branch</label>
            <SearchableSelect
              options={branchOptions}
              value={state.selectedBranch}
              onChange={v => onChange({ selectedBranch: v })}
              placeholder="Search branches…"
            />
          </div>
          <div className="form-field">
            <label>Local Folder to Clone Into</label>
            <input
              type="text"
              value={state.localPath}
              onChange={e => onChange({ localPath: e.target.value })}
              placeholder="C:\Users\you\repos"
            />
            {state.selectedRepo && state.localPath && (
              <p className="helper-text">
                Will clone to: {state.localPath}\{state.selectedRepo.owner}\{state.selectedRepo.name}
              </p>
            )}
          </div>
        </>
      )}

      <button
        onClick={onSave}
        disabled={busy || !state.selectedRepo || !state.selectedBranch || !state.localPath}
        className="btn-primary"
      >
        {busy ? <Loader2 size={14} className="spin" /> : null} Save GitHub →
      </button>
    </div>
  );
}

function JiraStep({ state, onChange, onLoadBoards, onSave, busy }: {
  state: DrawerState;
  onChange: (patch: Partial<DrawerState>) => void;
  onLoadBoards: () => void;
  onSave: () => void;
  busy: boolean;
}) {
  const boardOptions = state.jiraBoards.map(b => ({
    value: b.id,
    label: b.name,
    sublabel: b.projectKey ?? undefined,
    badge: b.boardType ?? undefined,
  }));

  return (
    <div className="step-content">
      <p className="step-desc">Connect Jira using your Atlassian email and API token.</p>
      <a href="https://id.atlassian.com/manage-profile/security/api-tokens"
        target="_blank" rel="noreferrer" className="help-link">
        <ExternalLink size={12} /> Generate Atlassian API Token →
      </a>

      <div className="form-field">
        <label>Atlassian Email</label>
        <input type="email" value={state.jiraEmail}
          onChange={e => onChange({ jiraEmail: e.target.value })}
          placeholder="you@company.com" />
      </div>
      <div className="form-field">
        <label>API Token</label>
        <input type="password" value={state.jiraToken}
          onChange={e => onChange({ jiraToken: e.target.value })}
          placeholder="ATATT3xFfGF0…" />
      </div>
      <div className="form-field">
        <label>Jira Site URL</label>
        <div className="input-row">
          <input type="text" value={state.jiraSiteUrl}
            onChange={e => onChange({ jiraSiteUrl: e.target.value })}
            placeholder="https://yourcompany.atlassian.net" />
          <button
            onClick={onLoadBoards}
            disabled={busy || !state.jiraEmail || !state.jiraToken || !state.jiraSiteUrl}
            className="btn-secondary"
          >
            {busy ? <Loader2 size={13} className="spin" /> : 'Load Boards'}
          </button>
        </div>
      </div>

      {state.jiraBoards.length > 0 && (
        <div className="form-field">
          <label>Select Board ({state.jiraBoards.length} found)</label>
          <SearchableSelect
            options={boardOptions}
            value={state.selectedBoard?.id ?? ''}
            onChange={v => onChange({ selectedBoard: state.jiraBoards.find(b => b.id === v) ?? null })}
            placeholder="Search boards by name or project key…"
            emptyMessage="No boards match your search"
          />
        </div>
      )}

      <button onClick={onSave} disabled={busy || !state.selectedBoard} className="btn-primary">
        {busy ? <Loader2 size={14} className="spin" /> : null} Save Jira →
      </button>
    </div>
  );
}

function ClaudeStep({ claudeKey, onChange, onSave, busy }: {
  claudeKey: string; onChange: (v: string) => void; onSave: () => void; busy: boolean;
}) {
  return (
    <div className="step-content">
      <p className="step-desc">Enter your Anthropic API key so Claude Code can work autonomously.</p>
      <a href="https://console.anthropic.com/settings/keys" target="_blank" rel="noreferrer" className="help-link">
        <ExternalLink size={12} /> Get Anthropic API Key →
      </a>
      <div className="form-field">
        <label>API Key</label>
        <input type="password" value={claudeKey} onChange={e => onChange(e.target.value)}
          placeholder="sk-ant-api03-…" />
      </div>
      <button onClick={onSave} disabled={busy || !claudeKey} className="btn-primary">
        {busy ? <Loader2 size={14} className="spin" /> : null} Save Key →
      </button>
    </div>
  );
}

function DoneStep({ workspaceId, onClose, onGoToTodos }: {
  workspaceId: string; onClose: () => void; onGoToTodos: () => void;
}) {
  return (
    <div className="step-content done-step">
      <div className="done-icon">✅</div>
      <h3>Workspace Ready!</h3>
      <p>All three integrations are configured. You can now select tickets and start AI work.</p>
      <div className="done-actions">
        <button onClick={onGoToTodos} className="btn-primary">
          Go to Todo Board <ChevronRight size={14} />
        </button>
        <button onClick={onClose} className="btn-secondary">Close</button>
      </div>
    </div>
  );
}
