import { useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { MessageList } from '../components/MessageList';
import type { useChat } from '../hooks/useChat';

interface Props {
  chat: ReturnType<typeof useChat>;
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
  onPickPrompt: (prompt: string) => void;
}

export function ChatPage({ chat, notify, onPickPrompt }: Props) {
  const { chatId } = useParams<{ chatId: string }>();
  const { openChat } = chat;

  useEffect(() => {
    void openChat(chatId ?? null);
  }, [chatId, openChat]);

  const pending = chat.reviewing || (chat.busy && !chat.activeChatId);
  const hasThread = chat.messages.length > 0 || pending || chat.loadingThread;

  if (!hasThread) {
    return (
      <EmptyState
        manualTitle={chat.activeManual?.title ?? null}
        staff={chat.staffChat}
        onPick={onPickPrompt}
      />
    );
  }

  return (
    <MessageList
      messages={chat.messages}
      aiStatus={chat.aiStatus}
      busy={pending}
      loading={chat.loadingThread}
      chatId={chat.activeChatId}
      notify={notify}
    />
  );
}
