'use client';

import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui';
import styles from './page.module.scss';

export default function UnauthorizedPage() {
  const router = useRouter();

  return (
    <div className={styles.unauthorizedContainer}>
      <div className={styles.content}>
        <div className={styles.icon}>
          <i className="fas fa-shield-alt"></i>
        </div>
        <h1 className={styles.title}>Access Denied</h1>
        <h2 className={styles.subtitle}>403</h2>
        <p className={styles.description}>
          You don&apos;t have permission to access this page. Please log in with an authorized
          account or contact an administrator if you believe this is an error.
        </p>
        <div className={styles.actions}>
          <Button onClick={() => router.push('/login')} variant="primary">
            Login
          </Button>
          <Button onClick={() => router.back()} variant="secondary">
            Go Back
          </Button>
        </div>
      </div>
    </div>
  );
}
