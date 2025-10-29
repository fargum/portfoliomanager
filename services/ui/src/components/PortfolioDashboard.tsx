'use client';

import React from 'react';
import { HoldingsGrid } from '@/components/HoldingsGrid';

interface PortfolioDashboardProps {
  accountId: number;
}

export function PortfolioDashboard({ accountId }: PortfolioDashboardProps) {
  return (
    <div className="w-full h-full">
      <HoldingsGrid accountId={accountId} />
    </div>
  );
}