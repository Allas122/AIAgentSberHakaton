import type { Identity } from './api/types';

export const routes = {
  chat: '/chat',
  chatById: (chatId: string) => `/chat/${chatId}`,
  letters: '/letters',
  assignments: '/assignments',
  files: '/files',
  accounts: '/accounts',
  usage: '/usage',
} as const;

export type Section = 'chat' | 'letters' | 'assignments' | 'files' | 'accounts' | 'usage';

export const STAFF_ROLES = ['Rector', 'Coordinator'];

export const isStaff = (identity: Identity) =>
  identity.role !== null && STAFF_ROLES.includes(identity.role);

export const isRector = (identity: Identity) => identity.role === 'Rector';

export function sectionFrom(pathname: string): Section {
  if (pathname.startsWith(routes.letters)) return 'letters';
  if (pathname.startsWith(routes.assignments)) return 'assignments';
  if (pathname.startsWith(routes.files)) return 'files';
  if (pathname.startsWith(routes.accounts)) return 'accounts';
  if (pathname.startsWith(routes.usage)) return 'usage';
  return 'chat';
}

export const SECTION_TITLES: Record<Section, string> = {
  chat: 'Заявки',
  letters: 'Деловая переписка',
  assignments: 'Поручения',
  files: 'Загруженные файлы',
  accounts: 'Учётные записи',
  usage: 'Расход токенов',
};

export const isGuest = (identity: Identity) => identity.role === 'Guest';
