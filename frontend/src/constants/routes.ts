export const ROUTES = {
  HOME: '/',
  BUSES: '/buses',
  LOGIN: '/login',
  REGISTER: '/register',
  ADMIN: '/admin',
  SETTINGS: '/settings',
  PRIVACY: '/privacy',
  TERMS: '/terms',
  DISCLAIMER: '/disclaimer',
} as const;

export type RouteKey = keyof typeof ROUTES;
export type RouteValue = (typeof ROUTES)[RouteKey];
