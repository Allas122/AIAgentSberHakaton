import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  api,
  ApiError,
  ensureAuth,
  getIdentity,
  login as apiLogin,
  logout as apiLogout,
  register as apiRegister,
} from '../api/client';
import type { RegisterInput } from '../api/client';
import { ChatHubClient } from '../api/hub';
import type {
  ChatKind,
  ChatMessage,
  ChatSummary,
  ConnectionState,
  ContestKind,
  Identity,
  ManualData,
  ManualStatusEvent,
} from '../api/types';
import { manualPending, manualUsable, normalizeMessage } from '../api/types';

const MANUAL_KEY = 'chatnode.manual';
const CONTEST_KEY = 'chatnode.contest';
const MANUAL_POLL_MS = 7000;

interface Options {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

const HUB_MARKER = 'HubException: ';

function errorText(e: unknown): string {
  if (e instanceof ApiError) return `Сервер ответил ${e.status}: ${e.message}`;
  if (!(e instanceof Error)) return 'Неизвестная ошибка';

  const marker = e.message.lastIndexOf(HUB_MARKER);
  return marker >= 0 ? e.message.slice(marker + HUB_MARKER.length) : e.message;
}

function titleFrom(text: string): string {
  const flat = text.replace(/\s+/g, ' ').trim();
  if (flat.length <= 48) return flat || 'Новый чат';
  return `${flat.slice(0, 48).replace(/\s\S*$/, '')}…`;
}

export function useChat({ notify }: Options) {
  const [connection, setConnection] = useState<ConnectionState>('idle');
  const [chats, setChats] = useState<ChatSummary[]>([]);
  const [staffChats, setStaffChats] = useState<ChatSummary[]>([]);
  const [activeChatId, setActiveChatId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [manuals, setManuals] = useState<ManualData[]>([]);
  const [manualId, setManualIdState] = useState<string | null>(
    () => localStorage.getItem(MANUAL_KEY),
  );
  const [contestKind] = useState<ContestKind>(
    () => (localStorage.getItem(CONTEST_KEY) as ContestKind) || 'Individual',
  );
  const [draftKind, setDraftKind] = useState<ChatKind>('Grant');
  const [statusByChat, setStatusByChat] = useState<Record<string, string>>({});
  const [pendingByChat, setPendingByChat] = useState<Record<string, true>>({});
  const [busy, setBusy] = useState(false);
  const [loadingThread, setLoadingThread] = useState(false);
  const [identity, setIdentity] = useState<Identity>(getIdentity);

  const hubRef = useRef<ChatHubClient | null>(null);
  const threadRef = useRef<string | null>(null);
  const notifyRef = useRef(notify);
  notifyRef.current = notify;

  const isStaffRef = useRef(false);
  isStaffRef.current = identity.role === 'Rector' || identity.role === 'Coordinator';

  const markPending = useCallback((chatId: string, pending: boolean) => {
    setPendingByChat((map) => {
      if (pending) return map[chatId] ? map : { ...map, [chatId]: true };
      if (!map[chatId]) return map;
      const { [chatId]: _drop, ...rest } = map;
      return rest;
    });

    if (!pending) {
      setStatusByChat((map) => {
        if (!(chatId in map)) return map;
        const { [chatId]: _drop, ...rest } = map;
        return rest;
      });
    }
  }, []);

  const restoreActiveReviews = useCallback(async () => {
    try {
      const running = await api.activeReviews();

      const statuses: Record<string, string> = {};
      const pending: Record<string, true> = {};

      for (const review of running) {
        pending[review.chatId] = true;
        const text = review.detail ?? (review.stage === 'Queued' ? 'Заявка в очереди на разбор…' : null);
        if (text) statuses[review.chatId] = text;
      }

      setPendingByChat((map) => ({ ...map, ...pending }));
      setStatusByChat((map) => ({ ...map, ...statuses }));
    } catch {
      // молча: восстановление статуса не должно мешать работе с чатом
    }
  }, []);

  const applyManualStatus = useCallback((event: ManualStatusEvent) => {
    setManuals((list) => {
      const next: ManualData = {
        id: event.manualId,
        title: event.title,
        stage: event.stage,
        totalChunks: event.totalChunks,
        processedChunks: event.processedChunks,
        failedChunks: event.failedChunks,
        detail: event.detail,
      };

      return list.some((m) => m.id === event.manualId)
        ? list.map((m) => (m.id === event.manualId ? next : m))
        : [next, ...list];
    });

    if (event.stage === 'Ready') {
      notifyRef.current('success', `«${event.title}»: ${event.detail ?? 'методичка разобрана'}`);
      return;
    }

    if (event.stage === 'Partial') {
      notifyRef.current(
        'error',
        `«${event.title}»: ${event.detail ?? 'часть методички разобрать не удалось'}`,
      );
      return;
    }

    if (event.stage === 'Failed') {
      notifyRef.current('error', `«${event.title}»: ${event.detail ?? 'разбор не удался'}`);
    }
  }, []);

  const applyManualStatusRef = useRef(applyManualStatus);
  applyManualStatusRef.current = applyManualStatus;

  const setManualId = useCallback((id: string | null) => {
    setManualIdState(id);
    if (id) localStorage.setItem(MANUAL_KEY, id);
    else localStorage.removeItem(MANUAL_KEY);
  }, []);

  const reconnectAs = useCallback(async () => {
    const hub = hubRef.current;
    if (!hub) return;

    setActiveChatId(null);
    threadRef.current = null;
    setMessages([]);
    setChats([]);
    setStaffChats([]);

    await hub.stop();
    await hub.start();
  }, []);

  const signIn = useCallback(
    async (name: string, password: string) => {
      const next = await apiLogin(name, password);
      setIdentity(next);
      await reconnectAs();
      notifyRef.current('success', `Вход выполнен: ${next.name ?? name}`);
      return next;
    },
    [reconnectAs],
  );

  const signUp = useCallback(
    async (input: RegisterInput) => {
      const next = await apiRegister(input);
      setIdentity(next);
      await reconnectAs();
      notifyRef.current('success', `Учётная запись создана: ${next.name ?? input.name}`);
      return next;
    },
    [reconnectAs],
  );

  const signOut = useCallback(async () => {
    setIdentity(await apiLogout());
    await reconnectAs();
    notifyRef.current('info', 'Гостевой режим');
  }, [reconnectAs]);

  useEffect(() => {
    const hub = new ChatHubClient({
      onState: (state) => {
        setConnection(state);
        if (state === 'online') {
          hub
            .getChatList('Grant')
            .then(setChats)
            .catch((e) =>
              notifyRef.current('error', `Список чатов недоступен: ${errorText(e)}`),
            );

          void refreshStaffChatsRef.current();

          const active = threadRef.current;
          if (active) void loadThreadRef.current(active);

          void restoreActiveReviewsRef.current();
        }

        if (state === 'offline') setLoadingThread(false);
      },
      onAiStatus: ({ chatId, status }) => {
        if (!chatId) return;

        setStatusByChat((map) => {
          if (!status) {
            if (!(chatId in map)) return map;
            const { [chatId]: _drop, ...rest } = map;
            return rest;
          }
          return { ...map, [chatId]: status };
        });
      },
      onMessageReady: ({ chatId, message }) => {
        markPendingRef.current(chatId, false);

        if (threadRef.current !== chatId) return;

        setMessages((list) => (list.some((m) => m.id === message.id) ? list : [...list, message]));
      },
      onFileStatus: ({ fileName, status }) => {
        const human: Record<string, string> = {
          Queued: 'загружен, ждёт очереди на разбор',
          Reviewing: 'разбирается',
          Reviewed: 'проверен',
          Failed: 'разобрать не удалось',
        };
        notifyRef.current(
          status === 'Failed' ? 'error' : 'info',
          `${fileName}: ${human[status] ?? status}`,
        );
      },
      onManualStatus: (event) => {
        applyManualStatusRef.current(event);
      },
      onReviewReady: ({ chatId, documentId, messageId, content }) => {
        markPendingRef.current(chatId, false);

        if (documentId && threadRef.current === chatId) {
          setMessages((list) =>
            list.map((m) =>
              m.file && m.file.documentId === documentId
                ? { ...m, file: { ...m.file, reviewMessageId: messageId } }
                : m,
            ),
          );
        }

        if (threadRef.current !== chatId) {
          notifyRef.current('success', 'Разбор готов в другом чате');
          return;
        }

        setMessages((list) =>
          list.some((m) => m.id === messageId)
            ? list
            : [
                ...list,
                {
                  id: messageId,
                  author: 'agent',
                  authorName: 'Агент-рецензент',
                  content,
                  createdAt: Date.now(),
                },
              ],
        );
      },
      onReviewFailed: ({ chatId, messageId, reason }) => {
        markPendingRef.current(chatId, false);
        notifyRef.current('error', reason);

        if (threadRef.current !== chatId) return;

        setMessages((list) =>
          list.some((m) => m.id === messageId)
            ? list
            : [
                ...list,
                {
                  id: messageId,
                  author: 'agent',
                  authorName: 'Агент-рецензент',
                  content: reason,
                  createdAt: Date.now(),
                  failed: true,
                },
              ],
        );
      },
    });
    hubRef.current = hub;

    void (async () => {
      try {
        await ensureAuth();
        setIdentity(getIdentity());
        await restoreActiveReviews();
        await hub.start();
      } catch (e) {
        notifyRef.current('error', `Не удалось подключиться: ${errorText(e)}`);
      }
    })();

    return () => {
      hubRef.current = null;
      void hub.stop();
    };
  }, []);

  const refreshChats = useCallback(async () => {
    const hub = hubRef.current;
    if (!hub) return;
    try {
      setChats(await hub.getChatList('Grant'));
    } catch (e) {
      notifyRef.current('error', `Список чатов недоступен: ${errorText(e)}`);
    }
  }, []);

  const refreshStaffChats = useCallback(async () => {
    const hub = hubRef.current;
    if (!hub || !isStaffRef.current) {
      setStaffChats([]);
      return;
    }

    try {
      setStaffChats(await hub.getChatList('Staff'));
    } catch (e) {
      notifyRef.current('error', `Список служебных чатов недоступен: ${errorText(e)}`);
    }
  }, []);

  const refreshManuals = useCallback(async (silent = false) => {
    try {
      const list = await api.manuals();
      setManuals(list);
      setManualIdState((current) => {
        if (current && list.some((m) => m.id === current)) return current;
        const fallback = (list.find(manualUsable) ?? list[0])?.id ?? null;
        if (fallback) localStorage.setItem(MANUAL_KEY, fallback);
        return fallback;
      });
    } catch (e) {
      if (!silent) notifyRef.current('error', `Методички не загрузились: ${errorText(e)}`);
    }
  }, []);

  useEffect(() => {
    void refreshManuals();
  }, [refreshManuals]);

  const awaitedManuals = manuals.some(manualPending);

  useEffect(() => {
    if (!awaitedManuals) return;

    const timer = window.setInterval(() => void refreshManuals(true), MANUAL_POLL_MS);
    return () => window.clearInterval(timer);
  }, [awaitedManuals, refreshManuals]);

  const loadThread = useCallback(async (chatId: string) => {
    const hub = hubRef.current;

    setLoadingThread(true);

    if (!hub || hub.getState() !== 'online') return;

    try {
      const raw = await hub.getMessages(chatId);
      if (threadRef.current !== chatId) return;
      setMessages(raw.map(normalizeMessage));
    } catch (e) {
      if (threadRef.current === chatId) {
        setMessages([]);
        notifyRef.current('error', `История не загрузилась: ${errorText(e)}`);
      }
    } finally {
      if (threadRef.current === chatId) setLoadingThread(false);
    }
  }, []);

  const loadThreadRef = useRef(loadThread);
  loadThreadRef.current = loadThread;

  const markPendingRef = useRef(markPending);
  markPendingRef.current = markPending;

  const restoreActiveReviewsRef = useRef(restoreActiveReviews);
  restoreActiveReviewsRef.current = restoreActiveReviews;

  const refreshStaffChatsRef = useRef(refreshStaffChats);
  refreshStaffChatsRef.current = refreshStaffChats;

  const openChat = useCallback(
    async (chatId: string | null) => {
      if (threadRef.current === chatId) return;

      setActiveChatId(chatId);
      threadRef.current = chatId;
      setMessages([]);

      if (!chatId) {
        setLoadingThread(false);
        return;
      }

      await loadThread(chatId);
    },
    [loadThread],
  );

  const send = useCallback(
    async (text: string, file: File | null): Promise<string | null> => {
      const hub = hubRef.current;
      const content = text.trim();
      if (!hub || busy) return null;
      if (!content && !file) return null;

      const staffChat = activeChatId
        ? staffChats.some((c) => c.id === activeChatId)
        : draftKind === 'Staff';

      if (!manualId && !staffChat) {
        notifyRef.current('error', 'Сначала выберите методичку — по ней агент сверяет заявку');
        return null;
      }

      if (file && !manualId) {
        notifyRef.current('error', 'Чтобы разобрать заявку, выберите методичку в панели слева');
        return null;
      }

      const localId = crypto.randomUUID();
      const optimistic: ChatMessage = {
        id: localId,
        author: 'user',
        authorName: 'Вы',
        content,
        createdAt: Date.now(),
        fileName: file?.name,
      };
      setMessages((list) => [...list, optimistic]);
      setBusy(true);

      let chatId = activeChatId;
      let queuedForReview = false;
      let keepPending = false;

      const inThread = () => threadRef.current === chatId;

      try {
        if (!chatId) {
          chatId = await hub.createChat(
            titleFrom(content || file?.name || 'Новый чат'),
            staffChat ? 'Staff' : 'Grant',
          );
          setActiveChatId(chatId);
          threadRef.current = chatId;
          if (staffChat) void refreshStaffChats();
          else void refreshChats();
        }

        markPending(chatId, true);
        setStatusByChat((map) => ({
          ...map,
          [chatId as string]: file ? 'Загружаю документ…' : 'Обрабатываю запрос…',
        }));

        if (file && manualId) {
          const queued = await api.uploadApplication(
            chatId,
            manualId,
            file,
            content || undefined,
            contestKind,
          );

          if (inThread()) {
            setMessages((list) =>
              list.map((m) =>
                m.id === localId
                  ? {
                      ...m,
                      id: queued.messageId || m.id,
                      fileName: undefined,
                      file: queued.documentId
                        ? {
                            documentId: queued.documentId,
                            fileName: queued.fileName || file.name,
                            reviewMessageId: null,
                          }
                        : undefined,
                    }
                  : m,
              ),
            );
          }

          queuedForReview = true;
          setStatusByChat((map) => ({
            ...map,
            [chatId as string]:
              queued.queueDepth > 1
                ? `Заявка в очереди на разбор, перед ней: ${queued.queueDepth - 1}`
                : 'Заявка принята, начинаю разбор…',
          }));
        } else {
          const reply = normalizeMessage(await hub.sendMessage(chatId, content, manualId));
          if (inThread()) {
            setMessages((list) => (list.some((m) => m.id === reply.id) ? list : [...list, reply]));
          }
        }
      } catch (e) {
        if (!file && hub.getState() !== 'online') {
          keepPending = true;
          notifyRef.current(
            'info',
            'Связь оборвалась, но агент продолжает готовить ответ — он появится в чате сам',
          );
        } else {
          if (inThread()) {
            setMessages((list) =>
              list.map((m) => (m.id === localId ? { ...m, failed: true } : m)),
            );
          }
          notifyRef.current('error', errorText(e));
        }
      } finally {
        setBusy(false);
        if (!queuedForReview && !keepPending && chatId) markPending(chatId, false);
      }

      return chatId;
    },
    [
      activeChatId,
      busy,
      staffChats,
      draftKind,
      manualId,
      contestKind,
      refreshChats,
      refreshStaffChats,
      markPending,
    ],
  );

  const deleteChat = useCallback(async (chatId: string) => {
    try {
      await api.deleteChat(chatId);

      setChats((list) => list.filter((c) => c.id !== chatId));
      setStaffChats((list) => list.filter((c) => c.id !== chatId));

      markPending(chatId, false);

      if (threadRef.current === chatId) {
        threadRef.current = null;
        setActiveChatId(null);
        setMessages([]);
      }

      notifyRef.current('success', 'Чат удалён');
    } catch (e) {
      notifyRef.current('error', `Не удалось удалить чат: ${errorText(e)}`);
      throw e;
    }
  }, [markPending]);

  const uploadManual = useCallback(
    async (title: string, file: File) => {
      try {
        const queued = await api.uploadManual(title, file);
        notifyRef.current('info', queued.message || `«${title}» сохранена, начинаю разбор`);
        await refreshManuals();
      } catch (e) {
        notifyRef.current('error', `Не удалось загрузить методичку: ${errorText(e)}`);
        throw e;
      }
    },
    [refreshManuals],
  );

  const aiStatus = activeChatId ? (statusByChat[activeChatId] ?? null) : null;
  const reviewing = activeChatId ? Boolean(pendingByChat[activeChatId]) : false;

  const activeChat = useMemo(
    () =>
      chats.find((c) => c.id === activeChatId) ??
      staffChats.find((c) => c.id === activeChatId) ??
      null,
    [chats, staffChats, activeChatId],
  );

  const activeManual = useMemo(
    () => manuals.find((m) => m.id === manualId) ?? null,
    [manuals, manualId],
  );

  const staffChat = activeChat ? activeChat.kind === 'Staff' : draftKind === 'Staff';

  const blockedReason = useMemo(() => {
    if (staffChat) return null;

    if (manuals.length === 0) return 'Сначала загрузите положение о гранте — сверять пока не с чем';
    if (!manualId) return 'Выберите положение о гранте в панели слева';

    if (activeManual && manualPending(activeManual)) {
      const progress =
        activeManual.totalChunks > 0
          ? ` (${activeManual.processedChunks} из ${activeManual.totalChunks} фрагментов)`
          : '';
      return `«${activeManual.title}» ещё разбирается${progress} — выберите другое положение или дождитесь конца разбора`;
    }

    if (activeManual?.stage === 'Failed') {
      return `«${activeManual.title}» разобрать не удалось — загрузите положение заново или выберите другое`;
    }

    return null;
  }, [staffChat, manuals.length, manualId, activeManual]);

  return {
    connection,
    chats,
    staffChats,
    activeChatId,
    activeChat,
    staffChat,
    messages,
    manuals,
    manualId,
    activeManual,
    blockedReason,
    setDraftKind,
    aiStatus,
    busy,
    reviewing,
    loadingThread,
    identity,
    signIn,
    signUp,
    signOut,
    contestKind,
    setManualId,
    openChat,
    deleteChat,
    send,
    uploadManual,
    refreshChats,
    refreshStaffChats,
    refreshManuals,
  };
}
