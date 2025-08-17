'use client';

import { useState, useMemo, useEffect } from 'react';
import styles from './BusList.module.scss';
import { BusStatus } from '@/lib/api';
import BusSection from '../BusSection';
import { useLocalStorage } from '@/hooks';

interface BusListProps {
  buses: BusStatus[];
  isLoading?: boolean;
  error?: string | null;
  lastUpdated?: Date | null;
}

export default function BusList({ buses, isLoading = false, error = null, lastUpdated = null }: BusListProps) {
  const [preferredBuses, setPreferredBuses] = useLocalStorage<string[]>('preferred-buses', []);
  const [searchTerm, setSearchTerm] = useState('');
  const [collapsedSections, setCollapsedSections] = useLocalStorage<Record<string, boolean>>('bus-section-collapsed', {
    'preferred': false,
    'at-bays': false,
    'not-arrived': true // Default collapsed
  });
  const [userModifiedSections, setUserModifiedSections] = useLocalStorage<Record<string, boolean>>('bus-section-user-modified', {});

  const togglePreferred = (service: string) => {
    setPreferredBuses(prev => 
      prev.includes(service) 
        ? prev.filter(s => s !== service)
        : [...prev, service]
    );
  };

  const filteredBuses = useMemo(() => {
    return buses.filter(bus => 
      bus.service.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (bus.bay && bus.bay.toLowerCase().includes(searchTerm.toLowerCase()))
    );
  }, [buses, searchTerm]);

  const organizedBuses = useMemo(() => {
    const preferred = filteredBuses.filter(bus => preferredBuses.includes(bus.service));
    const atBays = filteredBuses.filter(bus => bus.bay && !preferredBuses.includes(bus.service));
    const notArrived = filteredBuses.filter(bus => !bus.bay && !preferredBuses.includes(bus.service));

    return {
      preferred,
      atBays,
      notArrived,
    };
  }, [filteredBuses, preferredBuses]);

  // Handle section collapse/expand with search logic
  const handleSectionToggle = (sectionKey: string) => {
    setCollapsedSections(prev => ({
      ...prev,
      [sectionKey]: !prev[sectionKey]
    }));
    
    // Mark that user manually modified this section
    setUserModifiedSections(prev => ({
      ...prev,
      [sectionKey]: true
    }));
  };

  // Collapse/expand based on search
  useEffect(() => {
    if (searchTerm.trim()) {
      // When searching, expand sections that have matching buses
      const newCollapsedState = { ...collapsedSections };
      
      if (organizedBuses.preferred.length > 0) {
        newCollapsedState['preferred'] = false;
      }
      if (organizedBuses.atBays.length > 0) {
        newCollapsedState['at-bays'] = false;
      }
      if (organizedBuses.notArrived.length > 0) {
        newCollapsedState['not-arrived'] = false;
      }
      
      setCollapsedSections(newCollapsedState);
    } else {
      // When search is cleared, restore default collapsed state for sections
      // that weren't manually modified by the user
      const newCollapsedState = { ...collapsedSections };
      
      if (!userModifiedSections['preferred'] && organizedBuses.preferred.length === 0) {
        newCollapsedState['preferred'] = false; // Keep preferred expanded if it exists
      }
      if (!userModifiedSections['at-bays'] && organizedBuses.atBays.length === 0) {
        newCollapsedState['at-bays'] = false; // Keep at-bays expanded by default
      }
      if (!userModifiedSections['not-arrived'] && organizedBuses.notArrived.length === 0) {
        newCollapsedState['not-arrived'] = true; // Collapse not-arrived by default
      }
      
      setCollapsedSections(newCollapsedState);
    }
  }, [searchTerm, organizedBuses, userModifiedSections]);

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
          <input
            type="text"
            id="searchInput"
            className={styles.searchInput}
            placeholder="Search by bus number or bay"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>

        <div className={styles.busInfoSections}>
        {organizedBuses.preferred.length > 0 && (
          <BusSection
            title="Preferred Services"
            buses={organizedBuses.preferred}
            preferredBuses={preferredBuses}
            onTogglePreferred={togglePreferred}
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
          preferredBuses={preferredBuses}
          onTogglePreferred={togglePreferred}
          busCount={organizedBuses.atBays.length}
          isCollapsed={collapsedSections['at-bays'] || false}
          onToggleCollapse={() => handleSectionToggle('at-bays')}
        />

        <BusSection
          title="Not Arrived"
          buses={organizedBuses.notArrived}
          preferredBuses={preferredBuses}
          onTogglePreferred={togglePreferred}
          busCount={organizedBuses.notArrived.length}
          isCollapsed={collapsedSections['not-arrived'] || false}
          onToggleCollapse={() => handleSectionToggle('not-arrived')}
        />
        </div>
      </div>

      <div className={styles.infoFooter}>
        {lastUpdated && (
          <div id="lastUpdated">
            Last updated: {lastUpdated.toLocaleTimeString()}
          </div>
        )}
        
        <div className={styles.dataSource}>
          Data provided by <a href="https://webservices.runshaw.ac.uk/bus/busdepartures.aspx" target="_blank" rel="noopener noreferrer">Runshaw College</a>
          {' • '}Updates every 30 seconds
        </div>
      </div>
    </div>
  );
}