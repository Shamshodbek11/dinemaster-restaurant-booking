import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { dateTime } from '@/components/superadmin/utils';
import { superadminApi, ApiError, type PendingRestaurantChange } from '@/lib/api';
import { toast } from 'sonner';
import { CheckCircle2, XCircle, Loader2 } from 'lucide-react';

const FIELD_LABELS: Record<string, string> = {
  name: 'Nomi',
  description: 'Tavsif',
  cuisineType: 'Oshxona turi',
  priceRange: 'Narx darajasi',
  address: 'Manzil',
  city: 'Shahar',
  latitude: 'Kenglik',
  longitude: 'Uzunlik',
  imagesUrl: 'Rasmlar',
};

const show = (v: unknown) =>
  v === null || v === undefined || v === '' ? '—' : Array.isArray(v) ? `${v.length} ta rasm` : String(v);

/** Restoran egalari o'zgartirgan nom/manzil/tavsif — superadmin tasdiqlaydi yoki rad etadi. */
export const ModerationPanel = () => {
  const [items, setItems] = useState<PendingRestaurantChange[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<string | null>(null);
  const [reason, setReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await superadminApi.pendingChanges());
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const approve = async (id: string) => {
    setBusyId(id);
    try {
      await superadminApi.approveChanges(id);
      toast.success("O'zgarishlar qo'llandi");
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setBusyId(null);
    }
  };

  const reject = async (id: string) => {
    setBusyId(id);
    try {
      await superadminApi.rejectChanges(id, reason);
      toast.success('Rad etildi, egasiga xabar yuborildi');
      setRejecting(null);
      setReason('');
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setBusyId(null);
    }
  };

  if (loading) return <Loader2 className="w-6 h-6 animate-spin text-muted-foreground" />;

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-display text-2xl font-bold text-foreground">Moderatsiya</h3>
        <p className="text-muted-foreground text-sm">
          Tasdiqlangan restoranlar egalari o'zgartirgan ma'lumotlar. Tasdiqlanmaguncha mijozlarga eski qiymatlar ko'rinadi.
        </p>
      </div>

      {items.length === 0 && (
        <p className="text-muted-foreground text-center py-12">Tasdiq kutayotgan o'zgarish yo'q.</p>
      )}

      {items.map(item => (
        <div key={item.id} className="bg-card rounded-xl border border-border p-5 space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="font-display text-lg font-bold text-foreground">{item.name}</p>
              {item.submittedAt && (
                <p className="text-xs text-muted-foreground">{dateTime(item.submittedAt)}</p>
              )}
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="hero" disabled={busyId === item.id} onClick={() => approve(item.id)}>
                <CheckCircle2 className="w-4 h-4 mr-1" /> Tasdiqlash
              </Button>
              <Button size="sm" variant="outline" disabled={busyId === item.id} onClick={() => setRejecting(item.id)}>
                <XCircle className="w-4 h-4 mr-1" /> Rad etish
              </Button>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-muted-foreground">
                  <th className="py-1 pr-4 font-medium">Maydon</th>
                  <th className="py-1 pr-4 font-medium">Hozirgi</th>
                  <th className="py-1 font-medium">Yangi</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(item.pending).map(([field, value]) => (
                  <tr key={field} className="border-t border-border align-top">
                    <td className="py-2 pr-4 text-muted-foreground">{FIELD_LABELS[field] ?? field}</td>
                    <td className="py-2 pr-4 text-foreground/70 line-through">
                      {show((item.current as Record<string, unknown>)[field])}
                    </td>
                    <td className="py-2 text-foreground font-medium">{show(value)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {rejecting === item.id && (
            <div className="space-y-2">
              <Textarea
                value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder="Rad etish sababi (egasiga yuboriladi)"
                className="bg-secondary"
                rows={2}
              />
              <div className="flex gap-2">
                <Button size="sm" variant="destructive" disabled={busyId === item.id} onClick={() => reject(item.id)}>
                  Rad etishni tasdiqlash
                </Button>
                <Button size="sm" variant="ghost" onClick={() => { setRejecting(null); setReason(''); }}>
                  Bekor
                </Button>
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};
