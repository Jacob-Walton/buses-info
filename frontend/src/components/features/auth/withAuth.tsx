'use client';

import { useEffect, ComponentType } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/hooks';

interface WithAuthOptions {
  requiredRoles?: string[];
  redirectTo?: string;
}

export function withAuth<P extends object>(
  Component: ComponentType<P>,
  options: WithAuthOptions = {}
) {
  const { requiredRoles = [], redirectTo = '/login' } = options;

  return function AuthenticatedComponent(props: P) {
    const { isAuthenticated, isLoading, user } = useAuth();
    const router = useRouter();

    useEffect(() => {
      if (!isLoading) {
        if (!isAuthenticated) {
          router.push(redirectTo);
          return;
        }

        if (requiredRoles.length > 0 && user) {
          const userRole = user.role.toLowerCase();
          const hasRequiredRole = requiredRoles.some(role => 
            role.toLowerCase() === userRole
          );

          if (!hasRequiredRole) {
            router.push('/unauthorized');
            return;
          }
        }
      }
    }, [isAuthenticated, isLoading, user, router]);

    if (isLoading) {
      return (
        <div style={{ 
          display: 'flex', 
          justifyContent: 'center', 
          alignItems: 'center', 
          height: '100vh',
          fontSize: '18px'
        }}>
          Loading...
        </div>
      );
    }

    if (!isAuthenticated) {
      return null;
    }

    if (requiredRoles.length > 0 && user) {
      const userRole = user.role.toLowerCase();
      const hasRequiredRole = requiredRoles.some(role => 
        role.toLowerCase() === userRole
      );

      if (!hasRequiredRole) {
        return null;
      }
    }

    return <Component {...props} />;
  };
}
