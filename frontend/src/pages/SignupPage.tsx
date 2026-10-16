import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { Navbar } from '@/components/Navbar';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { ApiError } from '@/lib/api';
import { toast } from 'sonner';
import { Gift, UtensilsCrossed } from 'lucide-react';

const REF_KEY = 'dm-ref';

const SignupPage = () => {
  const { language, register } = useApp();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = params.get('next');
  const safeNext = next && next.startsWith('/') && !next.startsWith('//') ? next : null;
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [referralCode, setReferralCode] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [loading, setLoading] = useState(false);

  // Taklif havolasi: /signup?ref=KOD — kod sahifa yangilansa ham saqlanadi
  useEffect(() => {
    const fromUrl = params.get('ref')?.trim().toUpperCase();
    try {
      if (fromUrl) localStorage.setItem(REF_KEY, fromUrl);
      setReferralCode(fromUrl || localStorage.getItem(REF_KEY) || '');
    } catch {
      if (fromUrl) setReferralCode(fromUrl);
    }
  }, [params]);

  const handleSignup = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!acceptTerms) {
      toast.error(t('auth.termsRequired', language));
      return;
    }
    setLoading(true);
    try {
      await register({
        email,
        password,
        fullName: name,
        acceptTerms,
        referralCode: referralCode.trim() || undefined,
      });
      try { localStorage.removeItem(REF_KEY); } catch { /* ahamiyatsiz */ }
      toast.success(t('auth.signupSuccess', language));
      // Emailga kod yuborildi — tasdiqlash sahifasiga
      navigate(safeNext ? `/verify-email?next=${encodeURIComponent(safeNext)}` : '/verify-email');
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message :
        err instanceof Error ? err.message :
        t('auth.signupError', language);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 pb-12 flex items-center justify-center px-4">
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div className="w-14 h-14 rounded-2xl bg-gradient-amber flex items-center justify-center mx-auto mb-4 shadow-glow">
              <UtensilsCrossed className="w-7 h-7 text-primary-foreground" />
            </div>
            <h1 className="font-display text-3xl font-bold text-foreground">{t('auth.signup', language)}</h1>
          </div>
          <form onSubmit={handleSignup} className="space-y-4 bg-card rounded-2xl p-6 sm:p-8 border border-border">
            {referralCode && (
              <div className="flex items-center gap-2 bg-primary/10 border border-primary/30 rounded-xl p-3 text-sm text-foreground">
                <Gift className="w-4 h-4 text-primary shrink-0" /> {t('auth.invitedByFriend', language)}
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="su-name">{t('auth.name', language)}</Label>
              <Input id="su-name" value={name} onChange={e => setName(e.target.value)} required autoComplete="name" placeholder={t('auth.namePlaceholder', language)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-email">{t('auth.email', language)}</Label>
              <Input id="su-email" type="email" value={email} onChange={e => setEmail(e.target.value)} required autoComplete="email" placeholder="email@example.com" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-pass">{t('auth.password', language)}</Label>
              <Input id="su-pass" type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} autoComplete="new-password" placeholder="••••••••" />
              <p className="text-xs text-muted-foreground">{t('auth.passwordHint', language)}</p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-ref">{t('auth.referralCode', language)}</Label>
              <Input id="su-ref" value={referralCode} onChange={e => setReferralCode(e.target.value.toUpperCase())} maxLength={12} className="font-mono uppercase" placeholder={t('auth.optional', language)} />
            </div>
            <div className="flex items-start gap-2">
              <Checkbox id="su-terms" checked={acceptTerms} onCheckedChange={v => setAcceptTerms(v === true)} className="mt-0.5" />
              <Label htmlFor="su-terms" className="text-sm font-normal leading-snug text-muted-foreground cursor-pointer">
                {t('auth.acceptPrefix', language)}{' '}
                <Link to="/terms" target="_blank" className="text-primary hover:underline">{t('footer.terms', language)}</Link>
                {' '}{t('auth.and', language)}{' '}
                <Link to="/privacy" target="_blank" className="text-primary hover:underline">{t('footer.privacy', language)}</Link>
                {t('auth.acceptSuffix', language)}
              </Label>
            </div>
            <Button type="submit" variant="hero" className="w-full" disabled={loading || !acceptTerms}>
              {loading ? '...' : t('auth.signup', language)}
            </Button>
            <div className="text-center text-sm text-muted-foreground">
              {t('auth.hasAccount', language)}{' '}
              <Link to="/login" className="text-primary hover:underline">{t('auth.login', language)}</Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
};

export default SignupPage;
