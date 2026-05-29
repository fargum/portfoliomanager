import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { AuthProvider } from '@/components/AuthProvider';
import { AuthContextProvider } from '@/contexts/AuthContext';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { ClientOnly } from '@/components/ClientOnly';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Portfolio Manager',
  description: 'Professional portfolio management and holdings tracking',
  viewport: {
    width: 'device-width',
    initialScale: 1,
    maximumScale: 5,
    userScalable: true,
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const themeInitScript = `
    (function () {
      try {
        var key = 'portfolio-theme-preference';
        var stored = window.localStorage.getItem(key);
        var preference = (stored === 'light' || stored === 'dark' || stored === 'system') ? stored : 'system';
        var systemDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        var resolved = preference === 'system' ? (systemDark ? 'dark' : 'light') : preference;
        document.documentElement.classList.toggle('dark', resolved === 'dark');
      } catch (e) {
        // Ignore script errors and allow default light theme.
      }
    })();
  `;

  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className={`${inter.className} min-h-screen bg-financial-slate-50 dark:bg-financial-slate-950 text-financial-slate-900 dark:text-financial-slate-100`}>
        <ClientOnly>
          <ThemeProvider>
            <AuthProvider>
              <AuthContextProvider>
                <div className="min-h-screen bg-gradient-to-br from-financial-slate-50 to-financial-slate-100 dark:from-financial-slate-950 dark:to-financial-slate-900 transition-colors duration-300">
                  {children}
                </div>
              </AuthContextProvider>
            </AuthProvider>
          </ThemeProvider>
        </ClientOnly>
      </body>
    </html>
  );
}