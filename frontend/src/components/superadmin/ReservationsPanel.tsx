import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { adminApi, superadminApi, ApiError, type Restaurant, type SuperadminReservation } from '@/lib/api';
import { Pager, PanelHeader, Select, StatusBadge, Table } from './common';
import { PAGE_SIZE, dmy, money, useDebounced } from './utils';
import { Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';

const STATUSES = [
  { value: '', label: 'Barcha holatlar' }, { value: 'confirmed', label: 'Tasdiqlangan' },
  { value: 'completed', label: 'Yakunlangan' }, { value: 'cancelled', label: 'Bekor qilingan' },
  { value: 'no_show', label: 'Kelmadi' }, { value: 'pending', label: 'Kutilmoqda' },
];

/** Platformadagi barcha bronlar: filtr va status boshqaruvi. */
export const ReservationsPanel = ({ restaurants }: { restaurants: Restaurant[] }) => {
  const [status, setStatus] = useState('');
  const [restaurantId, setRestaurantId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: SuperadminReservation[] }>({ total: 0, items: [] });
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.reservations({ status, restaurantId, from, to, search: debounced, page, size: PAGE_SIZE });
      setData({ total: res.total, items: res.items });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [status, restaurantId, from, to, debounced, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [status, restaurantId, from, to, debounced]);

  const setResStatus = async (id: string, s: string) => {
    try {
      await adminApi.updateReservationStatus(id, s);
      toast.success('Holat yangilandi');
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  return (
    <div>
      <PanelHeader title="Bronlar" subtitle="Barcha restoranlardagi bronlar" />
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <Select label="Holat" value={status} onChange={setStatus} options={STATUSES} />
        <Select label="Restoran" value={restaurantId} onChange={setRestaurantId}
          options={[{ value: '', label: 'Barcha restoranlar' }, ...restaurants.map(r => ({ value: r.id, label: r.name }))]} />
        <Input type="date" value={from} onChange={e => setFrom(e.target.value)} className="w-40 bg-secondary" aria-label="dan" />
        <Input type="date" value={to} onChange={e => setTo(e.target.value)} className="w-40 bg-secondary" aria-label="gacha" />
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input value={search} onChange={e => setSearch(e.target.value)} placeholder="Mijoz, email yoki restoran..." className="pl-9 bg-secondary" />
        </div>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>

      <Table head={['Sana', 'Mijoz', 'Restoran', 'Mehmon', 'Depozit', 'Holat', 'Amallar']} empty={!loading && data.items.length === 0}>
        {data.items.map(r => (
          <tr key={r.id} className="border-b border-border/50">
            <td className="p-3 whitespace-nowrap">{dmy(r.date)} <span className="text-muted-foreground">{r.time?.slice(0, 5)}</span></td>
            <td className="p-3"><div>{r.userFullName}</div><div className="text-xs text-muted-foreground">{r.userEmail}</div></td>
            <td className="p-3">{r.restaurantName}{r.tableLabel && <span className="text-xs text-muted-foreground"> · {r.tableLabel}</span>}</td>
            <td className="p-3">{r.guestsCount}</td>
            <td className="p-3 text-xs">{r.depositAmount > 0 ? <>{money(r.depositAmount)} {r.depositPaid ? '✓' : <span className="text-destructive">to'lanmagan</span>}</> : '—'}</td>
            <td className="p-3"><StatusBadge status={r.status} /></td>
            <td className="p-3">
              <div className="flex flex-wrap gap-1">
                {(r.status === 'confirmed' || r.status === 'pending') && (
                  <>
                    <Button size="sm" variant="outline" onClick={() => setResStatus(r.id, 'completed')}>Keldi</Button>
                    <Button size="sm" variant="outline" onClick={() => setResStatus(r.id, 'no_show')}>Kelmadi</Button>
                    <Button size="sm" variant="ghost" className="text-destructive" onClick={() => setResStatus(r.id, 'cancelled')}>Bekor</Button>
                  </>
                )}
                {r.status === 'completed' && <Button size="sm" variant="ghost" onClick={() => setResStatus(r.id, 'no_show')}>Kelmadi deb tuzatish</Button>}
              </div>
            </td>
          </tr>
        ))}
      </Table>
      <Pager page={page} total={data.total} onPage={setPage} />
    </div>
  );
};
