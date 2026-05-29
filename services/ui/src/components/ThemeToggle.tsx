'use client';

import { Monitor, Moon, Sun } from 'lucide-react';
import { ThemePreference, useTheme } from '@/contexts/ThemeContext';
import type { ElementType } from 'react';

const options: { value: ThemePreference; label: string; icon: ElementType }[] = [
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
  { value: 'system', label: 'System', icon: Monitor },
];

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  return (
    <div className="inline-flex items-center rounded-lg border border-financial-slate-200/80 dark:border-financial-slate-700/80 bg-white/70 dark:bg-financial-slate-900/70 backdrop-blur-sm p-1 shadow-sm">
      {options.map((option) => {
        const Icon = option.icon;
        const active = theme === option.value;

        return (
          <button
            key={option.value}
            type="button"
            onClick={() => setTheme(option.value)}
            className={`inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors ${
              active
                ? 'bg-financial-blue-600 text-white'
                : 'text-financial-slate-600 dark:text-financial-slate-300 hover:bg-financial-slate-100 dark:hover:bg-financial-slate-800'
            }`}
            aria-label={`Switch to ${option.label} theme`}
            title={option.label}
          >
            <Icon className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">{option.label}</span>
          </button>
        );
      })}
    </div>
  );
}
