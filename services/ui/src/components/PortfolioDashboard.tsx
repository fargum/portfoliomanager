'use client';

import React from 'react';
import { HoldingsGrid } from '@/components/HoldingsGrid';
import { AccountMode } from '@/lib/api-client';

interface PortfolioDashboardProps {
  accountMode: AccountMode;
}

export function PortfolioDashboard({ accountMode }: PortfolioDashboardProps) {
  return (
    <div className="w-full h-full">
      <HoldingsGrid accountMode={accountMode} />
    </div>
  );
}
