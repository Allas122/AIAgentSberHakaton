import { useCallback, useEffect, useRef, useState } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { AssignmentsPanel } from './components/AssignmentsPanel';
import { Composer } from './components/Composer';
import type { ComposerHandle } from './components/Composer';
import { AccountsPanel } from './components/AccountsPanel';
import { UsagePanel } from './components/UsagePanel';
import { FilesPanel } from './components/FilesPanel';
import { LettersPanel } from './components/LettersPanel';
import { LoginModal } from './components/LoginModal';
import { ManualModal } from './components/ManualModal';
import { OrganizationPanel } from './components/OrganizationPanel';
import { Sidebar } from './components/Sidebar';
import { Toasts } from './components/Toasts';
import { Topbar } from './components/Topbar';
import { useChat } from './hooks/useChat';
import { useTheme } from './hooks/useTheme';
import { useToasts } from './hooks/useToasts';
import { ChatPage } from './pages/ChatPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { RequireRole } from './pages/RequireRole';
import { isRector, isStaff, routes, SECTION_TITLES, sectionFrom } from './routes';
import type { Section } from './routes';

const isDesktop = () => window.matchMedia('(min-width: 861px)').matches;

export default function App() {
  const { theme, setTheme, toggle } = useTheme();
  const { toasts, push, dismiss } = useToasts();
  const chat = useChat({ notify: push });

  const navigate = useNavigate();
  const location = useLocation();
  const section = sectionFrom(location.pathname);

  const [sidebarOpen, setSidebarOpen] = useState(isDesktop);
  const [manualModal, setManualModal] = useState(false);
  const [loginModal, setLoginModal] = useState(false);
  const composerRef = useRef<ComposerHandle>(null);

  const closeOnMobile = useCallback(() => {
    if (!isDesktop()) setSidebarOpen(false);
  }, []);

  const openDraft = chat.setDraftKind;

  const newChat = useCallback(() => {
    openDraft('Grant');
    navigate(routes.chat);
    closeOnMobile();
    composerRef.current?.focus();
  }, [openDraft, navigate, closeOnMobile]);

  const newStaffChat = useCallback(() => {
    openDraft('Staff');
    navigate(routes.chat);
    closeOnMobile();
    composerRef.current?.focus();
  }, [openDraft, navigate, closeOnMobile]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        newChat();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [newChat]);

  const selectChat = useCallback(() => closeOnMobile(), [closeOnMobile]);

  const deleteChat = useCallback(
    async (id: string) => {
      const wasOpen = chat.activeChatId === id;
      await chat.deleteChat(id);
      if (wasOpen) navigate(routes.chat, { replace: true });
    },
    [chat, navigate],
  );

  const selectSection = useCallback((_next: Section) => closeOnMobile(), [closeOnMobile]);

  const send = useCallback(
    async (text: string, file: File | null) => {
      const chatId = await chat.send(text, file);
      if (chatId && chatId !== chat.activeChatId) navigate(routes.chatById(chatId), { replace: true });
    },
    [chat, navigate],
  );

  const offline = chat.connection !== 'online';
  const onChatSection = section === 'chat';

  return (
    <div className="app" data-sidebar={sidebarOpen ? 'open' : 'collapsed'}>
      <Sidebar
        chats={chat.chats}
        staffChats={chat.staffChats}
        activeChatId={chat.activeChatId}
        manuals={chat.manuals}
        manualId={chat.manualId}
        staffChat={chat.staffChat}
        identity={chat.identity}
        view={section}
        theme={theme}
        onSelectChat={selectChat}
        onDeleteChat={deleteChat}
        onNewChat={newChat}
        onNewStaffChat={newStaffChat}
        onSelectManual={chat.setManualId}
        onSelectView={selectSection}
        onLogin={() => setLoginModal(true)}
        onLogout={() => {
          navigate(routes.chat);
          void chat.signOut();
        }}
        onUploadManual={() => setManualModal(true)}
        onSetTheme={setTheme}
        onClose={() => setSidebarOpen(false)}
      />

      <div className="backdrop" onClick={() => setSidebarOpen(false)} />

      <main className="main">
        <Topbar
          title={onChatSection ? (chat.activeChat?.title ?? 'Новый чат') : SECTION_TITLES[section]}
          manualTitle={chat.activeManual?.title ?? null}
          connection={chat.connection}
          theme={theme}
          sidebarHidden={!sidebarOpen}
          onToggleSidebar={() => setSidebarOpen((v) => !v)}
          onToggleTheme={toggle}
        />

        <Routes>
          <Route path="/" element={<Navigate to={routes.chat} replace />} />

          <Route
            path="/chat"
            element={
              <ChatPage chat={chat} notify={push} onPickPrompt={(p) => composerRef.current?.fill(p)} />
            }
          />
          <Route
            path="/chat/:chatId"
            element={
              <ChatPage chat={chat} notify={push} onPickPrompt={(p) => composerRef.current?.fill(p)} />
            }
          />

          <Route
            path="/letters"
            element={
              <RequireRole identity={chat.identity} allow={isRector}>
                <LettersPanel notify={push} />
              </RequireRole>
            }
          />

          <Route
            path="/assignments"
            element={
              <RequireRole identity={chat.identity} allow={isStaff}>
                <AssignmentsPanel notify={push} />
              </RequireRole>
            }
          />

          <Route
            path="/files"
            element={
              <RequireRole identity={chat.identity} allow={isStaff}>
                <FilesPanel notify={push} onManualsChanged={() => void chat.refreshManuals()} />
              </RequireRole>
            }
          />

          <Route
            path="/accounts"
            element={
              <RequireRole identity={chat.identity} allow={isRector}>
                <AccountsPanel notify={push} />
              </RequireRole>
            }
          />

          <Route
            path="/usage"
            element={
              <RequireRole identity={chat.identity} allow={isStaff}>
                <UsagePanel notify={push} />
              </RequireRole>
            }
          />

          <Route
            path="/organization"
            element={
              <RequireRole identity={chat.identity} allow={isRector}>
                <OrganizationPanel notify={push} />
              </RequireRole>
            }
          />

          <Route path="*" element={<NotFoundPage />} />
        </Routes>

        {onChatSection && (
          <Composer
            handleRef={composerRef}
            disabled={offline}
            busy={chat.busy}
            blockedReason={chat.blockedReason}
            placeholder={
              offline
                ? 'Нет связи с сервером — переподключаюсь…'
                : 'Спросите про мониторинг или письмо…'
            }
            onSend={(text, file) => void send(text, file)}
          />
        )}
      </main>

      {loginModal && (
        <LoginModal
          onClose={() => setLoginModal(false)}
          onSubmit={async (l, p) => {
            await chat.signIn(l, p);
            navigate(routes.chat);
          }}
          onRegister={async (input) => {
            await chat.signUp(input);
            navigate(routes.chat);
          }}
        />
      )}

      {manualModal && (
        <ManualModal onClose={() => setManualModal(false)} onSubmit={chat.uploadManual} />
      )}

      <Toasts toasts={toasts} onDismiss={dismiss} />
    </div>
  );
}
