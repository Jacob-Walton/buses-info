'use client';

import { useEffect } from 'react';
import { Button } from '@/components/ui';
import styles from './error.module.scss';

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error('Application error:', error);
  }, [error]);

  return (
    <div className={styles.errorContainer}>
      <div className={styles.content}>
        <div className={styles.icon}>
          <i className="fas fa-exclamation-triangle"></i>
        </div>
        <h1 className={styles.title}>Something went wrong!</h1>
        <p className={styles.description}>
          We&apos;re sorry, but something unexpected happened. Please try again.
        </p>
        {process.env.NODE_ENV === 'development' && (
          <details className={styles.errorDetails}>
            <summary>Error Details</summary>
            <pre>{error.message}</pre>
          </details>
        )}
        <div className={styles.actions}>
          <Button onClick={reset} variant="primary">
            Try Again
          </Button>
          <Button href="/" variant="secondary">
            Go Home
          </Button>
        </div>
      </div>
    </div>
  );
}
