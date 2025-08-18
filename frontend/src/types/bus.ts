export interface BusInfo {
  number: string;
  bay: string | null;
  lastArrival: string | null;
  status: 'arrived' | 'not-arrived';
  isPreferred: boolean;
}

export interface BusSection {
  name: string;
  buses: BusInfo[];
  mapUrl?: string;
  isCollapsed: boolean;
}

export interface BusPrediction {
  busNumber: string;
  predictions: {
    bay: string;
    probability: number;
  }[];
  confidence: number;
}

// API-related interfaces
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