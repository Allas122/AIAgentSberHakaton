import * as signalR from '@microsoft/signalr';
import { api } from './client';
import type {
  AiStatusEvent,
  ChatKind,
  ChatSummary,
  ConnectionState,
  FileStatusEvent,
  ManualStatusEvent,
  MessageReadyEvent,
  MessageReturn,
  ReviewFailedEvent,
  ReviewReadyEvent,
} from './types';
import { normalizeChat, normalizeManualStatus, normalizeMessage, pick } from './types';

const BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '');
const HUB_URL = `${BASE}/chat-hub`;

function browserTimeZone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
  } catch {
    return null;
  }
}

interface HubEvents {
  onState: (state: ConnectionState) => void;
  onAiStatus: (event: AiStatusEvent) => void;
  onMessageReady: (event: MessageReadyEvent) => void;
  onFileStatus: (event: FileStatusEvent) => void;
  onManualStatus: (event: ManualStatusEvent) => void;
  onReviewReady: (event: ReviewReadyEvent) => void;
  onReviewFailed: (event: ReviewFailedEvent) => void;
}

export class ChatHubClient {
  private connection: signalR.HubConnection | null = null;
  private state: ConnectionState = 'idle';
  private attempt = 0;
  private retryTimer: number | null = null;
  private stopped = false;
  private generation = 0;

  constructor(private readonly events: HubEvents) {}

  getState(): ConnectionState {
    return this.state;
  }

  private setState(next: ConnectionState) {
    if (this.state === next) return;
    this.state = next;
    this.events.onState(next);
  }

  async start(): Promise<void> {
    this.stopped = false;
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;
    await this.connect();
  }

  async stop(): Promise<void> {
    this.stopped = true;
    this.generation += 1;
    if (this.retryTimer !== null) {
      window.clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    const conn = this.connection;
    this.connection = null;
    this.setState('idle');
    await conn?.stop().catch(() => undefined);
  }

  private async connect(): Promise<void> {
    if (this.stopped) return;
    const gen = (this.generation += 1);
    const isStale = () => this.stopped || gen !== this.generation;
    this.setState('connecting');

    try {
      const ticket = await api.wsTicket();
      if (isStale()) return;

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${HUB_URL}?ticket=${encodeURIComponent(ticket)}`, {
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
        })
        .withHubProtocol(new signalR.JsonHubProtocol())
        .configureLogging(
          import.meta.env.DEV ? signalR.LogLevel.Warning : signalR.LogLevel.Error,
        )
        .build();

      connection.on('AiStatusUpdate', (payload: unknown) => {
        this.events.onAiStatus({
          chatId: pick<string>(payload, 'chatId') ?? '',
          status: pick<string>(payload, 'status') ?? '',
        });
      });

      connection.on('MessageReady', (payload: unknown) => {
        this.events.onMessageReady({
          chatId: pick<string>(payload, 'chatId') ?? '',
          message: normalizeMessage(pick<unknown>(payload, 'message')),
        });
      });

      connection.on('FileStatusChanged', (payload: unknown) => {
        this.events.onFileStatus({
          fileName: pick<string>(payload, 'fileName') ?? '',
          status: pick<string>(payload, 'status') ?? '',
        });
      });

      connection.on('ManualStatus', (payload: unknown) => {
        this.events.onManualStatus(normalizeManualStatus(payload));
      });

      connection.on('ReviewReady', (payload: unknown) => {
        this.events.onReviewReady({
          chatId: pick<string>(payload, 'chatId') ?? '',
          documentId: pick<string>(payload, 'documentId') ?? null,
          messageId: pick<string>(payload, 'messageId') ?? '',
          content: pick<string>(payload, 'content') ?? '',
        });
      });

      connection.on('ReviewFailed', (payload: unknown) => {
        this.events.onReviewFailed({
          chatId: pick<string>(payload, 'chatId') ?? '',
          fileName: pick<string>(payload, 'fileName') ?? '',
          messageId: pick<string>(payload, 'messageId') ?? '',
          reason: pick<string>(payload, 'reason') ?? '',
        });
      });

      connection.onclose(() => {
        if (isStale()) return;
        this.setState('offline');
        this.scheduleRetry();
      });

      await connection.start();

      if (isStale()) {
        await connection.stop().catch(() => undefined);
        return;
      }

      this.connection = connection;
      this.attempt = 0;
      this.setState('online');
    } catch {
      if (isStale()) return;
      this.setState('offline');
      this.scheduleRetry();
    }
  }

  private scheduleRetry() {
    if (this.stopped || this.retryTimer !== null) return;
    const delay = Math.min(1000 * 2 ** this.attempt, 15_000);
    this.attempt += 1;
    this.retryTimer = window.setTimeout(() => {
      this.retryTimer = null;
      void this.connect();
    }, delay);
  }

  private async invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    const conn = this.connection;
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Нет соединения с сервером');
    }
    return conn.invoke<T>(method, ...args);
  }

  async getChatList(
    kind: ChatKind = 'Grant',
    lastChatId: string | null = null,
    limit = 30,
  ): Promise<ChatSummary[]> {
    const raw = await this.invoke<unknown[]>('GetChatList', lastChatId, limit, kind);
    return Array.isArray(raw) ? raw.map(normalizeChat) : [];
  }

  async createChat(title: string, kind: ChatKind = 'Grant'): Promise<string> {
    return this.invoke<string>('CreateChat', { title, kind });
  }

  async getMessages(
    chatId: string,
    lastMessageId: string | null = null,
    limit = 50,
  ): Promise<unknown[]> {
    const raw = await this.invoke<unknown[]>('GetMessages', chatId, lastMessageId, limit);
    return Array.isArray(raw) ? raw : [];
  }

  async sendMessage(
    chatId: string,
    content: string,
    manualId: string | null,
  ): Promise<MessageReturn> {
    return this.invoke<MessageReturn>(
      'SendMessageToConsultingAgent',
      chatId,
      content,
      manualId,
      browserTimeZone(),
    );
  }

}
