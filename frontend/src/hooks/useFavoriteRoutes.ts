import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from './useAuth';
import { FavoriteRoutesResponse } from '@/types/preferences';

interface UseFavoriteRoutesOptions {
  cacheTimeout?: number; // in milliseconds, default 5 minutes
  autoSync?: boolean; // automatically sync with server
}

interface UseFavoriteRoutesReturn {
  favoriteRoutes: string[];
  isLoading: boolean;
  error: string | null;
  lastUpdated: Date | null;
  isCached: boolean;
  addFavorite: (routeName: string) => Promise<void>;
  removeFavorite: (routeName: string) => Promise<void>;
  toggleFavorite: (routeName: string) => Promise<boolean>;
  setFavorites: (routes: string[]) => Promise<void>;
  syncWithServer: () => Promise<void>;
  isInFavorites: (routeName: string) => boolean;
}

const CACHE_KEY = 'favorite-routes-cache';

interface CachedData {
  routes: string[];
  timestamp: number;
  serverLastUpdated: string;
}

export function useFavoriteRoutes(options: UseFavoriteRoutesOptions = {}): UseFavoriteRoutesReturn {
  const { cacheTimeout = 5 * 60 * 1000, autoSync = true } = options; // Default 5 minutes
  const { user, isAuthenticated } = useAuth();

  const [favoriteRoutes, setFavoriteRoutes] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [isCached, setIsCached] = useState(false);

  const loadFromCache = useCallback(() => {
    if (!user) return;

    try {
      const cachedData = localStorage.getItem(`${CACHE_KEY}-${user.id}`);
      if (cachedData) {
        const parsed: CachedData = JSON.parse(cachedData);
        const isExpired = Date.now() - parsed.timestamp > cacheTimeout;

        if (!isExpired) {
          setFavoriteRoutes(parsed.routes);
          setLastUpdated(new Date(parsed.serverLastUpdated));
          setIsCached(true);
          return;
        }
      }
    } catch (error) {
      console.error('Failed to load from cache:', error);
    }

    setIsCached(false);
  }, [user, cacheTimeout]);

  const saveToCache = useCallback(
    (routes: string[], serverLastUpdated: string) => {
      if (!user) return;

      try {
        const cacheData: CachedData = {
          routes,
          timestamp: Date.now(),
          serverLastUpdated,
        };
        localStorage.setItem(`${CACHE_KEY}-${user.id}`, JSON.stringify(cacheData));

        // Also update the basic localStorage
        localStorage.setItem('preferred-buses', JSON.stringify(routes));
      } catch (error) {
        console.error('Failed to save to cache:', error);
      }
    },
    [user],
  );

  const syncWithServer = useCallback(async () => {
    if (!isAuthenticated || !user) return;

    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`/api/users/${user.id}/favorites`, {
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const data: FavoriteRoutesResponse = await response.json();

      setFavoriteRoutes(data.routes);
      setLastUpdated(new Date(data.last_updated));
      setIsCached(false);

      saveToCache(data.routes, data.last_updated);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to sync with server';
      setError(errorMessage);
      console.error('Failed to sync favorite routes:', err);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, user, saveToCache]);

  // Create stable references
  const loadFromCacheRef = useRef(loadFromCache);
  loadFromCacheRef.current = loadFromCache;

  // Load from cache on component mount
  useEffect(() => {
    if (!isAuthenticated || !user) {
      // If not authenticated, load from basic localStorage
      const localRoutes = localStorage.getItem('preferred-buses');
      if (localRoutes) {
        try {
          const routes = JSON.parse(localRoutes);
          setFavoriteRoutes(Array.isArray(routes) ? routes : []);
        } catch (error) {
          console.error('Failed to parse local favorite routes:', error);
        }
      }
      return;
    }

    loadFromCacheRef.current();

    if (autoSync) {
      syncWithServerRef.current();
    }
  }, [isAuthenticated, user, autoSync]);

  const updateServerAndCache = useCallback(
    async (routes: string[]) => {
      if (!isAuthenticated || !user) {
        // Update local storage only
        localStorage.setItem('preferred-buses', JSON.stringify(routes));
        setFavoriteRoutes(routes);
        return;
      }

      const response = await fetch(`/api/users/${user.id}/favorites`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ routes }),
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const now = new Date().toISOString();
      setFavoriteRoutes(routes);
      setLastUpdated(new Date(now));
      saveToCache(routes, now);
    },
    [isAuthenticated, user, saveToCache],
  );

  const addFavorite = useCallback(
    async (routeName: string) => {
      if (favoriteRoutes.includes(routeName)) return;

      const newRoutes = [...favoriteRoutes, routeName];
      await updateServerAndCache(newRoutes);
    },
    [favoriteRoutes, updateServerAndCache],
  );

  const removeFavorite = useCallback(
    async (routeName: string) => {
      const newRoutes = favoriteRoutes.filter((route) => route !== routeName);
      await updateServerAndCache(newRoutes);
    },
    [favoriteRoutes, updateServerAndCache],
  );

  const toggleFavorite = useCallback(
    async (routeName: string): Promise<boolean> => {
      const isCurrentlyFavorite = favoriteRoutes.includes(routeName);

      if (isCurrentlyFavorite) {
        await removeFavorite(routeName);
        return false;
      } else {
        await addFavorite(routeName);
        return true;
      }
    },
    [favoriteRoutes, addFavorite, removeFavorite],
  );

  const setFavorites = useCallback(
    async (routes: string[]) => {
      await updateServerAndCache(routes);
    },
    [updateServerAndCache],
  );

  const isInFavorites = useCallback(
    (routeName: string): boolean => {
      return favoriteRoutes.includes(routeName);
    },
    [favoriteRoutes],
  );

  // Keep a stable reference to syncWithServer
  const syncWithServerRef = useRef(syncWithServer);
  syncWithServerRef.current = syncWithServer;

  // Auto-sync with server periodically if enabled
  useEffect(() => {
    if (!autoSync || !isAuthenticated) return;

    const interval = setInterval(() => {
      syncWithServerRef.current();
    }, cacheTimeout);

    return () => clearInterval(interval);
  }, [autoSync, isAuthenticated, cacheTimeout]);

  return {
    favoriteRoutes,
    isLoading,
    error,
    lastUpdated,
    isCached,
    addFavorite,
    removeFavorite,
    toggleFavorite,
    setFavorites,
    syncWithServer,
    isInFavorites,
  };
}
