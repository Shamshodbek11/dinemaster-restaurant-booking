import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { reviewsApi, superadminApi, ApiError, type SuperadminPayment, type SuperadminReview } from '@/lib/api';
import { Pager, PanelHeader, Select, Table } from './common';
import { PAGE_SIZE, dmy, dateTime, money, useDebounced } from './utils';
import { Loader2, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

/** Barcha sharhlar: qidiruv, past reytinglilar filtri, o'chirish (moderatsiya). */
export const ReviewsPanel = () => {
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const [maxRating, setMaxRating] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: SuperadminReview[] }>({ total: 0, items: [] });
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.reviews({ search: debounced, maxRating: maxRating ? Number(maxRating) : undefined, page, size: PAGE_SIZE });
      setData({ total: res.total, items: res.items });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [debounced, maxRating, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [debounced, maxRating]);

  const remove = async (r: SuperadminReview) => {
    if (!confirm(`"${r.restaurantName}" uchun sharhni o'chirasizmi?`)) return;
    try {
      await reviewsApi.delete(r.id);
      toast.success("Sharh o'chirildi");
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  return (
    <div>
      <PanelHeader title="Sharhlar" subtitle="Nomaqbul yoki soxta sharhlarni o'chirish" />
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input value={search} onChange={e => setSearch(e.target.value)} placeholder="Matn, mijoz yoki restoran..." className="pl-9 bg-secondary" />
        </div>
        <Select label="Reyting" value={maxRating} onChange={setMaxRating}
          options={[{ value: '', label: 'Barcha reytinglar' }, { value: '4', label: '4 va past' }, { value: '6', label: '6 va past' }]} />
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>
      <Table head={['Sana', 'Restoran', 'Mijoz', 'Reyting', 'Sharh', '']} empty={!loading && data.items.length === 0}>
        {data.items.map(r => (
          <tr key={r.id} className="border-b border-border/50 align-top">
            <td className="p-3 whitespace-nowrap text-xs">{dateTime(r.createdAt)}</td>
            <td className="p-3">{r.restaurantName}</td>
            <td className="p-3">{r.userFullName}</td>
            <td className="p-3 font-medium">{r.rating}/10</td>
            <td className="p-3 max-w-md">
              <div>{r.comment || <span className="text-muted-foreground">—</span>}</div>
              {r.ownerResponse && <div className="text-xs text-muted-foreground mt-1">Javob: {r.ownerResponse}</div>}
            </td>
            <td className="p-3">
              <Button size="sm" variant="ghost" className="text-destructive" onClick={() => remove(r)} title="O'chirish"><Trash2 className="w-4 h-4" /></Button>
            </td>
          </tr>
        ))}
      </Table>
      <Pager page={page} total={data.total} onPage={setPage} />
    </div>
  );
};

const PAYMENT_STATES: Record<number, string> = { 1: 'Kutilmoqda', 2: "To'langan", [-1]: 'Bekor', [-2]: 'Qaytarilgan' };

/** Depozit to'lovlari (Payme, Click, test). */
export const PaymentsPanel = () => {
  const [provider, setProvider] = useState('');
  const [state, setState] = useState('');
  const [refundPending, setRefundPending] = useState(false);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: SuperadminPayment[]; totals: { paid: number; refunded: number; refundsPending: number } }>(
    { total: 0, items: [], totals: { paid: 0, refunded: 0, refundsPending: 0 } });
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.payments({
        provider, state: state ? Number(state) : undefined, refundPending: refundPending || undefined, page, size: PAGE_SIZE,
      });
      setData({ total: res.total, items: res.items, totals: { refundsPending: 0, ...res.totals } });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [provider, state, refundPending, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [provider, state, refundPending]);

  const markRefunded = async (p: SuperadminPayment) => {
    if (!confirm(`${money(p.amount)} (${p.provider}) mijozga qaytarildimi? Avval ${p.provider === 'click' ? 'Click' : 'Payme'} kabinetida qaytarishni bajaring.`)) return;
    try {
      await superadminApi.markPaymentRefunded(p.id);
      toast.success("Qaytarildi deb belgilandi — mijozga xabar yuborildi");
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  return (
    <div>
      <PanelHeader title="To'lovlar" subtitle="Bron depozitlari">
        <div className="flex gap-4 text-sm">
          <span>To'langan: <b className="text-success">{money(data.totals.paid)}</b></span>
          <span>Qaytarilgan: <b className="text-destructive">{money(data.totals.refunded)}</b></span>
        </div>
      </PanelHeader>
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <Select label="Provayder" value={provider} onChange={setProvider}
          options={[{ value: '', label: 'Barcha provayderlar' }, { value: 'payme', label: 'Payme' }, { value: 'click', label: 'Click' }, { value: 'test', label: 'Test' }]} />
        <Select label="Holat" value={state} onChange={setState}
          options={[{ value: '', label: 'Barcha holatlar' }, ...Object.entries(PAYMENT_STATES).map(([v, l]) => ({ value: v, label: l }))]} />
        <Button size="sm" variant={refundPending ? 'default' : 'outline'} onClick={() => setRefundPending(v => !v)}>
          Qaytarish kutilmoqda{data.totals.refundsPending > 0 ? ` (${data.totals.refundsPending})` : ''}
        </Button>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>
      {data.totals.refundsPending > 0 && (
        <p className="text-sm mb-4 rounded-lg border border-orange-500/30 bg-orange-500/10 p-3">
          {data.totals.refundsPending} ta to'lov bo'yicha mijoz bronni o'z vaqtida bekor qilgan yoki restoran bekor qilgan — pul qaytarilishi kerak.
          Click/Payme kabinetida qaytaring, so'ng shu yerda "Qaytarildi" tugmasini bosing.
        </p>
      )}
      <Table head={['№', 'Sana', 'Mijoz', 'Restoran', 'Summa', 'Provayder', 'Holat', '']} empty={!loading && data.items.length === 0}>
        {data.items.map(p => (
          <tr key={p.id} className="border-b border-border/50">
            <td className="p-3 text-xs text-muted-foreground">{p.number}</td>
            <td className="p-3 text-xs whitespace-nowrap">{dateTime(p.createdAt)}</td>
            <td className="p-3">{p.userFullName}</td>
            <td className="p-3">{p.restaurantName} <span className="text-xs text-muted-foreground">{dmy(p.reservationDate)}</span></td>
            <td className="p-3 font-medium">{money(p.amount)}</td>
            <td className="p-3 capitalize">{p.provider}</td>
            <td className="p-3">
              {PAYMENT_STATES[p.state] ?? p.state}
              {p.refundRequestedAt && p.state === 2 && <div className="text-xs text-orange-500">qaytarish so'ralgan</div>}
            </td>
            <td className="p-3">
              {p.refundRequestedAt && p.state === 2 && (
                <Button size="sm" variant="outline" onClick={() => markRefunded(p)}>Qaytarildi</Button>
              )}
            </td>
          </tr>
        ))}
      </Table>
      <Pager page={page} total={data.total} onPage={setPage} />
    </div>
  );
};
