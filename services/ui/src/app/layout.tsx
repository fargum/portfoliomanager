import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Portfolio Manager',
  description: 'Professional portfolio management and holdings tracking',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className={`${inter.className} bg-financial-gray-50 min-h-screen`}>
        <div className="min-h-screen bg-gradient-to-br from-financial-gray-50 to-financial-gray-100">
          {children}
        </div>
      </body>
    </html>
  );
}