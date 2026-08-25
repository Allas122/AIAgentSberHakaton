import type {
  Account,
  ActiveReview,
  LetterTemplate,
  Assignment,
  AssignmentList,
  AssignmentStatus,
  ContestKind,
  DeleteDocumentResponse,
  DocumentList,
  Identity,
  LetterReply,
  ManualData,
  TicketResponse,
  TokenPair,
  UploadFileResponse,
  UploadManualResponse,
  UsageRecordPage,
  UsageReport,
} from './types';
import { decodeIdentity, normalizeManual } from './types';

const BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '');
const STORAGE_KEY = 'chatnode.auth';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

let tokens: TokenPair | null = readTokens();
let inflightAuth: Promise<TokenPair> | null = null;

function readTokens(): TokenPair | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as TokenPair) : null;
  } catch {
    return null;
  }
}

function writeTokens(next: TokenPair | null) {
  tokens = next;
  try {
    if (next) localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
  }
}

export async function login(userLogin: string, password: string): Promise<Identity> {
  const res = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ login: userLogin, password }),
  });

  if (!res.ok) {
    throw new ApiError(res.status, res.status === 401 ? 'Неверный логин или пароль' : res.statusText);
  }

  writeTokens((await res.json()) as TokenPair);
  return getIdentity();
}

export interface RegisterInput {
  login: string;
  password: string;
  name: string;
  email?: string;
}

export async function register(input: RegisterInput): Promise<Identity> {
  const res = await fetch(`${BASE}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      login: input.login,
      password: input.password,
      name: input.name,
      email: input.email || null,
    }),
  });

  if (!res.ok) await parseError(res);

  writeTokens((await res.json()) as TokenPair);
  return getIdentity();
}

export async function logout(): Promise<Identity> {
  writeTokens(null);
  await ensureAuth();
  return getIdentity();
}

export function getIdentity(): Identity {
  return tokens
    ? decodeIdentity(tokens.accessToken)
    : { userId: null, name: null, role: null, expiresAt: null };
}

function problemText(payload: unknown): string | null {
  if (!payload || typeof payload !== 'object') return null;
  const problem = payload as { title?: unknown; detail?: unknown; errors?: unknown };

  const validationMessages =
    problem.errors && typeof problem.errors === 'object'
      ? Object.values(problem.errors as Record<string, unknown>)
          .flatMap((messages) => (Array.isArray(messages) ? messages : [messages]))
          .filter((message): message is string => typeof message === 'string')
      : [];

  if (validationMessages.length > 0) return validationMessages.join(' ');
  if (typeof problem.detail === 'string' && problem.detail) return problem.detail;
  if (typeof problem.title === 'string' && problem.title) return problem.title;
  return null;
}

async function parseError(res: Response): Promise<never> {
  let detail = res.statusText;
  try {
    const text = await res.text();
    if (text) {
      let readable: string | null = null;
      try {
        readable = problemText(JSON.parse(text));
      } catch {
        readable = null;
      }
      detail = readable ?? text.slice(0, 300);
    }
  } catch {
  }
  throw new ApiError(res.status, detail || `HTTP ${res.status}`);
}

export function ensureAuth(force = false): Promise<TokenPair> {
  if (!force && tokens) return Promise.resolve(tokens);
  if (inflightAuth) return inflightAuth;

  inflightAuth = (async () => {
    if (force && tokens) {
      const res = await fetch(`${BASE}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          accessToken: tokens.accessToken,
          refreshToken: tokens.refreshToken,
        }),
      });
      if (res.ok) {
        const refreshed = (await res.json()) as TokenPair;
        writeTokens(refreshed);
        return refreshed;
      }
      writeTokens(null);
    }

    const res = await fetch(`${BASE}/api/auth/guest`, { method: 'POST' });
    if (!res.ok) await parseError(res);
    const guest = (await res.json()) as TokenPair;
    writeTokens(guest);
    return guest;
  })().finally(() => {
    inflightAuth = null;
  });

  return inflightAuth;
}

