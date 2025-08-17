import { Button } from '@/components/ui';
import styles from './not-found.module.scss';

export default function NotFound() {
  return (
    <div className={styles.notFoundContainer}>
      <div className={styles.content}>
        <h1 className={styles.title}>404</h1>
        <h2 className={styles.subtitle}>Page Not Found</h2>
        <p className={styles.description}>
          The page you&apos;re looking for doesn&apos;t exist or has been moved.
        </p>
        <div className={styles.actions}>
          <Button href="/" variant="primary">
            Go Home
          </Button>
          <Button href="/buses" variant="secondary">
            View Bus Info
          </Button>
        </div>
      </div>
    </div>
  );
}