'use client';

import { useAuth } from '@/hooks/useAuth';
import { Button } from '@/components/ui';
import styles from './Hero.module.scss';

export function Hero() {
  const { isAuthenticated } = useAuth();

  return (
    <section className={styles.hero}>
      <div className={styles.heroContent}>
        <h1 className={styles.heroTitle}>Bus Info</h1>
        <p className={styles.heroSubtitle}>Unofficial bus arrival information</p>
        
        {isAuthenticated ? (
          <Button href="/buses" variant="primary" size="large">
            View Bus Information
          </Button>
        ) : (
          <Button href="/login" variant="primary" size="large">
            Sign in to view bus information
          </Button>
        )}
        
        <p className={styles.disclaimer}>
          This is an independent project and is not affiliated with or endorsed by Runshaw College.
        </p>
      </div>
    </section>
  );
}