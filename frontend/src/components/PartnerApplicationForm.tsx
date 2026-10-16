import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { partnerApi, isAdmin, ApiError, type PartnerApplication } from '@/lib/api';
import { CheckCircle2, Clock, Loader2, XCircle } from 'lucide-react';
import { toast } from 'sonner';

/**
 * "Restoran egasimisiz?" — hamkorlik arizasi. Superadmin tasdiqlasa foydalanuvchiga restoran
 * egasi (admin) roli beriladi va u boshqaruv panelida restoranini qo'shadi.
 */
export const PartnerApplicationForm = () => {
  const { user, language, profile } = useApp();
  const [application, setApplication] = useState<PartnerApplication | null | undefined>(undefined);
  const [form, setForm] = useState({ restaurantName: '', city: '', address: '', phone: '', message: '' });
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!user) return;
    partnerApi.mine().then(a => setApplication(a ?? null)).catch(() => setApplication(null));
  }, [user]);

  useEffect(() => {
    if (profile?.phone) setForm(f => (f.phone ? f : { ...f, phone: profile.phone ?? '' }));
  }, [profile?.phone]);

  if (!user) return null;

  if (isAdmin(user)) {
    return (
      <div className="bg-card rounded-xl border border-border p-6 space-y-3">
        <p className="text-foreground">{t('partner.alreadyOwner', language)}</p>
        <Link to="/manager"><Button variant="hero">{t('partner.openDashboard', language)}</Button></Link>
      </div>
    );
  }

  if (application === undefined) return <Loader2 className="w-5 h-5 animate-spin text-muted-foreground" />;

  if (application?.status === 'pending') {
    return (
      <div className="bg-card rounded-xl border border-border p-6 flex gap-3">
        <Clock className="w-6 h-6 text-primary shrink-0" />
        <div>
          <p className="font-medium text-foreground">{t('partner.pendingTitle', language)}</p>
          <p className="text-sm text-muted-foreground mt-1">
            {application.restaurantName} ({application.city}) — {t('partner.pendingText', language)}
          </p>
        </div>
      </div>
    );
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const created = await partnerApi.submit({
        restaurantName: form.restaurantName.trim(),
        city: form.city.trim(),
        address: form.address.trim() || undefined,
        phone: form.phone.trim(),
        message: form.message.trim() || undefined,
      });
      setApplication(created);
      toast.success(t('partner.sent', language));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      {application?.status === 'rejected' && (
        <div className="bg-destructive/10 border border-destructive/30 rounded-xl p-4 flex gap-3 text-sm">
          <XCircle className="w-5 h-5 text-destructive shrink-0" />
          <div>
            <p className="font-medium text-foreground">{t('partner.rejectedTitle', language)}</p>
            <p className="text-muted-foreground">{application.rejectionReason}</p>
          </div>
        </div>
      )}
      {application?.status === 'approved' && (
        <div className="bg-success/10 border border-success/30 rounded-xl p-4 flex gap-3 text-sm">
          <CheckCircle2 className="w-5 h-5 text-success shrink-0" />
          <p className="text-foreground">{t('partner.approvedText', language)}</p>
        </div>
      )}
      <form onSubmit={submit} className="bg-card rounded-xl border border-border p-6 space-y-4">
        <div className="space-y-2">
          <Label>{t('profile.restaurantName', language)} *</Label>
          <Input value={form.restaurantName} onChange={e => setForm({ ...form, restaurantName: e.target.value })}
            placeholder={t('profile.restaurantNamePlaceholder', language)} required maxLength={200} className="bg-secondary" />
        </div>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label>{t('partner.city', language)} *</Label>
            <Input value={form.city} onChange={e => setForm({ ...form, city: e.target.value })}
              placeholder="Toshkent" required maxLength={100} className="bg-secondary" />
          </div>
          <div className="space-y-2">
            <Label>{t('detail.phone', language)} *</Label>
            <Input value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })}
              placeholder="+998 90 123 45 67" required maxLength={50} className="bg-secondary" />
          </div>
        </div>
        <div className="space-y-2">
          <Label>{t('detail.address', language)}</Label>
          <Input value={form.address} onChange={e => setForm({ ...form, address: e.target.value })}
            placeholder="Ko'cha, uy" className="bg-secondary" />
        </div>
        <div className="space-y-2">
          <Label>{t('partner.message', language)}</Label>
          <Textarea value={form.message} onChange={e => setForm({ ...form, message: e.target.value })}
            rows={3} className="bg-secondary" placeholder={t('partner.messagePlaceholder', language)} />
        </div>
        <Button type="submit" variant="hero" disabled={saving}>
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : t('partner.submit', language)}
        </Button>
      </form>
    </div>
  );
};
