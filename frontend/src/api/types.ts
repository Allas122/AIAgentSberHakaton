
export const UserRole = {
  None: 0,
  User: 1,
  Agent: 2,
  Coordinator: 3,
  Guest: 4,
  Rector: 5,
} as const;

export type UserRoleValue = (typeof UserRole)[keyof typeof UserRole];

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  jti: string;
}

export interface Identity {
  userId: string | null;
  name: string | null;
  role: string | null;
  expiresAt: number | null;
}

export interface TicketResponse {
  ticket: string;
}

export const ChatKind = {
  Grant: 'Grant',
  Staff: 'Staff',
} as const;

export type ChatKind = (typeof ChatKind)[keyof typeof ChatKind];

export interface ChatSummary {
  id: string;
  title: string;
  kind: ChatKind;
}

export interface MessageReturn {
  id: string;
  senderName: string;
  senderRole: UserRoleValue;
  content: string;
}

export const ManualStage = {
  Queued: 'Queued',
  Parsing: 'Parsing',
  Ready: 'Ready',
  Partial: 'Partial',
  Failed: 'Failed',
} as const;

export type ManualStage = (typeof ManualStage)[keyof typeof ManualStage];

export const MANUAL_STAGE_LABELS: Record<string, string> = {
  Queued: 'в очереди на разбор',
  Parsing: 'разбирается',
  Ready: 'разобрана',
  Partial: 'разобрана частично',
  Failed: 'разбор не удался',
};

export const ManualScope = {
  Public: 'Public',
  Staff: 'Staff',
} as const;

export type ManualScope = (typeof ManualScope)[keyof typeof ManualScope];

export interface ManualData {
  id: string;
  title: string;
  scope: ManualScope;
  stage: ManualStage;
  totalChunks: number;
  processedChunks: number;
  failedChunks: number;
  detail: string | null;
}

export interface ManualStatusEvent {
  manualId: string;
  title: string;
  stage: ManualStage;
  totalChunks: number;
  processedChunks: number;
  failedChunks: number;
  detail: string | null;
  gaps: string[];
}

export interface UploadManualResponse {
  manualId: string;
  title: string;
  stage: ManualStage;
  queueDepth: number;
  message: string;
}

export function manualPending(manual: { stage: ManualStage }): boolean {
  return manual.stage === 'Queued' || manual.stage === 'Parsing';
}

export function manualUsable(manual: { stage: ManualStage }): boolean {
  return manual.stage === 'Ready' || manual.stage === 'Partial';
}

export interface UploadFileResponse {
  messageId: string;
  applicationId: string;
  documentId: string | null;
  fileName: string;
  queueDepth: number;
}

export interface FileStatusEvent {
  fileName: string;
  status: string;
}

export interface MessageReadyEvent {
  chatId: string;
  message: ChatMessage;
}

export interface AiStatusEvent {
  chatId: string;
  status: string;
}

export interface ActiveReview {
  documentId: string;
  chatId: string;
  fileName: string;
  stage: string;
  detail: string | null;
}

export interface ReviewReadyEvent {
  chatId: string;
  documentId: string | null;
  messageId: string;
  content: string;
}

export interface ReviewFailedEvent {
  chatId: string;
  fileName: string;
  messageId: string;
  reason: string;
}

export interface MessageFile {
  documentId: string;
  fileName: string;
  reviewMessageId: string | null;
}

export interface ChatMessage {
  id: string;
  author: 'user' | 'agent';
  authorName: string;
  content: string;
  createdAt: number;
  file?: MessageFile;
  fileName?: string;
  failed?: boolean;
}

export type ConnectionState = 'idle' | 'connecting' | 'online' | 'offline';

export const AssignmentStatus = {
  New: 'New',
  InProgress: 'InProgress',
  Blocked: 'Blocked',
  Done: 'Done',
  Cancelled: 'Cancelled',
} as const;

export type AssignmentStatus = (typeof AssignmentStatus)[keyof typeof AssignmentStatus];

export const ASSIGNMENT_STATUSES: { value: AssignmentStatus; label: string }[] = [
  { value: 'New', label: 'Новое' },
  { value: 'InProgress', label: 'В работе' },
  { value: 'Blocked', label: 'Заблокировано' },
  { value: 'Done', label: 'Исполнено' },
  { value: 'Cancelled', label: 'Отменено' },
];

export const ASSIGNMENT_SOURCES: Record<string, string> = {
  Manual: 'Вручную',
  Letter: 'Из письма',
  Application: 'Из заявки',
};

