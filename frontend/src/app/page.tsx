'use client';

import { useEffect } from 'react';
import { useBuses } from '@/hooks';
import { BusList } from '@/components/features/bus-info';
import { withAuth } from '@/components/features/auth';
import styles from './page.module.scss';

function BusesPage() {
  const { 
    buses, 
    isLoading, 
    error, 
    lastUpdated
  } = useBuses({
    refreshInterval: 30000, // 30 seconds
    autoRefresh: true
  });

  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, []);

  return (
    <div className={styles.busesPage}>
      <div className={styles.container}>
        <BusList 
          buses={buses}
          isLoading={isLoading}
          error={error}
          lastUpdated={lastUpdated}
        />
      </div>
    </div>
  );
}

export default withAuth(BusesPage, {
  requiredRoles: ['admin', 'user'],
  redirectTo: '/login'
});