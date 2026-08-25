import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import type { Identity } from '../api/types';
import { routes } from '../routes';

interface Props {
  identity: Identity;
  allow: (identity: Identity) => boolean;
  children: ReactNode;
}

export function RequireRole({ identity, allow, children }: Props) {
  if (identity.role === null) return null;

  if (!allow(identity)) return <Navigate to={routes.chat} replace />;

  return <>{children}</>;
}
