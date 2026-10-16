import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useApp } from '@/contexts/AppContext';
import { t, tf } from '@/lib/i18n';
import { BRAND } from '@/lib/brand';
import { profileApi, type ReferralInfo } from '@/lib/api';
import { toast } from 'sonner';
import { Copy, Gift, Share2, UserPlus, CheckCircle2, Sparkles } from 'lucide-react';

export const ReferralPanel = () => {
  const { language } = useApp();
  const [info, setInfo] = useState<ReferralInfo | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    profileApi.referral().then(setInfo).catch(() => setError(true));
  }, []);

  if (error) return <p className="text-muted-foreground">{t('common.error', language)}</p>;
  if (!info) return <div className="h-40 rounded-xl bg-card border border-border animate-pulse" />;

  const link = `${window.location.origin}/signup?ref=${info.code}`;
  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success(t('profile.copied', language));
    } catch {
      toast.error(t('common.error', language));
    }
  };
  const share = async () => {
    const text = tf('referral.shareText', language, { brand: BRAND, n: info.bonusPerFriend });
    if (navigator.share) {
      try { await navigator.share({ title: BRAND, text, url: link }); } catch { /* foydalanuvchi bekor qildi */ }
    } else {
      copy(`${text} ${link}`);
    }
  };

  return (
    <div className="space-y-4">
      <div className="bg-card rounded-xl border border-border p-5 sm:p-6 space-y-4">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary/15 flex items-center justify-center shrink-0">
            <Gift className="w-5 h-5 text-primary" />
          </div>
          <p className="text-foreground text-sm">{tf('referral.howItWorks', language, { n: info.bonusPerFriend })}</p>
        </div>
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">{t('referral.yourCode', language)}</p>
          <div className="flex gap-2">
            <Input value={info.code} readOnly className="bg-secondary font-mono tracking-widest text-lg w-40" />
            <Button variant="outline" onClick={() => copy(info.code)} aria-label={t('profile.copied', language)}><Copy className="w-4 h-4" /></Button>
          </div>
        </div>
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">{t('referral.yourLink', language)}</p>
          <div className="flex gap-2">
            <Input value={link} readOnly className="bg-secondary text-sm" />
            <Button variant="outline" onClick={() => copy(link)} aria-label={t('profile.copied', language)}><Copy className="w-4 h-4" /></Button>
            <Button variant="hero" onClick={share}><Share2 className="w-4 h-4 sm:mr-1" /><span className="hidden sm:inline">{t('referral.share', language)}</span></Button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-3">
        {[
          { icon: UserPlus, value: info.invited, label: t('referral.invited', language) },
          { icon: CheckCircle2, value: info.rewarded, label: t('referral.visited', language) },
          { icon: Sparkles, value: info.earned, label: t('referral.earned', language) },
        ].map(s => (
          <div key={s.label} className="bg-card rounded-xl border border-border p-4 text-center">
            <s.icon className="w-5 h-5 text-primary mx-auto mb-1" />
            <div className="font-display text-xl font-bold text-foreground">{s.value}</div>
            <div className="text-xs text-muted-foreground">{s.label}</div>
          </div>
        ))}
      </div>
    </div>
  );
};
