import { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useApp } from '@/contexts/AppContext';
import { Skeleton } from '@/components/ui/skeleton';

interface Props {
  children: ReactNode;
  requiredRole?: 'admin' | 'superadmin';
}

export const ProtectedRoute = ({ children, requiredRole }: Props) => {
  const { user, loading } = useApp();

  if (loading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="space-y-4 w-64">
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      </div>
    );
  }

  if (!user) return <Navigate to="/login" replace />;

  // Rol tekshiruvi — user.roles JWT'dan keladi
  const roles = (user.roles ?? []).map(role => role.toLowerCase());
  const isSuperadmin = roles.includes('superadmin');
  // Boshqaruv paneli: restoran egasi, xodim (menejer/host) yoki superadmin
  const isAdmin = roles.includes('admin') || roles.includes('staff') || isSuperadmin;

  if (requiredRole === 'superadmin' && !isSuperadmin) {
    if (isAdmin) return <Navigate to="/manager" replace />;
    return <Navigate to="/" replace />;
  }
  if (requiredRole === 'admin' && !isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
};
