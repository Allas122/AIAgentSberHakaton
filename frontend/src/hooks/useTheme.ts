import { useCallback, useEffect, useState } from 'react';

export type Theme = 'light' | 'dark';

const KEY = 'theme';

function current(): Theme {
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
}

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(current);

  const apply = useCallback((next: Theme) => {
    const root = document.documentElement;
    root.classList.add('theme-switching');
    root.dataset.theme = next;
    setTheme(next);
    try {
      localStorage.setItem(KEY, next);
    } catch {
    }
    window.setTimeout(() => root.classList.remove('theme-switching'), 250);
  }, []);

  const toggle = useCallback(() => {
    apply(current() === 'dark' ? 'light' : 'dark');
  }, [apply]);

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const onChange = (e: MediaQueryListEvent) => {
      if (localStorage.getItem(KEY)) return;
      const next: Theme = e.matches ? 'dark' : 'light';
      document.documentElement.dataset.theme = next;
      setTheme(next);
    };
    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, []);

  return { theme, setTheme: apply, toggle };
}
