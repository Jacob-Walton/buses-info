import { useState, useEffect, useCallback, useRef } from 'react';
import { BusResponse } from '@/types/bus';
import { ApiError } from '@/lib/errors';

interface UseBusesOptions {
  refreshInterval?: number; // in milliseconds
  autoRefresh?: boolean;
}

interface UseBusesReturn {
  buses: BusResponse['buses'];
  isLoading: boolean;
  error: string | null;
  lastUpdated: Date | null;
  isCached: boolean;
  refresh: () => Promise<void>;
  isRefreshing: boolean;
}

export function useBuses(options: UseBusesOptions = {}): UseBusesReturn {
  const { refreshInterval = 30000, autoRefresh = true } = options; // Default 30 seconds

  const [buses, setBuses] = useState<BusResponse['buses']>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [isCached, setIsCached] = useState(false);

  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const fetchBuses = useCallback(async (isRefresh = false) => {
    // Cancel any pending request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    abortControllerRef.current = new AbortController();

    try {
      if (isRefresh) {
        setIsRefreshing(true);
      } else {
        setIsLoading(true);
      }
      setError(null);

      const apiResponse = await fetch('/api/buses/current', {
        signal: abortControllerRef.current.signal,
      });

      if (!apiResponse.ok) {
        throw new ApiError(
          apiResponse.status,
          `HTTP ${apiResponse.status}: ${apiResponse.statusText}`,
        );
      }

      const response: BusResponse = await apiResponse.json();

      setBuses(response.buses);
      setIsCached(response.cached);
      setLastUpdated(new Date());
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') {
        // Request was cancelled, don't update state
        return;
      }

      const errorMessage =
        err instanceof ApiError
          ? `Failed to fetch bus data: ${err.message}`
          : 'An unexpected error occurred while fetching bus data';

      setError(errorMessage);
      console.error('Error fetching buses:', err);
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  const refresh = useCallback(async () => {
    await fetchBuses(true);
  }, [fetchBuses]);

  // Initial fetch
  useEffect(() => {
    fetchBuses();
  }, [fetchBuses]);

  // Set up auto-refresh
  useEffect(() => {
    if (!autoRefresh || refreshInterval <= 0) {
      return;
    }

    intervalRef.current = setInterval(() => {
      fetchBuses(true);
    }, refreshInterval);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [autoRefresh, refreshInterval, fetchBuses]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, []);

  return {
    buses,
    isLoading,
    error,
    lastUpdated,
    isCached,
    refresh,
    isRefreshing,
  };
}
