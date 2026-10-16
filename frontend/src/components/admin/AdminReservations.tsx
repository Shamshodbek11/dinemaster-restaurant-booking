import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { StatusBadge } from '@/components/superadmin/common';
import { money } from '@/components/superadmin/utils';
import {
  adminApi, reservationsApi, tablesApi, ApiError,
  type AccessLevel, type Reservation, type RestaurantTable,
} from '@/lib/api';
import { toast } from 'sonner';
import { BadgeDollarSign, CalendarDays, CheckCircle2, Loader2, Undo2, UserX, XCircle } from 'lucide-react';
import { cn } from '@/lib/utils';

interface Props {
  restaurantId: string;
  accessLevel: AccessLevel;
  reservations: Reservation[];
  onUpdate: (id: string, patch: Partial<Reservation>) => void;
  filterDate: string;
  onFilterDate: (date: string) => void;
  today: string;
  /** Serverdan qayta yuklash (depozit holati kabi server hisoblaydigan maydonlar uchun) */
  onReload: () => void;
}

const STATUS_OPTIONS = [
  { value: 'all', label: 'Hammasi' },
  { value: 'active', label: 'Faol (kutilayotgan)' },
  { value: 'confirmed', label: 'Tasdiqlangan' },
  { value: 'completed', label: 'Yakunlangan (keldi)' },
  { value: 'cancelled', label: 'Bekor qilingan' },
  { value: 'no_show', label: 'Kelmadi' },
];

const DEPOSIT_OUTCOME: Record<string, { label: string; cls: string }> = {
  refund_requested: { label: 'qaytarilishi kerak', cls: 'bg-orange-500/20 text-orange-500' },
  refunded: { label: 'qaytarildi', cls: 'bg-muted text-muted-foreground' },
  forfeited: { label: 'restoranda qoldi', cls: 'bg-green-500/20 text-green-500' },
};

const startsAt = (r: Reservation) => new Date(`${r.date}T${r.time}`).getTime();
const isActive = (r: Reservation) => r.status === 'confirmed' || r.status === 'pending';
const fmtDate = (d: string) => d.split('-').reverse().join('.');

