import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useWorkflowStore } from '../store/workflowStore';
import type {
  TicketStatusChangedEvent, ClaudeStreamChunkEvent,
  PrCreatedEvent, TestResultEvent, SessionCompleteEvent
} from '../types';

interface StepProgressEvent {
  ticketWorkId: string;
  step: string;
  message: string;
  timestamp: string;
}

export function useWorkflowStream(sessionId: string | null) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const { updateTicketStatus, appendChunk, setStepProgress } = useWorkflowStore();

  useEffect(() => {
    if (!sessionId) return;
    const token = localStorage.getItem('token');

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/workflow', { accessTokenFactory: () => token ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('TicketStatusChanged', (e: TicketStatusChangedEvent) => {
      updateTicketStatus(e.ticketWorkId, e.status);
    });

    connection.on('ClaudeStreamChunk', (e: ClaudeStreamChunkEvent) => {
      appendChunk(e.ticketWorkId, { role: e.role, content: e.chunk, roundNumber: e.roundNumber });
    });

    connection.on('StepProgress', (e: StepProgressEvent) => {
      setStepProgress(e.ticketWorkId, e.step, e.message, e.timestamp);
    });

    connection.on('PrCreated', (e: PrCreatedEvent) => {
      if ('Notification' in window && Notification.permission === 'granted') {
        new Notification('PR Ready for Review', {
          body: `Pull request #${e.prNumber} is ready for review.`,
          icon: '/icon-192.png',
        });
      }
    });

    connection.on('TestResultReceived', (_e: TestResultEvent) => { /* status already updated */ });
    connection.on('SessionComplete', (_e: SessionCompleteEvent) => { /* handled by query refetch */ });

    connection.start()
      .then(() => connection.invoke('JoinSession', sessionId))
      .catch(console.error);

    connectionRef.current = connection;
    return () => {
      connection.invoke('LeaveSession', sessionId).catch(() => {});
      connection.stop();
    };
  }, [sessionId]);
}
