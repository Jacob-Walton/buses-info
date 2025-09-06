import styles from './loading.module.scss';

export default function Loading() {
  return (
    <div className={styles.loadingContainer}>
      <div className={styles.spinner}>
        <div className={styles.spinnerInner}></div>
      </div>
      <p className={styles.loadingText}>Loading...</p>
    </div>
  );
}
