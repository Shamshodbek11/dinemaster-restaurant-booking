import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { paymentsApi, ApiError, type PaymentProvider } from '@/lib/api';
import { CreditCard, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

const LABELS: Record<PaymentProvider, string> = {
  payme: 'Payme',
  click: 'Click',
  test: 'Test',
};

interface Props {
  reservationId: string;
  amount: number;
  /** test provayderda to'lov darhol o'tadi — ro'yxatni yangilash uchun */
  onPaid?: () => void;
  compact?: boolean;
}

/** Bron depozitini Payme / Click (yoki lokal test) orqali to'lash tugmalari. */
export const DepositPayButtons = ({ reservationId, amount, onPaid, compact }: Props) => {
  const { language } = useApp();
  const [providers, setProviders] = useState<PaymentProvider[] | null>(null);
  const [busy, setBusy] = useState<PaymentProvider | null>(null);

  useEffect(() => {
    paymentsApi.providers().then(r => setProviders(r.providers)).catch(() => setProviders([]));
  }, []);

  const pay = async (provider: PaymentProvider) => {
    setBusy(provider);
    try {
      const res = await paymentsApi.deposit(reservationId, provider);
      if (res.paid) {
        toast.success(t('payment.paid', language));
        onPaid?.();
      } else {
        window.location.href = res.redirectUrl;
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setBusy(null);
    }
  };

  if (providers === null) return null;
  if (providers.length === 0) {
    return <p className="text-xs text-muted-foreground">{t('payment.atRestaurant', language)}</p>;
  }

  return (
    <div className={compact ? 'flex flex-wrap gap-2' : 'space-y-2'}>
      {!compact && (
        <p className="text-sm text-foreground flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-primary" />
          {t('payment.payDeposit', language)}: <strong>{amount.toLocaleString()} so'm</strong>
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        {providers.map(p => (
          <Button
            key={p}
            size="sm"
            variant={p === 'test' ? 'outline' : 'hero'}
            disabled={busy !== null}
            onClick={() => pay(p)}
          >
            {busy === p ? <Loader2 className="w-4 h-4 animate-spin" /> : p === 'test' ? t('payment.testProvider', language) : LABELS[p]}
          </Button>
        ))}
      </div>
    </div>
  );
};