interface RequestOptions {
  method?: string;
  body?: BodyInit | null;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

async function request(path: string, options: RequestOptions = {}, retry = true): Promise<Response> {
  const auth = await ensureAuth();
  const res = await fetch(`${BASE}${path}`, {
    method: options.method ?? 'GET',
    body: options.body,
    signal: options.signal,
    headers: { ...options.headers, Authorization: `Bearer ${auth.accessToken}` },
  });

  if (res.status === 401 && retry) {
    await ensureAuth(true);
    return request(path, options, false);
  }
  if (!res.ok) await parseError(res);
  return res;
}

async function json<T>(path: string, options?: RequestOptions): Promise<T> {
  const res = await request(path, options);
  return (await res.json()) as T;
}

function fileNameFrom(header: string | null): string | null {
  if (!header) return null;

  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8) return decodeURIComponent(utf8[1]);

  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1] : null;
}

export const api = {
  async wsTicket(): Promise<string> {
    const data = await json<TicketResponse>('/api/auth/ws-ticket');
    return data.ticket;
  },

  async activeReviews(signal?: AbortSignal): Promise<ActiveReview[]> {
    const data = await json<ActiveReview[]>('/api/reviews/active', { signal });
    return Array.isArray(data) ? data : [];
  },

  async manuals(): Promise<ManualData[]> {
    const data = await json<unknown[]>('/api/manuals');
    return Array.isArray(data) ? data.map(normalizeManual) : [];
  },

  async manual(manualId: string, signal?: AbortSignal): Promise<ManualData> {
    return normalizeManual(await json<unknown>(`/api/manuals/${manualId}`, { signal }));
  },

  async composeLetterReply(
    file: File | null,
    letter: string,
    intent: string,
    templateId?: string | null,
    signal?: AbortSignal,
  ): Promise<LetterReply> {
    const form = new FormData();
    if (file) form.append('File', file);
    if (letter.trim()) form.append('Text', letter.trim());
    if (intent.trim()) form.append('Intent', intent.trim());
    if (templateId) form.append('TemplateId', templateId);

    return json<LetterReply>('/api/letters/reply', {
      method: 'POST',
      body: form,
      signal,
    });
  },

  async assignments(
    status: AssignmentStatus | null,
    signal?: AbortSignal,
  ): Promise<AssignmentList> {
    const query = status ? `?status=${status}` : '';
    return json<AssignmentList>(`/api/assignments${query}`, { signal });
  },

  async createAssignment(
    input: {
      title: string;
      description?: string;
      assignee?: string;
      assigneeId?: string | null;
      dueDate?: string;
    },
    signal?: AbortSignal,
  ): Promise<Assignment> {
    return json<Assignment>('/api/assignments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: input.title,
        description: input.description ?? '',
        assignee: input.assignee || null,
        assigneeId: input.assigneeId || null,
        dueDate: input.dueDate || null,
      }),
      signal,
    });
  },

  async updateAssignment(
    id: string,
    patch: Partial<{
      title: string;
      description: string;
      assignee: string;
      assigneeId: string;
      dueDate: string;
      status: AssignmentStatus;
    }>,
    signal?: AbortSignal,
  ): Promise<Assignment> {
    return json<Assignment>(`/api/assignments/${id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(patch),
      signal,
    });
  },

  async deleteAssignment(id: string, signal?: AbortSignal): Promise<void> {
    await request(`/api/assignments/${id}`, { method: 'DELETE', signal });
  },

  async letterTemplates(signal?: AbortSignal): Promise<LetterTemplate[]> {
    return json<LetterTemplate[]>('/api/letter-templates', { signal });
  },

  async createLetterTemplate(
    name: string,
    content: string,
    file: File | null,
    signal?: AbortSignal,
  ): Promise<LetterTemplate> {
    const form = new FormData();
    form.append('Name', name);
    if (file) form.append('File', file);
    else form.append('Content', content);

    return json<LetterTemplate>('/api/letter-templates', { method: 'POST', body: form, signal });
  },

  async updateLetterTemplate(
    id: string,
    patch: Partial<{ name: string; content: string }>,
    signal?: AbortSignal,
  ): Promise<LetterTemplate> {
    return json<LetterTemplate>(`/api/letter-templates/${id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(patch),
      signal,
    });
  },

  async deleteLetterTemplate(id: string, signal?: AbortSignal): Promise<void> {
    await request(`/api/letter-templates/${id}`, { method: 'DELETE', signal });
  },

  async accounts(signal?: AbortSignal): Promise<Account[]> {
    return json<Account[]>('/api/accounts', { signal });
  },

  async curators(signal?: AbortSignal): Promise<Account[]> {
    return json<Account[]>('/api/accounts/curators', { signal });
  },

  async usage(days: number, signal?: AbortSignal): Promise<UsageReport> {
    const from = new Date(Date.now() - days * 86400_000).toISOString();
    return json<UsageReport>(`/api/usage?from=${encodeURIComponent(from)}`, { signal });
  },

  async usageRecords(
    input: { days: number; operation?: string | null; limit: number; offset: number },
    signal?: AbortSignal,
  ): Promise<UsageRecordPage> {
    const params = new URLSearchParams({
      from: new Date(Date.now() - input.days * 86400_000).toISOString(),
      limit: String(input.limit),
      offset: String(input.offset),
    });

    if (input.operation) params.set('operation', input.operation);

    return json<UsageRecordPage>(`/api/usage/records?${params}`, { signal });
  },

  async clearUsage(days: number, signal?: AbortSignal): Promise<{ removed: number }> {
    const from = new Date(Date.now() - days * 86400_000).toISOString();
    return json<{ removed: number }>(`/api/usage?from=${encodeURIComponent(from)}`, {
      method: 'DELETE',
      signal,
    });
  },

  async createAccount(
    input: { login: string; password: string; name: string; email?: string; role: string },
    signal?: AbortSignal,
  ): Promise<Account> {
    return json<Account>('/api/accounts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        login: input.login,
        password: input.password,
        name: input.name,
        email: input.email || null,
        role: input.role,
      }),
      signal,
    });
  },

  async updateAccount(
    id: string,
    patch: Partial<{ name: string; email: string; role: string; password: string }>,
    signal?: AbortSignal,
  ): Promise<Account> {
    return json<Account>(`/api/accounts/${id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(patch),
      signal,
    });
  },

  async deleteAccount(id: string, signal?: AbortSignal): Promise<void> {
    await request(`/api/accounts/${id}`, { method: 'DELETE', signal });
  },

  async documents(kind: string | null, signal?: AbortSignal): Promise<DocumentList> {
    const query = kind ? `?kind=${kind}` : '';
    return json<DocumentList>(`/api/documents${query}`, { signal });
  },

  async deleteDocument(documentId: string, signal?: AbortSignal): Promise<DeleteDocumentResponse> {
    return json<DeleteDocumentResponse>(`/api/documents/${documentId}`, {
      method: 'DELETE',
      signal,
    });
  },

  async downloadDocument(documentId: string, signal?: AbortSignal): Promise<void> {
    const res = await request(`/api/documents/${documentId}/content`, { signal });

    const blob = await res.blob();
    const name = fileNameFrom(res.headers.get('content-disposition')) ?? 'document';

    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },

  async deleteChat(chatId: string, signal?: AbortSignal): Promise<void> {
    await request(`/api/chat/${chatId}`, { method: 'DELETE', signal });
  },

  async downloadReview(chatId: string, messageId: string, signal?: AbortSignal): Promise<void> {
    const res = await request(
      `/api/chat/${chatId}/messages/${encodeURIComponent(messageId)}/review.docx`,
      { signal },
    );

    const blob = await res.blob();
    const name = fileNameFrom(res.headers.get('content-disposition')) ?? 'razbor-zayavki.docx';

    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },

  async uploadManual(
    title: string,
    file: File,
    signal?: AbortSignal,
  ): Promise<UploadManualResponse> {
    const form = new FormData();
    form.append('Title', title);
    form.append('File', file);
    return json<UploadManualResponse>('/api/manuals/upload', {
      method: 'POST',
      body: form,
      signal,
    });
  },

  async uploadApplication(
    chatId: string,
    manualId: string,
    file: File,
    content: string | undefined,
    contestKind: ContestKind,
    signal?: AbortSignal,
  ): Promise<UploadFileResponse> {
    const form = new FormData();
    form.append('ManualId', manualId);
    if (content) form.append('Content', content);
    form.append('ContestKind', contestKind);
    form.append('File', file);
    return json<UploadFileResponse>(`/api/chat/${chatId}/files`, {
      method: 'POST',
      body: form,
      signal,
    });
  },
};
