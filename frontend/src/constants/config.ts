export const config = {
  // API Configuration
  apiBaseUrl: process.env.NEXT_PUBLIC_API_URL || '/api',
  
  // Development Configuration
  isDevelopment: process.env.NODE_ENV === 'development',
  hasBackendProxy: !!process.env.BACKEND_URL,
} as const;