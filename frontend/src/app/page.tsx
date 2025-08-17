import { Hero } from '@/components/features/home';
import { LegalModal } from '@/components/common';
import styles from './page.module.scss';

export default function HomePage() {
  return (
    <div className={styles.homeContainer}>
      <Hero />
      <LegalModal />
    </div>
  );
}