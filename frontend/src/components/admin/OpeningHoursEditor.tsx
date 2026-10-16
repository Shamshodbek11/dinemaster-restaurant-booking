import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

const DAYS = [
  { key: 'mon', label: 'Dushanba' },
  { key: 'tue', label: 'Seshanba' },
  { key: 'wed', label: 'Chorshanba' },
  { key: 'thu', label: 'Payshanba' },
  { key: 'fri', label: 'Juma' },
  { key: 'sat', label: 'Shanba' },
  { key: 'sun', label: 'Yakshanba' },
] as const;

type DayKey = (typeof DAYS)[number]['key'];
type Day = { open: string; close: string; closed: boolean };

const DEFAULT_DAY: Day = { open: '10:00', close: '23:00', closed: false };

/** {"mon":"10:00-23:00","sun":"closed"} → kunlar bo'yicha qiymatlar */
const parse = (raw: string): Record<DayKey, Day> => {
  let obj: Record<string, unknown> = {};
  try {
    const parsed = JSON.parse(raw || '{}');
    if (parsed && typeof parsed === 'object') obj = parsed as Record<string, unknown>;
  } catch {
    /* noto'g'ri JSON — standart qiymatlar */
  }
  const hasAny = DAYS.some(d => d.key in obj);
  const result = {} as Record<DayKey, Day>;
  for (const d of DAYS) {
    const v = obj[d.key];
    if (!hasAny) result[d.key] = { ...DEFAULT_DAY };
    else if (typeof v === 'string' && /^\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}$/.test(v.trim())) {
      const [open, close] = v.split('-').map(s => s.trim().padStart(5, '0'));
      result[d.key] = { open, close: close === '24:00' ? '00:00' : close, closed: false };
    } else {
      result[d.key] = { ...DEFAULT_DAY, closed: true };
    }
  }
  return result;
};

const serialize = (days: Record<DayKey, Day>) =>
  JSON.stringify(
    Object.fromEntries(DAYS.map(d => [d.key, days[d.key].closed ? 'closed' : `${days[d.key].open}-${days[d.key].close}`])),
  );

interface Props {
  value: string;
  onChange: (json: string) => void;
}

/**
 * Restoran ish vaqti. Yopilish ochilishdan oldin bo'lsa (masalan 18:00–02:00) — ertasi kuni yopiladi.
 * Bron yopilishdan kamida 1 soat oldin boshlanishi kerak (backend tekshiradi).
 */
export const OpeningHoursEditor = ({ value, onChange }: Props) => {
  const days = parse(value);
  const update = (key: DayKey, patch: Partial<Day>) => onChange(serialize({ ...days, [key]: { ...days[key], ...patch } }));

  return (
    <div className="space-y-2">
      <Label>Ish vaqti</Label>
      <div className="rounded-xl border border-border divide-y divide-border">
        {DAYS.map(d => {
          const day = days[d.key];
          return (
            <div key={d.key} className="grid grid-cols-[1fr_auto] sm:flex sm:flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2">
              <span className="sm:w-28 text-sm font-medium text-foreground">{d.label}</span>
              <label className="sm:order-last flex items-center gap-2 text-sm text-muted-foreground sm:ml-auto justify-self-end">
                <input
                  type="checkbox"
                  checked={day.closed}
                  onChange={e => update(d.key, { closed: e.target.checked })}
                  className="w-4 h-4"
                />
                Dam olish
              </label>
              <div className="col-span-2 flex items-center gap-2">
              <Input
                type="time"
                value={day.open}
                disabled={day.closed}
                onChange={e => update(d.key, { open: e.target.value })}
                className="bg-secondary w-full sm:w-32"
                aria-label={`${d.label} ochilish`}
              />
              <span className="text-muted-foreground">–</span>
              <Input
                type="time"
                value={day.close}
                disabled={day.closed}
                onChange={e => update(d.key, { close: e.target.value })}
                className="bg-secondary w-full sm:w-32"
                aria-label={`${d.label} yopilish`}
              />
              </div>
            </div>
          );
        })}
      </div>
      <p className="text-xs text-muted-foreground">
        Yopilish vaqti ochilishdan oldin bo'lsa (masalan 18:00–02:00), restoran ertasi kuni yopiladi. Oxirgi bron yopilishdan 1 soat oldin.
      </p>
    </div>
  );
};
