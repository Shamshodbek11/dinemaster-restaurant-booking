import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { DepositPayButtons } from '@/components/DepositPayButtons';
import { useApp } from '@/contexts/AppContext';
import { t, tf } from '@/lib/i18n';
import { reservationsApi, restaurantsApi, ApiError, type Reservation, type AvailabilityResult } from '@/lib/api';
import { localDateTimeString } from '@/lib/utils';
import { toast } from 'sonner';
import { CalendarClock, Loader2, Percent, XCircle } from 'lucide-react';
import { cn } from '@/lib/utils';

/** Backend: BookingOptions (FreeCancellationHours, LateCancellationMinutes) bilan bir xil */
const FREE_CANCEL_HOURS = 24;
const NO_CHANGE_MINUTES = 120;

const money = (n: number) => `${Math.round(n).toLocaleString('uz-UZ').replace(/,/g, ' ')} so'm`;

const statusClass: Record<string, string> = {
  confirmed: 'bg-success/20 text-success',
  pending: 'bg-muted text-muted-foreground',
  completed: 'bg-primary/20 text-primary',
  cancelled: 'bg-destructive/20 text-destructive',
  no_show: 'bg-orange-500/20 text-orange-500',
};

const startsAt = (b: Reservation) => new Date(`${b.date}T${b.time}`);
const minutesUntil = (b: Reservation) => (startsAt(b).getTime() - Date.now()) / 60000;

interface Props {
  booking: Reservation;
  onChanged: () => void;
}

