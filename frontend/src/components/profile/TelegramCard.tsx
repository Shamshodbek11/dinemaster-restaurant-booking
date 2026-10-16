import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { profileApi, ApiError } from '@/lib/api';
import { toast } from 'sonner';
import { Send, Loader2, RefreshCw } from 'lucide-react';

/** Bildirishnomalarni Telegram'ga ham olish: bot havolasi orqali ulanadi (/start KOD). */
export const TelegramCard = () => {
  const { language, profile, refreshProfile } = useApp();
  const [busy, setBusy] = useState(false);
  const [waiting, setWaiting] = useState(false);
  const linked = !!profile?.telegramLinked;

  const link = async () => {
    setBusy(true);
    // Brauzer popup'ni bloklamasligi uchun oynani darhol ochamiz
    const win = window.open('', '_blank');
    try {
      const { url } = await profileApi.telegramLink();
      if (win) win.location.href = url; else window.location.href = url;
      setWaiting(true);
    } catch (err) {
      win?.close();
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setBusy(false);
    }
  };

  const unlink = async () => {
    setBusy(true);
    try {
      await profileApi.telegramUnlink();
      await refreshProfile();
      toast.success(t('telegram.unlinked', language));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setBusy(false);
    }
  };

  const check = async () => {
    await refreshProfile();
    setWaiting(false);
  };

  return (
    <div className="bg-card rounded-xl border border-border p-5 space-y-3">
      <div className="flex items-center gap-3">
        <Send className="w-5 h-5 text-sky-500" />
        <div className="flex-1">
          <p className="text-foreground font-medium">Telegram</p>
          <p className="text-xs text-muted-foreground">
            {linked ? t('telegram.linked', language) : t('telegram.description', language)}
          </p>
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        {linked ? (
          <Button variant="outline" size="sm" onClick={unlink} disabled={busy}>{t('telegram.unlink', language)}</Button>
        ) : (
          <Button variant="hero" size="sm" onClick={link} disabled={busy}>
            {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : t('telegram.link', language)}
          </Button>
        )}
        {waiting && !linked && (
          <Button variant="outline" size="sm" onClick={check}>
            <RefreshCw className="w-4 h-4 mr-1" /> {t('telegram.check', language)}
          </Button>
        )}
      </div>
      {waiting && !linked && <p className="text-xs text-muted-foreground">{t('telegram.waitingHint', language)}</p>}
    </div>
  );
};
