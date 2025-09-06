'use client';

import { useState, useMemo } from 'react';
import styles from './BusList.module.scss';
import { BusStatus } from '@/types/bus';
import BusSection from '../BusSection';
import { useLocalStorage, useFavoriteRoutes } from '@/hooks';

interface BusListProps {
  buses: BusStatus[];
  isLoading?: boolean;
  error?: string | null;
  lastUpdated?: Date | null;
}

export default function BusList({
  buses,
  isLoading = false,
  error = null,
  lastUpdated = null,
}: BusListProps) {
  const { favoriteRoutes, toggleFavorite, isInFavorites } = useFavoriteRoutes({
    cacheTimeout: 5 * 60 * 1000, // 5 minutes
    autoSync: true,
  });

  const [searchTerm, setSearchTerm] = useState('');
  const [collapsedSections, setCollapsedSections] = useLocalStorage<Record<string, boolean>>(
    'bus-section-collapsed',
    {
      preferred: false,
      'at-bays': false,
      'not-arrived': true, // Default collapsed
    },
  );

  const handleTogglePreferred = async (service: string) => {
    try {
      await toggleFavorite(service);
    } catch (error) {
      console.error('Failed to toggle favorite:', error);
    }
  };

  const filteredBuses = useMemo(() => {
    if (!searchTerm.trim()) {
      return buses; // Return all buses when no search term
    }
    return buses.filter(
      (bus) =>
        bus.service.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (bus.bay && bus.bay.toLowerCase().includes(searchTerm.toLowerCase())),
    );
  }, [buses, searchTerm]);

  const organizedBuses = useMemo(() => {
    const preferred = filteredBuses.filter((bus) => isInFavorites(bus.service));
    const atBays = filteredBuses.filter((bus) => bus.bay && !isInFavorites(bus.service));
    const notArrived = filteredBuses.filter((bus) => !bus.bay && !isInFavorites(bus.service));

    return {
      preferred,
      atBays,
      notArrived,
    };
  }, [filteredBuses, isInFavorites]);

  // Handle section collapse/expand with search logic
  const handleSectionToggle = (sectionKey: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [sectionKey]: !prev[sectionKey],
    }));
  };

  if (error) {
    return (
      <div className={styles.error}>
        <h3>Error loading bus information</h3>
        <p>{error}</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className={styles.loading}>
        <div className={styles.spinner}></div>
        <p>Loading...</p>
      </div>
    );
  }

  return (
    <div className={styles.busInfoContainer}>
      <div className={styles.contentContainer}>
        <div className={styles.searchOverlay}>
          <div className={styles.searchContainer}>
            <i className="fas fa-search"></i>
            <input
              type="text"
              id="searchInput"
              className={styles.searchInput}
              placeholder="Search by bus number or bay"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>

        <div className={styles.busInfoSections}>
          {organizedBuses.preferred.length > 0 && (
            <BusSection
              title="Preferred Services"
              buses={organizedBuses.preferred}
              preferredBuses={favoriteRoutes}
              onTogglePreferred={handleTogglePreferred}
              isPreferredSection={true}
              showStarIcon={true}
              busCount={organizedBuses.preferred.length}
              isCollapsed={collapsedSections['preferred'] || false}
              onToggleCollapse={() => handleSectionToggle('preferred')}
            />
          )}

          <BusSection
            title="At Bays"
            buses={organizedBuses.atBays}
            preferredBuses={favoriteRoutes}
            onTogglePreferred={handleTogglePreferred}
            busCount={organizedBuses.atBays.length}
            isCollapsed={collapsedSections['at-bays'] || false}
            onToggleCollapse={() => handleSectionToggle('at-bays')}
          />

          <BusSection
            title="Not Arrived"
            buses={organizedBuses.notArrived}
            preferredBuses={favoriteRoutes}
            onTogglePreferred={handleTogglePreferred}
            busCount={organizedBuses.notArrived.length}
            isCollapsed={collapsedSections['not-arrived'] || false}
            onToggleCollapse={() => handleSectionToggle('not-arrived')}
          />
        </div>
      </div>

      <div className={styles.infoFooter}>
        {lastUpdated && (
          <div id="lastUpdated">Last updated: {lastUpdated.toLocaleTimeString()}</div>
        )}

        <div className={styles.dataSource}>
          Data provided by{' '}
          <a
            href="https://webservices.runshaw.ac.uk/bus/busdepartures.aspx"
            target="_blank"
            rel="noopener noreferrer"
          >
            Runshaw College
          </a>
          {' • '}Updates every 30 seconds
        </div>
      </div>
    </div>
  );
}
