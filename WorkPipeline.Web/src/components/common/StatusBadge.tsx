const statusConfig: Record<string, { label: string; cls: string }> = {
  Queued: { label: 'Queued', cls: 'queued' },
  InProgress: { label: 'In Progress', cls: 'in-progress' },
  VerifyingTests: { label: 'Testing', cls: 'testing' },
  PushingBranch: { label: 'Pushing', cls: 'pushing' },
  CreatingPR: { label: 'Creating PR', cls: 'creating-pr' },
  AwaitingReview: { label: 'Awaiting Review', cls: 'awaiting-review' },
  ChangesRequested: { label: 'Changes Requested', cls: 'changes-requested' },
  Merged: { label: 'Merged', cls: 'merged' },
  Failed: { label: 'Failed', cls: 'failed' },
  Skipped: { label: 'Skipped', cls: 'skipped' },
};

const priorityConfig: Record<string, string> = {
  Highest: 'highest',
  High: 'high',
  Medium: 'medium',
  Low: 'low',
  Lowest: 'lowest',
};

export function StatusBadge({ status }: { status: string }) {
  const cfg = statusConfig[status] ?? { label: status, cls: 'queued' };
  return (
    <span className={`status-badge ${cfg.cls}`}>
      {cfg.label}
    </span>
  );
}

export function PriorityBadge({ priority }: { priority: string }) {
  const cls = priorityConfig[priority] ?? 'lowest';
  return (
    <span className={`priority-badge ${cls}`}>
      {priority}
    </span>
  );
}

