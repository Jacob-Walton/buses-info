import styles from './BusCard.module.scss';
import { BusStatus } from '@/lib/api';

interface BusCardProps {
  bus: BusStatus;
  isPreferred?: boolean;
  onTogglePreferred?: () => void;
}

export default function BusCard({ bus, isPreferred = false, onTogglePreferred }: BusCardProps) {
  const getBayDisplay = (bay: string | null) => {
    if (!bay) return 'Not assigned';
    return `Bay ${bay}`;
  };

  const getBayStatus = (bay: string | null) => {
    if (!bay) return 'not-assigned';
    return 'assigned';
  };

  return (
    <div className={`${styles.busCard} ${isPreferred ? styles.preferred : ''}`}>
      <div className={styles.header}>
        <div className={styles.serviceInfo}>
          <h3 className={styles.serviceNumber}>Service {bus.service}</h3>
          <span className={`${styles.bayStatus} ${styles[getBayStatus(bus.bay)]}`}>
            {getBayDisplay(bus.bay)}
          </span>
        </div>
        {onTogglePreferred && (
          <button
            className={`${styles.favoriteBtn} ${isPreferred ? styles.active : ''}`}
            onClick={onTogglePreferred}
            aria-label={isPreferred ? 'Remove from favorites' : 'Add to favorites'}
          >
            ★
          </button>
        )}
      </div>
      
      <div className={styles.details}>
        <div className={styles.status}>
          <span className={styles.label}>Status:</span>
          <span className={`${styles.statusValue} ${styles[getBayStatus(bus.bay)]}`}>
            {bus.bay ? 'At bay' : 'In transit'}
          </span>
        </div>
      </div>
    </div>
  );
}