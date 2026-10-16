import { useCallback, useEffect, useState } from 'react';
import { addDays, addMonths, addWeeks, addYears, format, parseISO } from 'date-fns';
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { adminApi, ApiError, type RestaurantReport } from '@/lib/api';
import { ChevronLeft, ChevronRight, Download, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

type Period = 'day' | 'week' | 'month' | 'year';

const PERIODS: { key: Period; label: string }[] = [
  { key: 'day', label: 'Kunlik' },
  { key: 'week', label: 'Haftalik' },
  { key: 'month', label: 'Oylik' },
  { key: 'year', label: 'Yillik' },
];

const money = (v: number) => `${Math.round(v).toLocaleString('ru-RU')} so'm`;

const shift = (date: string, period: Period, dir: 1 | -1) => {
  const d = parseISO(date);
  const next = period === 'day' ? addDays(d, dir) : period === 'week' ? addWeeks(d, dir) : period === 'month' ? addMonths(d, dir) : addYears(d, dir);
  return format(next, 'yyyy-MM-dd');
};

/** Restoran hisobotlari: davr filtri, ko'rsatkichlar, grafik, jadval va PDF. */
export const AdminReports = ({ restaurantId }: { restaurantId: string }) => {
  const [period, setPeriod] = useState<Period>('day');
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [report, setReport] = useState<RestaurantReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [downloading, setDownloading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setReport(await adminApi.report({ restaurantId, period, date }));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Hisobotni yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [restaurantId, period, date]);

  useEffect(() => { load(); }, [load]);

  const downloadPdf = async () => {
    setDownloading(true);
    try {
      await adminApi.downloadReportPdf({ restaurantId, period, date });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "PDF tayyorlab bo'lmadi");
    } finally {
      setDownloading(false);
    }
  };

  const x = report?.totals;
  const kpis = x
    ? [
        { label: 'Jami bronlar', value: x.reservations },
        { label: 'Mehmonlar', value: x.guests },
        { label: 'Yakunlangan', value: x.completed },
        { label: 'Faol (kelgusi)', value: x.active },
        { label: 'Bekor qilingan', value: `${x.cancelled} (${x.cancelRate}%)` },
        { label: 'Kelmagan', value: `${x.noShow} (${x.noShowRate}%)` },
        { label: "To'langan depozit", value: money(x.depositsPaid) },
        { label: 'Oldindan buyurtma', value: money(x.preOrderTotal) },
        { label: "O'rtacha mehmon", value: x.avgGuests },
        { label: 'Noyob mijozlar', value: `${x.uniqueGuests} (qaytgan: ${x.returningGuests})` },
        { label: 'Sharhlar', value: x.reviews ? `${x.reviews} · ${x.avgRating}/10` : '0' },
        { label: "Kutish ro'yxati", value: x.waitlistEntries },
      ]
    : [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h3 className="font-display text-2xl font-bold text-foreground">Hisobotlar</h3>
          <p className="text-sm text-muted-foreground">
            {report ? `${report.label} · ${report.restaurantName}` : '...'}
          </p>
        </div>
        <Button variant="hero" onClick={downloadPdf} disabled={downloading || loading}>
          {downloading ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : <Download className="w-4 h-4 mr-2" />}
          PDF yuklab olish
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="flex rounded-xl border border-border overflow-hidden">
          {PERIODS.map(p => (
            <button
              key={p.key}
              onClick={() => setPeriod(p.key)}
              className={cn('px-4 py-2 text-sm font-medium transition-colors',
                period === p.key ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:text-foreground')}
            >
              {p.label}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-1">
          <Button variant="outline" size="icon" onClick={() => setDate(d => shift(d, period, -1))} aria-label="oldingi"><ChevronLeft className="w-4 h-4" /></Button>
          <Input type="date" value={date} onChange={e => e.target.value && setDate(e.target.value)} className="w-40 bg-secondary" />
          <Button variant="outline" size="icon" onClick={() => setDate(d => shift(d, period, 1))} aria-label="keyingi"><ChevronRight className="w-4 h-4" /></Button>
        </div>
        <Button variant="ghost" size="sm" onClick={() => setDate(format(new Date(), 'yyyy-MM-dd'))}>Bugun</Button>
      </div>

      {loading && !report ? (
        <Loader2 className="w-6 h-6 animate-spin text-muted-foreground" />
      ) : report && (
        <div className={cn('space-y-6', loading && 'opacity-60')}>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {kpis.map(k => (
              <div key={k.label} className="bg-card rounded-xl border border-border p-4">
                <div className="font-display text-xl font-bold text-foreground">{k.value}</div>
                <div className="text-xs text-muted-foreground mt-1">{k.label}</div>
              </div>
            ))}
          </div>

          {x && x.depositsUnpaid > 0 && (
            <p className="text-sm text-destructive">To'lanmagan depozitlar (faol bronlar): {money(x.depositsUnpaid)}</p>
          )}

          <div className="bg-card rounded-xl border border-border p-4">
            <h4 className="font-medium text-foreground mb-3">
              {period === 'day' ? 'Soatlar bo\'yicha' : period === 'year' ? 'Oylar bo\'yicha' : 'Kunlar bo\'yicha'}
            </h4>
            {report.breakdown.length === 0 ? (
              <p className="text-sm text-muted-foreground py-8 text-center">Bu davrda bronlar yo'q.</p>
            ) : (
              <div className="h-64">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={report.breakdown}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                    <XAxis dataKey="label" tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }} />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }} />
                    <Tooltip contentStyle={{ background: 'hsl(var(--card))', border: '1px solid hsl(var(--border))', borderRadius: 8 }} />
                    <Legend />
                    <Bar dataKey="reservations" name="Bronlar" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} />
                    <Bar dataKey="guests" name="Mehmonlar" fill="hsl(var(--muted-foreground))" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </div>

          <div className="bg-card rounded-xl border border-border overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-muted-foreground border-b border-border">
                  {['Davr', 'Bronlar', 'Mehmonlar', 'Yakunlangan', 'Bekor', 'Kelmagan'].map(h => (
                    <th key={h} className="p-3 font-medium">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {report.breakdown.map(r => (
                  <tr key={r.label} className="border-b border-border/50">
                    <td className="p-3 text-foreground">{r.label}</td>
                    <td className="p-3">{r.reservations}</td>
                    <td className="p-3">{r.guests}</td>
                    <td className="p-3">{r.completed}</td>
                    <td className="p-3">{r.cancelled}</td>
                    <td className="p-3">{r.noShow}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="grid md:grid-cols-2 gap-4">
            <div className="bg-card rounded-xl border border-border p-4">
              <h4 className="font-medium text-foreground mb-2">Eng gavjum soatlar</h4>
              {report.topHours.length === 0 ? <p className="text-sm text-muted-foreground">—</p> : (
                <ul className="space-y-1 text-sm">
                  {report.topHours.map(h => <li key={h.label}>{h.label} — {h.reservations} ta bron, {h.guests} mehmon</li>)}
                </ul>
              )}
            </div>
            <div className="bg-card rounded-xl border border-border p-4">
              <h4 className="font-medium text-foreground mb-2">Ko'p buyurtma qilingan taomlar</h4>
              {report.topMenuItems.length === 0 ? <p className="text-sm text-muted-foreground">Oldindan buyurtmalar yo'q.</p> : (
                <ul className="space-y-1 text-sm">
                  {report.topMenuItems.map(m => <li key={m.name}>{m.name} — {m.quantity} ta ({money(m.revenue)})</li>)}
                </ul>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
