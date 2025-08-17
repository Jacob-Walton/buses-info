'use client';

import styles from './BusSection.module.scss';
import { BusStatus } from '@/lib/api';
import BusItem from '../BusItem';

interface BusSectionProps {
  title: string;
  buses: BusStatus[];
  preferredBuses: string[];
  onTogglePreferred: (service: string) => void;
  isPreferredSection?: boolean;
  showStarIcon?: boolean;
  busCount?: number;
  isCollapsed?: boolean;
  onToggleCollapse?: () => void;
}

export default function BusSection({ 
  title, 
  buses, 
  preferredBuses, 
  onTogglePreferred,
  isPreferredSection = false,
  showStarIcon = false,
  busCount,
  isCollapsed = false,
  onToggleCollapse
}: BusSectionProps) {
  const handleToggleCollapse = () => {
    if (onToggleCollapse) {
      onToggleCollapse();
    }
  };

  const sectionClasses = [
    styles.busSection,
    isPreferredSection ? styles.preferredSection : '',
  ].filter(Boolean).join(' ');

  const headerClasses = [
    styles.busSectionHeader,
    isCollapsed ? styles.collapsed : '',
  ].filter(Boolean).join(' ');

  const contentClasses = [
    styles.busSectionContent,
    isCollapsed ? styles.collapsed : '',
  ].filter(Boolean).join(' ');

  return (
    <div className={sectionClasses}>
      <div className={headerClasses} onClick={handleToggleCollapse}>
        <div className={styles.sectionHeaderContent}>
          <h3>
            {showStarIcon && <i className="fas fa-star" />}
            {title}
          </h3>
          {busCount !== undefined && (
            <span className={styles.busCount}>
              ({busCount} {busCount === 1 ? 'service' : 'services'})
            </span>
          )}
        </div>
        <button className={styles.sectionToggle} aria-label="Toggle section">
          <i className="fas fa-chevron-up" />
        </button>
      </div>
      
      <div className={contentClasses}>
        {buses.map((bus) => (
          <BusItem
            key={bus.service}
            bus={bus}
            isPreferred={preferredBuses.includes(bus.service)}
            onTogglePreferred={() => onTogglePreferred(bus.service)}
          />
        ))}
        {buses.length === 0 && !isCollapsed && (
          <div className={styles.emptyMessage}>
            No buses in this section
          </div>
        )}
      </div>
    </div>
  );
}