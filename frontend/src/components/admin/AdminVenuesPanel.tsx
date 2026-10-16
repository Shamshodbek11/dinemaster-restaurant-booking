import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/superadmin/common';
import { money } from '@/components/superadmin/utils';
import { venuesApi, ApiError, type Venue, type VenueOwnerReservation } from '@/lib/api';
import { toast } from 'sonner';
import { Building2, CheckCircle2, Loader2, XCircle } from 'lucide-react';

const fmtDate = (d: string) => d.slice(0, 10).split('-').reverse().join('.');

/** Zal egasi: o'z tadbir zallari va ularga kelgan bronlar (tasdiqlash / bekor qilish / yakunlash). */
export const AdminVenuesPanel = ({ venues }: { venues: Venue[] }) => {
  const [items, setItems] = useState<VenueOwnerReservation[] | null>(null);
  const [status, setStatus] = useState('pending');
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(() => {
    setItems(null);
    venuesApi.ownerReservations(status === 'all' ? undefined : status)
      .then(setItems)
      .catch(err => { setItems([]); toast.error(err instanceof ApiError ? err.message : "Yuklab bo'lmadi"); });
  }, [status]);

  useEffect(() => { load(); }, [load]);

  const setReservationStatus = async (r: VenueOwnerReservation, next: 'confirmed' | 'cancelled' | 'completed') => {
    let note: string | undefined;
    if (next === 'cancelled') {
      const input = prompt('Bekor qilish sababi (mijozga yuboriladi):');
      if (input === null) return;
      note = input.trim() || undefined;
    }
    setBusy(r.id);
    try {
      await venuesApi.setReservationStatus(r.id, next, note);
      toast.success('Saqlandi — mijozga xabar yuborildi');
      load();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Xatolik');
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-display text-2xl font-bold text-foreground">Tadbir zallari</h3>
        <p className="text-sm text-muted-foreground">To'y, banket va tadbirlar uchun kelgan so'rovlar</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {venues.map(v => (
          <div key={v.id} className="bg-card rounded-xl border border-border p-4">
            <div className="flex items-center gap-2 mb-1">
              <Building2 className="w-4 h-4 text-primary" />
              <p className="font-medium text-foreground truncate">{v.name}</p>
            </div>
            <p className="text-xs text-muted-foreground">{v.city} · {v.capacityMin}–{v.capacityMax} kishi · {money(v.pricePerHead)} / kishi</p>
            <p className="text-xs mt-1">
              {v.isSuspended ? <span className="text-destructive">To'xtatilgan</span>
                : v.isApproved ? <span className="text-success">Faol</span>
                : <span className="text-orange-500">Tasdiq kutilmoqda</span>}
            </p>
          </div>
        ))}
      </div>

      <div className="flex flex-wrap gap-2">
        {[['pending', 'Yangi so\'rovlar'], ['confirmed', 'Tasdiqlangan'], ['completed', 'Yakunlangan'], ['cancelled', 'Bekor qilingan'], ['all', 'Hammasi']].map(([v, l]) => (
          <Button key={v} size="sm" variant={status === v ? 'default' : 'outline'} onClick={() => setStatus(v)}>{l}</Button>
        ))}
      </div>

      {items === null ? (
        <div className="h-24 rounded-xl bg-card border border-border animate-pulse" />
      ) : items.length === 0 ? (
        <p className="text-center text-muted-foreground py-10">So'rovlar yo'q</p>
      ) : (
        <div className="space-y-3">
          {items.map(r => (
            <div key={r.id} className="bg-card rounded-xl border border-border p-4 flex flex-col md:flex-row md:items-center gap-3">
              <div className="flex-1 min-w-0 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="font-medium text-foreground">{r.venueName}</p>
                  <StatusBadge status={r.status} />
                </div>
                <p className="text-sm text-muted-foreground">
                  {fmtDate(r.eventDate)} · {r.eventType} · {r.guestsCount} kishi · {money(r.totalPrice)}
                </p>
                <p className="text-sm text-foreground">
                  {r.userFullName} {r.userPhone && <a href={`tel:${r.userPhone}`} className="text-primary ml-1">{r.userPhone}</a>}
                  {r.userEmail && <span className="text-muted-foreground ml-1">· {r.userEmail}</span>}
                </p>
                {r.specialRequests && <p className="text-xs text-muted-foreground italic">"{r.specialRequests}"</p>}
              </div>
              <div className="flex gap-2 flex-wrap">
                {busy === r.id ? <Loader2 className="w-4 h-4 animate-spin" /> : (
                  <>
                    {r.status === 'pending' && (
                      <Button size="sm" onClick={() => setReservationStatus(r, 'confirmed')}><CheckCircle2 className="w-4 h-4 mr-1" /> Tasdiqlash</Button>
                    )}
                    {r.status === 'confirmed' && (
                      <Button size="sm" variant="outline" onClick={() => setReservationStatus(r, 'completed')}>Yakunlandi</Button>
                    )}
                    {(r.status === 'pending' || r.status === 'confirmed') && (
                      <Button size="sm" variant="outline" className="text-destructive" onClick={() => setReservationStatus(r, 'cancelled')}>
                        <XCircle className="w-4 h-4 mr-1" /> Bekor
                      </Button>
                    )}
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
