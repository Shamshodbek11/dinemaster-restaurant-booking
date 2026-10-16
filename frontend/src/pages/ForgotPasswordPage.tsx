import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/components/ui/input-otp';
import { Navbar } from '@/components/Navbar';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { authApi, ApiError } from '@/lib/api';
import { UtensilsCrossed, ArrowLeft, Mail, Loader2, Lock, Eye, EyeOff } from 'lucide-react';
import { toast } from 'sonner';

/**
 * Parolni tiklash (2 bosqich):
 *   1. Email → serverdan 6 xonali kod yuboriladi
 *   2. Kod + yangi parol
 */
const ForgotPasswordPage = () => {
  const { language } = useApp();
  const navigate = useNavigate();
  const [step, setStep] = useState<'email' | 'code'>('email');
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showPw, setShowPw] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const sendCode = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!email.trim()) return;
    setLoading(true);
    setError('');
    try {
      await authApi.forgotPassword(email.trim());
      setStep('code');
    } catch (err) {
      // Email mavjudligi oshkor bo'lmasligi uchun server odatda 200 qaytaradi;
      // bu yerga faqat tarmoq/limit xatolari keladi
      setError(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setLoading(false);
    }
  };

  const reset = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (code.length !== 6) return setError(t('reset.codeRequired', language));
    if (password.length < 8) return setError(t('reset.passwordShort', language));
    if (password !== confirm) return setError(t('reset.passwordMismatch', language));

    setLoading(true);
    try {
      await authApi.resetPassword(email.trim(), code, password);
      toast.success(t('reset.success', language));
      navigate('/login');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('common.error', language));
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
            <h1 className="font-display text-3xl font-bold text-foreground">
              {t('auth.resetPassword', language)}
            </h1>
            <p className="text-muted-foreground mt-2 text-sm">
              {step === 'email' ? t('reset.emailIntro', language) : (
                <>{t('reset.codeSent', language)} <strong className="text-foreground">{email}</strong></>
              )}
            </p>
          </div>

          <div className="bg-card rounded-2xl p-8 border border-border">
            {step === 'email' ? (
              <form onSubmit={sendCode} className="space-y-4">
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                  <Input
                    type="email"
                    placeholder="example@mail.com"
                    value={email}
                    onChange={e => setEmail(e.target.value)}
                    className="pl-10"
                    required
                    autoFocus
                  />
                </div>
                {error && <p className="text-sm text-destructive">{error}</p>}
                <Button type="submit" variant="hero" className="w-full" disabled={loading || !email.trim()}>
                  {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : t('reset.sendCode', language)}
                </Button>
              </form>
            ) : (
              <form onSubmit={reset} className="space-y-4">
                <div className="flex justify-center">
                  <InputOTP maxLength={6} value={code} onChange={v => setCode(v.replace(/\D/g, ''))} autoFocus>
                    <InputOTPGroup>
                      {[0, 1, 2, 3, 4, 5].map(i => <InputOTPSlot key={i} index={i} />)}
                    </InputOTPGroup>
                  </InputOTP>
                </div>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                  <Input
                    type={showPw ? 'text' : 'password'}
                    placeholder={t('reset.newPassword', language)}
                    value={password}
                    onChange={e => setPassword(e.target.value)}
                    className="pl-10 pr-10"
                    minLength={8}
                    required
                  />
                  <button
                    type="button"
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground"
                    onClick={() => setShowPw(v => !v)}
                    aria-label="parolni ko'rsatish"
                  >
                    {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                <Input
                  type={showPw ? 'text' : 'password'}
                  placeholder={t('reset.confirmPassword', language)}
                  value={confirm}
                  onChange={e => setConfirm(e.target.value)}
                  required
                />
                {error && <p className="text-sm text-destructive">{error}</p>}
                <Button type="submit" variant="hero" className="w-full" disabled={loading}>
                  {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : t('reset.submit', language)}
                </Button>
                <button type="button" className="w-full text-sm text-primary hover:underline" onClick={() => sendCode()} disabled={loading}>
                  {t('verify.resend', language)}
                </button>
              </form>
            )}

            <Link to="/login">
              <Button variant="ghost" className="w-full mt-4">
                <ArrowLeft className="w-4 h-4 mr-2" />
                {t('auth.login', language)}
              </Button>
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ForgotPasswordPage;
