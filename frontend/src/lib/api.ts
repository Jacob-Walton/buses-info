// Import dummy data for development
import { dummyApi } from './dummyData';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export interface BusStatus {
  service: string;
  bay: string | null;
}

export interface BusResponse {
  buses: BusStatus[];
  cached: boolean;
}

export interface HealthStatus {
  database: boolean;
  site: boolean;
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;
  
  try {
    const response = await fetch(url, {
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
      ...options,
    });

    if (!response.ok) {
      throw new ApiError(response.status, `HTTP ${response.status}: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }
    throw new ApiError(0, `Network error: ${error instanceof Error ? error.message : 'Unknown error'}`);
  }
}

// Check if we're in development mode
const isDevelopment = process.env.NODE_ENV === 'development';

export const api = {
  // Health endpoints
  async healthCheck(): Promise<string> {
    if (isDevelopment) {
      return dummyApi.healthCheck();
    }
    return fetchApi<string>('/health');
  },

  async healthStatus(): Promise<HealthStatus> {
    if (isDevelopment) {
      return dummyApi.healthStatus();
    }
    return fetchApi<HealthStatus>('/health/status');
  },

  // Bus endpoints
  async getCurrentBuses(): Promise<BusResponse> {
    if (isDevelopment) {
      return dummyApi.getCurrentBuses();
    }
    return fetchApi<BusResponse>('/buses/current');
  },
};