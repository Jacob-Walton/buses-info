import { BusStatus, BusResponse, HealthStatus } from './api';

// Dummy bus data for development
const dummyBuses: BusStatus[] = [
  { service: '101', bay: 'A1' },
  { service: '102', bay: 'A2' },
  { service: '103', bay: null },
  { service: '104', bay: 'B5' },
  { service: '105', bay: null },
  { service: '106', bay: 'C12' },
  { service: '107', bay: 'T1' },
  { service: '108', bay: null },
  { service: '109', bay: 'A15' },
  { service: '110', bay: 'B3' },
  { service: '111', bay: null },
  { service: '112', bay: 'C7' },
  { service: '113', bay: 'A8' },
  { service: '114', bay: null },
  { service: '115', bay: 'T2' },
  { service: '116', bay: 'B11' },
  { service: '117', bay: null },
  { service: '118', bay: 'C16' },
  { service: '119', bay: 'A4' },
  { service: '120', bay: null },
  { service: '121', bay: 'B9' },
  { service: '122', bay: 'C1' },
  { service: '123', bay: null },
  { service: '124', bay: 'A13' },
  { service: '125', bay: 'B2' },
];

const dummyHealthStatus: HealthStatus = {
  database: true,
  site: true,
};

// Simulate network delay
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

// Simulate occasional errors for testing
const shouldSimulateError = () => Math.random() < 0.05; // 5% chance of error

export const dummyApi = {
  async healthCheck(): Promise<string> {
    await delay(200);
    if (shouldSimulateError()) {
      throw new Error('Health check failed');
    }
    return 'OK';
  },

  async healthStatus(): Promise<HealthStatus> {
    await delay(300);
    if (shouldSimulateError()) {
      throw new Error('Unable to retrieve health status');
    }
    return dummyHealthStatus;
  },

  async getCurrentBuses(): Promise<BusResponse> {
    await delay(200 + Math.random() * 200); // 200-400ms delay
    
    if (shouldSimulateError()) {
      throw new Error('Failed to fetch bus data from external service');
    }

    return {
      buses: dummyBuses,
      cached: Math.random() < 0.3, // 30% chance of cached data
    };
  },
};