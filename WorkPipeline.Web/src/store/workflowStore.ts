import { create } from 'zustand';
import type { TicketWork, TicketWorkStatus } from '../types';

interface StreamChunk {
  role: string;
  content: string;
  roundNumber: number;
}

interface WorkflowState {
  selectedTicketKeys: Set<string>;
  activeSessionId: string | null;
  ticketMap: Record<string, TicketWork>;
  streamChunks: Record<string, StreamChunk[]>;
  stepProgress: Record<string, { step: string; message: string; timestamp: string }>;
  selectedStreamTicketId: string | null;

  toggleTicket: (key: string) => void;
  selectAll: (keys: string[]) => void;
  clearSelection: () => void;

  setActiveSession: (sessionId: string) => void;
  setTickets: (tickets: TicketWork[]) => void;
  updateTicketStatus: (id: string, status: TicketWorkStatus) => void;
  appendChunk: (ticketWorkId: string, chunk: StreamChunk) => void;
  setStepProgress: (ticketWorkId: string, step: string, message: string, timestamp: string) => void;
  setSelectedStreamTicket: (id: string | null) => void;
}

export const useWorkflowStore = create<WorkflowState>((set) => ({
  selectedTicketKeys: new Set(),
  activeSessionId: null,
  ticketMap: {},
  streamChunks: {},
  stepProgress: {},
  selectedStreamTicketId: null,

  toggleTicket: (key) =>
    set((s) => {
      const next = new Set(s.selectedTicketKeys);
      next.has(key) ? next.delete(key) : next.add(key);
      return { selectedTicketKeys: next };
    }),
  selectAll: (keys) => set({ selectedTicketKeys: new Set(keys) }),
  clearSelection: () => set({ selectedTicketKeys: new Set() }),

  setActiveSession: (sessionId) => set({ activeSessionId: sessionId }),
  setTickets: (tickets) =>
    set((s) => {
      const newMap = Object.fromEntries(tickets.map((t) => [t.id, t]));
      const newChunks = { ...s.streamChunks };
      tickets.forEach((t) => {
        if (t.transcript && t.transcript.length > 0 && (!newChunks[t.id] || newChunks[t.id].length === 0)) {
          newChunks[t.id] = t.transcript.map((tr) => ({
            role: tr.role,
            content: tr.content,
            roundNumber: tr.roundNumber,
          }));
        }
      });
      return { ticketMap: newMap, streamChunks: newChunks };
    }),
  updateTicketStatus: (id, status) =>
    set((s) => ({
      ticketMap: { ...s.ticketMap, [id]: { ...s.ticketMap[id], status } },
    })),
  appendChunk: (ticketWorkId, chunk) =>
    set((s) => ({
      streamChunks: {
        ...s.streamChunks,
        [ticketWorkId]: [...(s.streamChunks[ticketWorkId] ?? []), chunk],
      },
    })),
  setStepProgress: (ticketWorkId, step, message, timestamp) =>
    set((s) => ({
      stepProgress: {
        ...s.stepProgress,
        [ticketWorkId]: { step, message, timestamp },
      },
    })),
  setSelectedStreamTicket: (id) => set({ selectedStreamTicketId: id }),
}));
