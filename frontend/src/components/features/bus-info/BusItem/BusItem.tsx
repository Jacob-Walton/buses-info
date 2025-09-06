import styles from './BusItem.module.scss';
import React from 'react';
import { BusStatus } from '@/types/bus';

interface BusItemProps {
  bus: BusStatus;
  isPreferred?: boolean;
  onTogglePreferred?: () => void;
}

export default function BusItem({ bus, isPreferred = false, onTogglePreferred }: BusItemProps) {
  const getBayDisplay = () => {
    if (bus.bay) {
      return (
        <div className={styles.busBay}>
          Bay <span className={styles.busBay__number}>{bus.bay}</span>
        </div>
      );
    }
    return <div className={`${styles.busBay} ${styles.busBay__notArrived}`}>Not arrived</div>;
  };

  return (
    <div className={`${styles.busItem} ${isPreferred ? styles.preferred : ''}`}>
      {onTogglePreferred && (
        <i
          className={`fas fa-star ${styles.starBadge} ${isPreferred ? styles.active : ''}`}
          onClick={onTogglePreferred}
          aria-label={isPreferred ? 'Remove from favorites' : 'Add to favorites'}
        />
      )}

      <div className={styles.busContent}>
        <div className={styles.busNumber}>{bus.service}</div>

        {getBayDisplay()}
      </div>
    </div>
  );
}
