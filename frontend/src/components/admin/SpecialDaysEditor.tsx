import { useMemo, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { CalendarOff, Plus, Trash2 } from 'lucide-react';
import { localDateTimeString } from '@/lib/utils';

type Entry = { date: string; closed: boolean; open: string; close: string };

/** {"2026-12-31":"closed","2027-01-01":"12:00-20:00"} → ro'yxat (sana bo'yicha) */
const parse = (raw: string | null | undefined): Entry[] => {
  try {
    const obj = JSON.parse(raw || '{}') as Record<string, unknown>;
    return Object.entries(obj)
      .filter(([k]) => /^\d{4}-\d{2}-\d{2}$/.test(k))
      .map(([date, v]) => {
        const s = typeof v === 'string' ? v.trim() : '';
        const m = s.match(/^(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$/);
        return m
          ? { date, closed: false, open: m[1].padStart(5, '0'), close: m[2].padStart(5, '0') }
          : { date, closed: true, open: '10:00', close: '18:00' };
      })
      .sort((a, b) => a.date.localeCompare(b.date));
  } catch {
    return [];
  }
};

const serialize = (list: Entry[]) =>
  JSON.stringify(Object.fromEntries(list.filter(e => e.date).map(e => [e.date, e.closed ? 'closed' : `${e.open}-${e.close}`])));

interface Props {
  value: string | null | undefined;
  onChange: (json: string) => void;
}

/** Bayram, sanitariya kuni, xususiy tadbir: shu kunlarda oddiy ish vaqti o'rniga ishlatiladi. */
export const SpecialDaysEditor = ({ value, onChange }: Props) => {
  const entries = useMemo(() => parse(value), [value]);
  const today = localDateTimeString().slice(0, 10);
  const [newDate, setNewDate] = useState('');

  const update = (list: Entry[]) => onChange(serialize(list));
  const set = (date: string, patch: Partial<Entry>) => update(entries.map(e => (e.date === date ? { ...e, ...patch } : e)));
  const past = entries.filter(e => e.date < today);

  const add = () => {
    if (!newDate || entries.some(e => e.date === newDate)) return;
    update([...entries, { date: newDate, closed: true, open: '10:00', close: '18:00' }]);
    setNewDate('');
  };

  return (
    <div className="space-y-3 rounded-xl border border-border p-4">
      <div className="flex items-center gap-2">
        <CalendarOff className="w-4 h-4 text-primary" />
        <Label className="text-foreground">Maxsus kunlar (bayramlar, yopiq kunlar)</Label>
      </div>
      <p className="text-xs text-muted-foreground">
        Shu sanalarda oddiy ish vaqti o'rniga quyidagisi qo'llanadi. Yopiq kunga bron qabul qilinmaydi va qidiruvda restoran chiqmaydi.
      </p>

      {entries.filter(e => e.date >= today).map(e => (
        <div key={e.date} className="flex flex-wrap items-center gap-2 bg-secondary/60 rounded-lg p-2">
          <span className="font-medium text-sm text-foreground w-24">{e.date.split('-').reverse().join('.')}</span>
          <label className="flex items-center gap-1.5 text-sm text-foreground">
            <input type="checkbox" checked={e.closed} onChange={ev => set(e.date, { closed: ev.target.checked })} className="w-4 h-4" />
            Yopiq
          </label>
          {!e.closed && (
            <div className="flex items-center gap-1">
              <Input type="time" value={e.open} onChange={ev => set(e.date, { open: ev.target.value })} className="h-8 w-28 bg-background" aria-label="Ochilish" />
              <span className="text-muted-foreground">–</span>
              <Input type="time" value={e.close} onChange={ev => set(e.date, { close: ev.target.value })} className="h-8 w-28 bg-background" aria-label="Yopilish" />
            </div>
          )}
          <Button type="button" variant="ghost" size="icon" className="ml-auto h-8 w-8" aria-label="O'chirish"
            onClick={() => update(entries.filter(x => x.date !== e.date))}>
            <Trash2 className="w-4 h-4 text-destructive" />
          </Button>
        </div>
      ))}

      <div className="flex flex-wrap items-center gap-2">
        <Input type="date" min={today} value={newDate} onChange={e => setNewDate(e.target.value)} className="w-44 bg-secondary" aria-label="Sana" />
        <Button type="button" variant="outline" size="sm" onClick={add} disabled={!newDate || entries.some(e => e.date === newDate)}>
          <Plus className="w-4 h-4 mr-1" /> Kun qo'shish
        </Button>
        {past.length > 0 && (
          <Button type="button" variant="ghost" size="sm" className="text-muted-foreground"
            onClick={() => update(entries.filter(e => e.date >= today))}>
            O'tgan kunlarni tozalash ({past.length})
          </Button>
        )}
      </div>
    </div>
  );
};