export interface Assignment {
  id: string;
  ownerId: string;
  title: string;
  description: string;
  assignee: string | null;
  assigneeId: string | null;
  dueDate: string | null;
  status: AssignmentStatus;
  sourceKind: string;
  sourceRef: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AssignmentList {
  items: Assignment[];
  total: number;
}

export interface ComposedAssignment {
  id: string;
  title: string;
  assignee: string | null;
  dueDate: string | null;
  status: AssignmentStatus;
}

export const DOCUMENT_KINDS: { value: string; label: string }[] = [
  { value: 'Manual', label: 'Методички' },
  { value: 'GrantApplication', label: 'Заявки' },
  { value: 'GeneratedDocument', label: 'Сформированные' },
];

export const DOCUMENT_KIND_LABELS: Record<string, string> = {
  Manual: 'Методичка',
  GrantApplication: 'Заявка',
  GeneratedDocument: 'Документ',
};

export const REVIEW_STAGE_LABELS: Record<string, string> = {
  Queued: 'в очереди',
  Reviewing: 'проверяется',
  Reviewed: 'проверена',
  Failed: 'разбор не удался',
};

export interface DocumentReview {
  stage: string;
  detail: string | null;
  chatId: string | null;
  messageId: string | null;
  totalScore: number | null;
  maxScore: number | null;
  unverifiedCount: number | null;
  completedAt: string | null;
}

export interface StoredDocument {
  id: string;
  kind: string;
  fileName: string | null;
  sizeBytes: number;
  ownerId: string;
  chatId: string | null;
  containsPersonalData: boolean;
  createdAt: string;
  review: DocumentReview | null;
}

export interface DocumentList {
  items: StoredDocument[];
  total: number;
}

export interface DeleteDocumentResponse {
  documentId: string;
  kind: string;
  fileName: string;
  manualRemoved: boolean;
  message: string;
}

export const ASSIGNABLE_ROLES: { value: string; label: string }[] = [
  { value: 'Coordinator', label: 'Координатор' },
  { value: 'User', label: 'Сотрудник' },
  { value: 'Rector', label: 'Проректор' },
];

export const ROLE_LABELS: Record<string, string> = {
  Rector: 'Проректор',
  Coordinator: 'Координатор',
  User: 'Сотрудник',
  Guest: 'Гость',
};

export interface LetterTemplate {
  id: string;
  name: string;
  content: string;
  sourceFileName: string | null;
  createdAt: string;
  updatedAt: string;
  isPreset: boolean;
  hasForm: boolean;
  placeholders: string[];
}

export const PersonGender = {
  Unknown: 'Unknown',
  Male: 'Male',
  Female: 'Female',
} as const;

export type PersonGender = (typeof PersonGender)[keyof typeof PersonGender];

export interface LetterAddressee {
  name: string;
  position: string;
  salutation: string;
  gender: PersonGender;
}

export const EMPTY_ADDRESSEE: LetterAddressee = {
  name: '',
  position: '',
  salutation: '',
  gender: PersonGender.Unknown,
};

export interface OrganizationProfile {
  fullName: string;
  shortName: string;
  address: string;
  phone: string;
  fax: string;
  email: string;
  website: string;
  okpo: string;
  ogrn: string;
  inn: string;
  kpp: string;
  signerPosition: string;
  signerName: string;
  contactName: string;
  contactPosition: string;
  contactPhone: string;
  contactEmail: string;
  executorName: string;
  executorPhone: string;
  updatedAt: string | null;
}

export const EMPTY_ORGANIZATION_PROFILE: OrganizationProfile = {
  fullName: '',
  shortName: '',
  address: '',
  phone: '',
  fax: '',
  email: '',
  website: '',
  okpo: '',
  ogrn: '',
  inn: '',
  kpp: '',
  signerPosition: '',
  signerName: '',
  contactName: '',
  contactPosition: '',
  contactPhone: '',
  contactEmail: '',
  executorName: '',
  executorPhone: '',
  updatedAt: null,
};

export interface Account {
  id: string;
  login: string;
  name: string;
  email: string | null;
  role: string;
  isSelf: boolean;
  createdAt: string;
}

export const USAGE_OPERATIONS: Record<string, string> = {
  Consulting: 'Ответ консультанта',
  ReviewSection: 'Проверка фрагмента заявки',
  ReviewCriteria: 'Извлечение критериев',
  ReviewCoverage: 'Сверка разделов с критериями',
  ReviewMemo: 'Накопительная сводка',
  ReviewNarration: 'Пояснения к оценке',
  ManualParse: 'Разбор методички',
  Letter: 'Ответ на письмо',
  Embedding: 'Эмбеддинги',
  ApplicationReview: 'Разбор заявки целиком',
};

export interface UsageRow {
  operation: string;
  model: string;
  calls: number;
  partialCalls: number;
  failedCalls: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  duration: string;
  slowest: string;
  balanceSpent: number;
}

export interface UsageRecord {
  startedAt: string;
  operation: string;
  model: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  toolCalls: number;
  partial: boolean;
  failed: boolean;
  duration: string;
  balanceSpent: number | null;
  chatId: string | null;
}

export interface UsageRecordPage {
  items: UsageRecord[];
  total: number;
  limit: number;
  offset: number;
}

export interface UsageReport {
  from: string;
  to: string;
  rows: UsageRow[];
  totalTokens: number;
  totalDuration: string;
  balanceSpent: number;
  hasPartial: boolean;
}

export interface LetterReply {
  reply: string;
  assignments: ComposedAssignment[];
  warnings: string[];
  documentId: string | null;
  fileName: string | null;
}

export interface LetterRequisites {
  outgoingNumber: string;
  outgoingDate: string;
  replyToNumber: string;
  replyToDate: string;
}

export const EMPTY_REQUISITES: LetterRequisites = {
  outgoingNumber: '',
  outgoingDate: '',
  replyToNumber: '',
  replyToDate: '',
};

export const ContestKind = {
  Individual: 'Individual',
  University: 'University',
  Nonprofit: 'Nonprofit',
} as const;

export type ContestKind = (typeof ContestKind)[keyof typeof ContestKind];

const CLAIM_USER_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const CLAIM_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function decodeIdentity(accessToken: string): Identity {
  const empty: Identity = { userId: null, name: null, role: null, expiresAt: null };
  try {
    const payload = accessToken.split('.')[1];
    if (!payload) return empty;
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(
      decodeURIComponent(
        json
          .split('')
          .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
          .join(''),
      ),
    ) as Record<string, unknown>;

    return {
      userId: (claims[CLAIM_USER_ID] as string) ?? null,
      name: (claims.name as string) ?? null,
      role: (claims[CLAIM_ROLE] as string) ?? null,
      expiresAt: typeof claims.exp === 'number' ? claims.exp * 1000 : null,
    };
  } catch {
    return empty;
  }
}

export function pick<T>(source: unknown, ...keys: string[]): T | undefined {
  if (!source || typeof source !== 'object') return undefined;
  const record = source as Record<string, unknown>;
  for (const key of keys) {
    if (record[key] !== undefined) return record[key] as T;
    const alt = key.charAt(0).toUpperCase() + key.slice(1);
    if (record[alt] !== undefined) return record[alt] as T;
  }
  return undefined;
}

function normalizeMessageFile(raw: unknown): MessageFile | undefined {
  const file = pick<unknown>(raw, 'file');
  if (!file || typeof file !== 'object') return undefined;

  const documentId = pick<string>(file, 'documentId');
  if (!documentId) return undefined;

  return {
    documentId,
    fileName: pick<string>(file, 'fileName') || 'Документ',
    reviewMessageId: pick<string>(file, 'reviewMessageId') ?? null,
  };
}

export function normalizeMessage(raw: unknown): ChatMessage {
  const role = pick<number>(raw, 'senderRole') ?? UserRole.None;
  const isAgent = role === UserRole.Agent;
  return {
    id: pick<string>(raw, 'id') ?? crypto.randomUUID(),
    author: isAgent ? 'agent' : 'user',
    authorName: pick<string>(raw, 'senderName', 'userName') ?? (isAgent ? 'Агент' : 'Вы'),
    content: pick<string>(raw, 'content') ?? '',
    createdAt: Date.now(),
    file: normalizeMessageFile(raw),
  };
}

export function normalizeChat(raw: unknown): ChatSummary {
  return {
    id: pick<string>(raw, 'id') ?? '',
    title: pick<string>(raw, 'title') || 'Без названия',
    kind: pick<string>(raw, 'kind') === 'Staff' ? 'Staff' : 'Grant',
  };
}

export function normalizeManual(raw: unknown): ManualData {
  return {
    id: pick<string>(raw, 'id') ?? '',
    title: pick<string>(raw, 'title') || 'Методичка',
    scope: (pick<string>(raw, 'scope') as ManualScope) ?? ManualScope.Public,
    stage: (pick<string>(raw, 'stage') as ManualStage) ?? 'Ready',
    totalChunks: pick<number>(raw, 'totalChunks') ?? 0,
    processedChunks: pick<number>(raw, 'processedChunks') ?? 0,
    failedChunks: pick<number>(raw, 'failedChunks') ?? 0,
    detail: pick<string>(raw, 'detail') ?? null,
  };
}

export function normalizeManualStatus(raw: unknown): ManualStatusEvent {
  const gaps = pick<unknown>(raw, 'gaps');

  return {
    manualId: pick<string>(raw, 'manualId') ?? '',
    title: pick<string>(raw, 'title') || 'Методичка',
    stage: (pick<string>(raw, 'stage') as ManualStage) ?? 'Queued',
    totalChunks: pick<number>(raw, 'totalChunks') ?? 0,
    processedChunks: pick<number>(raw, 'processedChunks') ?? 0,
    failedChunks: pick<number>(raw, 'failedChunks') ?? 0,
    detail: pick<string>(raw, 'detail') ?? null,
    gaps: Array.isArray(gaps) ? gaps.filter((x): x is string => typeof x === 'string') : [],
  };
}