export const BookingCard = ({ booking: b, onChanged }: Props) => {
  const { language, refreshProfile } = useApp();
  const [modifyOpen, setModifyOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const active = b.status === 'confirmed' || b.status === 'pending';
  const upcoming = active && `${b.date}T${b.time}` > localDateTimeString();
  const canModify = upcoming && minutesUntil(b) > NO_CHANGE_MINUTES;

  return (
    <div className="bg-card rounded-xl border border-border p-4 sm:p-5 flex flex-col sm:flex-row sm:items-start justify-between gap-3">
      <div className="min-w-0 space-y-1">
        <p className="text-foreground font-medium">{b.restaurantName || b.restaurantId.slice(0, 8)}</p>
        <p className="text-muted-foreground text-sm">
          {b.date.split('-').reverse().join('.')} • {b.time?.slice(0, 5)} • {tf('booking.guestsShort', language, { n: b.guestsCount })}
          {b.tableLabel ? ` • ${t('booking.table', language)} ${b.tableLabel}` : ''}
        </p>
        {b.specialRequests && <p className="text-muted-foreground text-xs italic">"{b.specialRequests}"</p>}
        <div className="flex flex-wrap items-center gap-2 pt-1">
          <span className={cn('text-xs font-medium px-2 py-0.5 rounded-full', statusClass[b.status] ?? 'bg-muted text-muted-foreground')}>
            {t(`status.${b.status}`, language)}
          </span>
          {b.totalDiscount > 0 && (
            <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-primary/15 text-primary inline-flex items-center gap-1">
              <Percent className="w-3 h-3" /> {b.totalDiscount}% {t('booking.off', language)}
            </span>
          )}
        </div>

        {b.status === 'cancelled' && b.cancelledBy === 'restaurant' && (
          <p className="text-xs text-destructive">
            {t('profile.cancelledByRestaurant', language)}{b.cancelReason ? `: ${b.cancelReason}` : ''}
          </p>
        )}
        {b.status === 'cancelled' && b.cancelledBy === 'system' && b.cancelReason && (
          <p className="text-xs text-muted-foreground">{b.cancelReason}</p>
        )}
        {b.isLateCancellation && <p className="text-xs text-orange-500">{t('profile.lateCancellation', language)}</p>}

        {!!b.yumsEarned && (b.status === 'completed' || active) && (
          <p className={cn('text-xs', b.status === 'completed' ? 'text-success' : 'text-muted-foreground')}>
            {b.status === 'completed'
              ? tf('yums.bookingAwarded', language, { n: b.yumsEarned })
              : tf('yums.bookingPending', language, { n: b.yumsEarned })}
          </p>
        )}

        {b.depositAmount > 0 && (
          <div className="pt-1 space-y-2">
            {b.depositOutcome ? (
              <p className={cn('text-xs', b.depositOutcome === 'forfeited' ? 'text-orange-500' : 'text-success')}>
                {t(`deposit.${b.depositOutcome}`, language)}: {money(Number(b.depositAmount))}
              </p>
            ) : b.depositPaid ? (
              <p className="text-xs text-success">{t('payment.depositPaid', language)}: {money(Number(b.depositAmount))}</p>
            ) : upcoming ? (
              <>
                <p className="text-xs text-primary">{t('payment.depositUnpaid', language)}: {money(Number(b.depositAmount))}</p>
                <DepositPayButtons reservationId={b.id} amount={Number(b.depositAmount)} onPaid={onChanged} compact />
              </>
            ) : null}
          </div>
        )}
      </div>

      {upcoming && (
        <div className="flex sm:flex-col gap-2 shrink-0">
          <Button variant="outline" size="sm" disabled={!canModify} title={canModify ? undefined : t('profile.modifyTooLate', language)}
            onClick={() => setModifyOpen(true)}>
            <CalendarClock className="w-4 h-4 mr-1" /> {t('profile.modify', language)}
          </Button>
          <Button variant="outline" size="sm" className="text-destructive hover:text-destructive" onClick={() => setCancelOpen(true)}>
            <XCircle className="w-4 h-4 mr-1" /> {t('profile.cancel', language)}
          </Button>
        </div>
      )}

      {modifyOpen && (
        <ModifyDialog booking={b} onClose={() => setModifyOpen(false)} onDone={() => { setModifyOpen(false); onChanged(); }} />
      )}
      {cancelOpen && (
        <CancelDialog booking={b} onClose={() => setCancelOpen(false)}
          onDone={() => { setCancelOpen(false); onChanged(); refreshProfile().catch(() => {}); }} />
      )}
    </div>
  );
};

const ModifyDialog = ({ booking: b, onClose, onDone }: { booking: Reservation; onClose: () => void; onDone: () => void }) => {
  const { language } = useApp();
  const [date, setDate] = useState(b.date);
  const [time, setTime] = useState(b.time.slice(0, 5));
  const [guests, setGuests] = useState(b.guestsCount);
  const [availability, setAvailability] = useState<AvailabilityResult | null>(null);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [saving, setSaving] = useState(false);
  const todayStr = localDateTimeString().slice(0, 10);

  useEffect(() => {
    if (!date) return;
    let cancelled = false;
    setLoadingSlots(true);
    restaurantsApi.availability(b.restaurantId, date)
      .then(res => { if (!cancelled) setAvailability(res); })
      .catch(() => { if (!cancelled) setAvailability(null); })
      .finally(() => { if (!cancelled) setLoadingSlots(false); });
    return () => { cancelled = true; };
  }, [b.restaurantId, date]);

  const ownSlot = date === b.date ? b.time.slice(0, 5) : null;
  const slots = availability?.slots ?? [];
  const isFree = (s: { time: string; available: boolean }) => s.available || s.time === ownSlot;
  const unchanged = date === b.date && time === b.time.slice(0, 5) && guests === b.guestsCount;

  const save = async () => {
    setSaving(true);
    try {
      await reservationsApi.modify(b.id, { date, time: `${time}:00`, guestsCount: guests, keepTable: true });
      toast.success(t('profile.modified', language));
      onDone();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error', language));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent className="bg-card border-border max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="font-display text-foreground">{t('profile.modifyTitle', language)}</DialogTitle>
          <DialogDescription>{b.restaurantName}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>{t('booking.date', language)}</Label>
              <Input type="date" value={date} min={todayStr} onChange={e => { setDate(e.target.value); setTime(''); }} className="bg-secondary" />
            </div>
            <div className="space-y-2">
              <Label>{t('booking.guests', language)}</Label>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="icon" onClick={() => setGuests(Math.max(1, guests - 1))}>-</Button>
                <span className="w-8 text-center font-bold text-foreground">{guests}</span>
                <Button variant="outline" size="icon" onClick={() => setGuests(Math.min(50, guests + 1))}>+</Button>
              </div>
            </div>
          </div>
          <div className="space-y-2">
            <Label>{t('booking.time', language)}</Label>
            {loadingSlots ? <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" /> :
              slots.length === 0 ? <p className="text-sm text-muted-foreground">{t('booking.closedThatDay', language)}</p> : (
                <div className="grid grid-cols-4 gap-2 max-h-48 overflow-y-auto pr-1">
                  {slots.map(s => (
                    <button key={s.time} disabled={!isFree(s)} onClick={() => setTime(s.time)}
                      className={cn('py-2 rounded-lg text-sm font-medium border',
                        !isFree(s) && 'opacity-40 line-through cursor-not-allowed',
                        time === s.time ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary text-foreground border-border hover:border-primary/50')}>
                      {s.time}
                    </button>
                  ))}
                </div>
              )}
          </div>
          {b.depositPaid && guests > b.guestsCount && (
            <p className="text-xs text-orange-500">{t('profile.modifyMoreGuestsDeposit', language)}</p>
          )}
          <div className="flex gap-3">
            <Button variant="outline" className="flex-1" onClick={onClose}>{t('common.cancel', language)}</Button>
            <Button variant="hero" className="flex-1" disabled={saving || !time || unchanged} onClick={save}>
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : t('common.save', language)}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
};

const CancelDialog = ({ booking: b, onClose, onDone }: { booking: Reservation; onClose: () => void; onDone: () => void }) => {
  const { language } = useApp();
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const hoursLeft = minutesUntil(b) / 60;
  const paidDeposit = b.depositPaid && b.depositAmount > 0;
  const refundable = hoursLeft >= FREE_CANCEL_HOURS;

  const confirm = async () => {
    setSaving(true);
    try {
      const res = await reservationsApi.cancel(b.id, reason.trim());
      toast.success(res.message || t('profile.cancelled', language));
      onDone();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('profile.cancelError', language));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent className="bg-card border-border">
        <DialogHeader>
          <DialogTitle className="font-display text-foreground">{t('profile.cancelTitle', language)}</DialogTitle>
          <DialogDescription>
            {b.restaurantName} — {b.date.split('-').reverse().join('.')} {b.time.slice(0, 5)}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {paidDeposit ? (
            <div className={cn('rounded-xl p-3 text-sm border', refundable ? 'bg-success/10 border-success/30' : 'bg-orange-500/10 border-orange-500/30')}>
              {refundable
                ? tf('profile.cancelRefund', language, { amount: money(Number(b.depositAmount)) })
                : tf('profile.cancelNoRefund', language, { amount: money(Number(b.depositAmount)), h: FREE_CANCEL_HOURS })}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('profile.cancelFree', language)}</p>
          )}
          <div className="space-y-2">
            <Label>{t('profile.cancelReason', language)}</Label>
            <Textarea value={reason} onChange={e => setReason(e.target.value)} rows={2} maxLength={500}
              placeholder={t('profile.cancelReasonPlaceholder', language)} className="bg-secondary" />
          </div>
          <div className="flex gap-3">
            <Button variant="outline" className="flex-1" onClick={onClose}>{t('profile.keepBooking', language)}</Button>
            <Button variant="destructive" className="flex-1" disabled={saving} onClick={confirm}>
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : t('profile.confirmCancel', language)}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
};
