import { useEffect, useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/components/ui/input-otp';
import { Navbar } from '@/components/Navbar';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { authApi, ApiError } from '@/lib/api';
import { MailCheck, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

const RESEND_SECONDS = 60;

/** Ro'yxatdan o'tgandan keyin emailga kelgan 6 xonali kodni kiritish. */
const VerifyEmailPage = () => {
  const { language, user, loading, refreshUser } = useApp();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = params.get('next') || '/';

  const [code, setCode] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sending, setSending] = useState(false);
  const [cooldown, setCooldown] = useState(RESEND_SECONDS);

  useEffect(() => {
    if (cooldown <= 0) return;
    const id = setTimeout(() => setCooldown(c => c - 1), 1000);
    return () => clearTimeout(id);
  }, [cooldown]);

  if (loading) return null;
  if (!user) return <Navigate to="/login" replace />;
  if (user.emailConfirmed !== false) return <Navigate to={next} replace />;

  const submit = async (value = code) => {
    if (value.length !== 6) return;
    setSubmitting(true);
    try {
      await authApi.verifyEmail(value);
      await refreshUser();
      toast.success(t('verify.success', language));
      navigate(next, { replace: true });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
      setCode('');
    } finally {
      setSubmitting(false);
    }
  };

  const resend = async () => {
    setSending(true);
    try {
      const res = await authApi.sendEmailCode();
      toast.success(res.message);
      setCooldown(RESEND_SECONDS);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 flex items-center justify-center px-4">
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div className="w-14 h-14 rounded-2xl bg-gradient-amber flex items-center justify-center mx-auto mb-4 shadow-glow">
              <MailCheck className="w-7 h-7 text-primary-foreground" />
            </div>
            <h1 className="font-display text-3xl font-bold text-foreground">{t('verify.title', language)}</h1>
            <p className="text-muted-foreground mt-2 text-sm">
              {t('verify.sentTo', language)} <strong className="text-foreground">{user.email}</strong>
            </p>
          </div>

          <div className="bg-card rounded-2xl p-8 border border-border space-y-6">
            <div className="flex justify-center">
              <InputOTP
                maxLength={6}
                value={code}
                onChange={v => {
                  const digits = v.replace(/\D/g, '');
                  setCode(digits);
                  if (digits.length === 6) submit(digits);
                }}
                disabled={submitting}
                autoFocus
              >
                <InputOTPGroup>
                  {[0, 1, 2, 3, 4, 5].map(i => <InputOTPSlot key={i} index={i} />)}
                </InputOTPGroup>
              </InputOTP>
            </div>

            <Button variant="hero" className="w-full" disabled={submitting || code.length !== 6} onClick={() => submit()}>
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : t('verify.confirm', language)}
            </Button>

            <div className="text-center text-sm text-muted-foreground">
              {cooldown > 0 ? (
                <span>{t('verify.resendIn', language).replace('{n}', String(cooldown))}</span>
              ) : (
                <button className="text-primary hover:underline disabled:opacity-50" onClick={resend} disabled={sending}>
                  {t('verify.resend', language)}
                </button>
              )}
            </div>

            <p className="text-xs text-muted-foreground bg-secondary rounded-lg p-3">
              {t('verify.hint', language)}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default VerifyEmailPage;