export const AdminReservations = ({ restaurantId, accessLevel, reservations, onUpdate, filterDate, onFilterDate, today, onReload }: Props) => {
  const [filterStatus, setFilterStatus] = useState('active');
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [cancelTarget, setCancelTarget] = useState<Reservation | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const canManageMoney = accessLevel === 'owner' || accessLevel === 'manager';

  useEffect(() => {
    tablesApi.list(restaurantId).then(setTables).catch(() => setTables([]));
  }, [restaurantId]);

  const filtered = reservations
    .filter(r => {
      if (filterDate && r.date !== filterDate) return false;
      if (filterStatus === 'active') return isActive(r);
      if (filterStatus !== 'all' && r.status !== filterStatus) return false;
      return true;
    })
    .sort((a, b) => (filterStatus === 'active' ? startsAt(a) - startsAt(b) : startsAt(b) - startsAt(a)));

  const run = async (id: string, action: () => Promise<Partial<Reservation>>, okMessage: string) => {
    setBusyId(id);
    try {
      onUpdate(id, await action());
      toast.success(okMessage);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Xatolik');
    } finally {
      setBusyId(null);
    }
  };

  const checkIn = (r: Reservation) =>
    run(r.id, async () => ({ status: (await adminApi.updateReservationStatus(r.id, 'completed')).status }), 'Mehmon keldi deb belgilandi');
  const noShow = (r: Reservation) => {
    if (!confirm(`${r.userFullName || 'Mehmon'} kelmagan deb belgilansinmi?${r.depositPaid ? ' To\'langan depozit restoranda qoladi.' : ''}`)) return;
    run(r.id, async () => ({ status: (await adminApi.updateReservationStatus(r.id, 'no_show')).status, isNoShow: true }), 'Kelmadi deb belgilandi');
  };
  const depositPaid = (r: Reservation) =>
    run(r.id, async () => ({ depositPaid: (await reservationsApi.markDepositPaid(r.id)).depositPaid }), "Depozit to'langan deb belgilandi");
  const depositRefunded = (r: Reservation) => {
    if (!confirm(`${money(r.depositAmount)} mijozga qaytarildimi?`)) return;
    run(r.id, async () => { await reservationsApi.markDepositRefunded(r.id); return { depositOutcome: 'refunded' }; }, 'Depozit qaytarildi deb belgilandi');
  };
  const assignTable = (r: Reservation, tableId: string) =>
    run(r.id, async () => {
      const res = await adminApi.assignTable(r.id, tableId || null);
      return { tableId: res.tableId, tableLabel: res.tableLabel };
    }, tableId ? 'Stol biriktirildi' : 'Stol olib tashlandi');

  const actions = (r: Reservation) => {
    const now = Date.now();
    const busy = busyId === r.id;
    if (busy) return <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />;
    if (isActive(r)) {
      const canCheckIn = now >= startsAt(r) - 60 * 60000;
      const canNoShow = now >= startsAt(r);
      return (
        <div className="flex gap-1 flex-wrap justify-end">
          {r.depositAmount > 0 && !r.depositPaid && (
            <Button size="sm" variant="ghost" onClick={() => depositPaid(r)} title="Depozit naqd/terminal orqali olindi">
              <BadgeDollarSign className="w-3.5 h-3.5 mr-1" /> To'landi
            </Button>
          )}
          <Button size="sm" onClick={() => checkIn(r)} disabled={!canCheckIn} title={canCheckIn ? undefined : "Bron vaqtidan 1 soat oldin ochiladi"}>
            <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Keldi
          </Button>
          <Button size="sm" variant="ghost" className="text-orange-500 hover:text-orange-500" onClick={() => noShow(r)}
            disabled={!canNoShow} title={canNoShow ? undefined : 'Bron vaqti kelgach ochiladi'}>
            <UserX className="w-3.5 h-3.5 mr-1" /> Kelmadi
          </Button>
          <Button size="sm" variant="outline" onClick={() => setCancelTarget(r)}>
            <XCircle className="w-3.5 h-3.5 mr-1" /> Bekor
          </Button>
        </div>
      );
    }
    if (r.depositOutcome === 'refund_requested' && canManageMoney) {
      return (
        <Button size="sm" variant="outline" onClick={() => depositRefunded(r)}>
          <Undo2 className="w-3.5 h-3.5 mr-1" /> Depozit qaytarildi
        </Button>
      );
    }
    return null;
  };

  const tableSelect = (r: Reservation) => {
    if (!isActive(r) || tables.length === 0) return <span className="text-muted-foreground">{r.tableLabel || '—'}</span>;
    const options = tables.filter(t => t.isActive && (t.capacity >= r.guestsCount || t.id === r.tableId));
    return (
      <select value={r.tableId ?? ''} onChange={e => assignTable(r, e.target.value)} aria-label="Stol"
        disabled={busyId === r.id}
        className="h-8 max-w-[9rem] rounded-md border border-border bg-secondary px-2 text-sm text-foreground">
        <option value="">— tanlanmagan</option>
        {options.map(t => <option key={t.id} value={t.id}>{t.label} ({t.capacity})</option>)}
      </select>
    );
  };

  const deposit = (r: Reservation) => {
    if (r.depositAmount <= 0) return <span className="text-muted-foreground">—</span>;
    const outcome = r.depositOutcome ? DEPOSIT_OUTCOME[r.depositOutcome] : null;
    return (
      <span className={cn('text-xs font-medium px-2 py-1 rounded-full whitespace-nowrap',
        outcome ? outcome.cls : r.depositPaid ? 'bg-green-500/20 text-green-500' : 'bg-yellow-500/20 text-yellow-600')}>
        {money(r.depositAmount)} {outcome ? `· ${outcome.label}` : r.depositPaid ? '✓' : '· to\'lanmagan'}
      </span>
    );
  };

  const notes = (r: Reservation) => (
    <>
      {r.specialRequests && <p className="text-xs text-muted-foreground italic">"{r.specialRequests}"</p>}
      {r.totalDiscount > 0 && <p className="text-xs text-primary">Chegirma {r.totalDiscount}%{r.preOrderTotal > 0 ? ` · oldindan buyurtma ${money(r.preOrderTotal)}` : ''}</p>}
      {r.totalDiscount === 0 && r.preOrderTotal > 0 && <p className="text-xs text-muted-foreground">Oldindan buyurtma {money(r.preOrderTotal)}</p>}
      {r.status === 'cancelled' && (
        <p className="text-xs text-muted-foreground">
          {r.cancelledBy === 'restaurant' ? 'Restoran bekor qildi' : r.cancelledBy === 'system' ? 'Tizim bekor qildi' : 'Mehmon bekor qildi'}
          {r.isLateCancellation ? ' (kech)' : ''}{r.cancelReason ? `: ${r.cancelReason}` : ''}
        </p>
      )}
    </>
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2 sm:gap-4 flex-wrap">
        <Input type="date" value={filterDate} min={today} onChange={e => onFilterDate(e.target.value)} className="w-44 bg-secondary" aria-label="Sana" />
        <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)} aria-label="Holat"
          className="h-10 px-3 rounded-md border border-input bg-secondary text-foreground text-sm">
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        {filterDate && <Button variant="outline" size="sm" onClick={() => onFilterDate('')}>Sanani tozalash</Button>}
        <span className="text-sm text-muted-foreground ml-auto">{filtered.length} ta bron</span>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground bg-card rounded-xl border border-border">
          <CalendarDays className="w-10 h-10 mx-auto mb-2 opacity-40" />
          <p>Bronlar topilmadi</p>
        </div>
      ) : (
        <>
          {/* Telefon: kartochkalar */}
          <div className="space-y-3 md:hidden">
            {filtered.map(r => (
              <div key={r.id} className="bg-card rounded-xl border border-border p-4 space-y-2">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium text-foreground">{r.userFullName || 'Mehmon'}</p>
                    <p className="text-sm text-muted-foreground">
                      {fmtDate(r.date)} · {r.time?.slice(0, 5)} · {r.guestsCount} kishi
                    </p>
                    {r.userPhone && <a href={`tel:${r.userPhone}`} className="text-sm text-primary">{r.userPhone}</a>}
                  </div>
                  <StatusBadge status={r.status} />
                </div>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="text-muted-foreground">Stol:</span> {tableSelect(r)}
                  {deposit(r)}
                </div>
                {notes(r)}
                <div className="pt-1">{actions(r)}</div>
              </div>
            ))}
          </div>

          {/* Kompyuter: jadval */}
          <div className="hidden md:block bg-card rounded-xl border border-border overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-muted-foreground">
                  {['Sana', 'Vaqt', 'Mehmon', 'Kishi', 'Stol', 'Depozit', 'Holat', ''].map(h => (
                    <th key={h} className={cn('p-3 font-medium whitespace-nowrap', h === '' && 'text-right')}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filtered.map(r => (
                  <tr key={r.id} className="border-b border-border last:border-0 align-top">
                    <td className="p-3 text-foreground whitespace-nowrap">{fmtDate(r.date)}</td>
                    <td className="p-3 text-foreground">{r.time?.slice(0, 5)}</td>
                    <td className="p-3 min-w-[12rem]">
                      <p className="text-foreground font-medium">{r.userFullName || '—'}</p>
                      {r.userPhone && <p className="text-xs text-muted-foreground">{r.userPhone}</p>}
                      {notes(r)}
                    </td>
                    <td className="p-3 text-foreground">{r.guestsCount}</td>
                    <td className="p-3">{tableSelect(r)}</td>
                    <td className="p-3">{deposit(r)}</td>
                    <td className="p-3"><StatusBadge status={r.status} /></td>
                    <td className="p-3 text-right">{actions(r)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {cancelTarget && (
        <CancelDialog
          reservation={cancelTarget}
          onClose={() => setCancelTarget(null)}
          onDone={(patch) => { onUpdate(cancelTarget.id, patch); setCancelTarget(null); onReload(); }}
        />
      )}
    </div>
  );
};

const REASONS = ["Zal yopiq (maxsus tadbir)", "Texnik nosozlik", "Mehmon iltimosiga ko'ra", "Joy qolmadi"];

const CancelDialog = ({ reservation: r, onClose, onDone }: {
  reservation: Reservation; onClose: () => void; onDone: (patch: Partial<Reservation>) => void;
}) => {
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const confirm = async () => {
    setSaving(true);
    try {
      const res = await adminApi.updateReservationStatus(r.id, 'cancelled', reason.trim());
      toast.success('Bron bekor qilindi — mehmonga xabar yuborildi');
      onDone({
        status: res.status, cancelledBy: 'restaurant', cancelReason: reason.trim() || null,
        depositOutcome: r.depositPaid && r.depositAmount > 0 ? 'refund_requested' : r.depositOutcome,
      });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Bekor qilib bo'lmadi");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={v => { if (!v) onClose(); }}>
      <DialogContent className="bg-card border-border">
        <DialogHeader>
          <DialogTitle className="font-display text-foreground">Bronni bekor qilish</DialogTitle>
          <DialogDescription>
            {r.userFullName || 'Mehmon'} — {fmtDate(r.date)} {r.time.slice(0, 5)}, {r.guestsCount} kishi
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {r.depositPaid && r.depositAmount > 0 && (
            <p className="text-sm rounded-lg bg-orange-500/10 border border-orange-500/30 p-3">
              Restoran bekor qilgani uchun {money(r.depositAmount)} depozit mehmonga to'liq qaytariladi.
            </p>
          )}
          <div className="space-y-2">
            <Label>Sabab (mehmonga ko'rsatiladi)</Label>
            <div className="flex flex-wrap gap-1.5">
              {REASONS.map(x => (
                <button key={x} type="button" onClick={() => setReason(x)}
                  className={cn('text-xs px-2.5 py-1 rounded-full border', reason === x ? 'border-primary bg-primary/10 text-primary' : 'border-border text-muted-foreground')}>
                  {x}
                </button>
              ))}
            </div>
            <Textarea value={reason} onChange={e => setReason(e.target.value)} rows={2} maxLength={500} className="bg-secondary" placeholder="Sababni yozing..." />
          </div>
          <div className="flex gap-3">
            <Button variant="outline" className="flex-1" onClick={onClose}>Ortga</Button>
            <Button variant="destructive" className="flex-1" disabled={saving} onClick={confirm}>
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Bekor qilish'}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
};
