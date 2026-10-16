import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Navbar } from '@/components/Navbar';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { ApiError } from '@/lib/api';
import { toast } from 'sonner';
import { UtensilsCrossed } from 'lucide-react';

const LoginPage = () => {
  const { language, login } = useApp();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  // Faqat ichki yo'llar (ochiq redirect bo'lmasin)
  const next = params.get('next');
  const safeNext = next && next.startsWith('/') && !next.startsWith('//') ? next : null;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const user = await login(email, password);
      toast.success(t('auth.welcomeBack', language));

      // Rol asosida yo'naltirish
      const roles = user.roles ?? [];
      if (safeNext) {
        navigate(safeNext);
      } else if (roles.includes('superadmin')) {
        navigate('/superadmin-dashboard');
      } else if (roles.includes('admin') || roles.includes('staff')) {
        navigate('/manager');
      } else {
        navigate('/');
      }
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message :
        err instanceof Error ? err.message :
        t('auth.loginError', language);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 flex items-center justify-center px-4">
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div className="w-14 h-14 rounded-2xl bg-gradient-amber flex items-center justify-center mx-auto mb-4 shadow-glow">
              <UtensilsCrossed className="w-7 h-7 text-primary-foreground" />
            </div>
            <h1 className="font-display text-3xl font-bold text-foreground">{t('auth.login', language)}</h1>
          </div>
          <form onSubmit={handleLogin} className="space-y-4 bg-card rounded-2xl p-8 border border-border">
            <div className="space-y-2">
              <Label>{t('auth.email', language)}</Label>
              <Input type="email" value={email} onChange={e => setEmail(e.target.value)} required placeholder="email@example.com" />
            </div>
            <div className="space-y-2">
              <Label>{t('auth.password', language)}</Label>
              <Input type="password" value={password} onChange={e => setPassword(e.target.value)} required placeholder="••••••••" />
            </div>
            <Button type="submit" variant="hero" className="w-full" disabled={loading}>
              {loading ? '...' : t('auth.login', language)}
            </Button>
            <div className="text-center text-sm text-muted-foreground">
              <Link to="/forgot-password" className="text-primary hover:underline">{t('auth.forgotPassword', language)}</Link>
            </div>
            <div className="text-center text-sm text-muted-foreground">
              {t('auth.noAccount', language)}{' '}
              <Link to="/signup" className="text-primary hover:underline">{t('auth.signup', language)}</Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
};

export default LoginPage;
