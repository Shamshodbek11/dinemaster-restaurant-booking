import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { reservationsApi, ApiError } from '@/lib/api';
import { uz } from 'date-fns/locale';
import { toast } from 'sonner';
import {
  ChevronLeft,
  ChevronRight,
  Loader2,
  CalendarRange,
  Users as UsersIcon,
} from 'lucide-react';
import {
  addMonths,
  endOfMonth,
  endOfWeek,
  format,
  isSameMonth,
  isToday,
  startOfMonth,
  startOfWeek,
} from 'date-fns';

interface Props {
  restaurantId: string;
  onSelectDate: (date: string) => void;
}

interface DayStat {
  date: string;
  count: number;
  guests: number;
}

/**
 * Monthly calendar of reservations for a single restaurant.
 * Clicking a day jumps the parent to the reservations tab filtered by that date.
 */
export const AdminMonthlyCalendar = ({ restaurantId, onSelectDate }: Props) => {
  const [cursor, setCursor] = useState(new Date());
  const [data, setData] = useState<DayStat[]>([]);
  const [loading, setLoading] = useState(true);

  const monthKey = format(cursor, 'yyyy-MM');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const stats = await reservationsApi.calendar(restaurantId, monthKey);
      setData(stats);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
      setData([]);
    } finally {
      setLoading(false);
    }
  }, [restaurantId, monthKey]);

  useEffect(() => {
    if (restaurantId) load();
  }, [restaurantId, monthKey, load]);

  const cells = useMemo(() => {
    const monthStart = startOfMonth(cursor);
    const monthEnd = endOfMonth(cursor);
    const gridStart = startOfWeek(monthStart, { weekStartsOn: 1 });
    const gridEnd = endOfWeek(monthEnd, { weekStartsOn: 1 });

    const map = new Map<string, DayStat>();
    data.forEach(d => map.set(d.date, d));

    const days: {
      date: Date;
      dateStr: string;
      inMonth: boolean;
      count: number;
      guests: number;
    }[] = [];
    let day = gridStart;
    while (day <= gridEnd) {
      const dateStr = format(day, 'yyyy-MM-dd');
      const stat = map.get(dateStr);
      days.push({
        date: day,
        dateStr,
        inMonth: isSameMonth(day, cursor),
        count: stat?.count ?? 0,
        guests: stat?.guests ?? 0,
      });
      day = new Date(day.getTime() + 24 * 3600 * 1000);
    }
    return days;
  }, [data, cursor]);

  const weekdays = ['Du', 'Se', 'Ch', 'Pa', 'Ju', 'Sh', 'Ya'];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <CalendarRange className="w-5 h-5 text-primary" />
          <h3 className="font-display text-lg font-bold text-foreground">
            {format(cursor, 'LLLL yyyy', { locale: uz })}
          </h3>
        </div>
        <div className="flex gap-2">
          <Button size="icon" variant="outline" onClick={() => setCursor(addMonths(cursor, -1))}>
            <ChevronLeft className="w-4 h-4" />
          </Button>
          <Button size="sm" variant="outline" onClick={() => setCursor(new Date())}>
            Bugun
          </Button>
          <Button size="icon" variant="outline" onClick={() => setCursor(addMonths(cursor, 1))}>
            <ChevronRight className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="w-6 h-6 animate-spin text-primary" />
        </div>
      ) : (
        <div className="bg-card border border-border rounded-xl p-4">
          <div className="grid grid-cols-7 gap-1 mb-2">
            {weekdays.map(w => (
              <div key={w} className="text-center text-xs font-medium text-muted-foreground py-2">
                {w}
              </div>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-1">
            {cells.map(c => (
              <button
                key={c.dateStr}
                onClick={() => onSelectDate(c.dateStr)}
                className={`aspect-square rounded-lg p-2 text-left transition-all flex flex-col justify-between relative ${
                  c.inMonth
                    ? 'bg-secondary/40 hover:bg-secondary/60 text-foreground'
                    : 'bg-secondary/20 text-muted-foreground'
                } ${isToday(c.date) ? 'ring-2 ring-primary' : ''}`}
              >
                <span className="text-xs font-medium">{format(c.date, 'd')}</span>
                {c.count > 0 && (
                  <div className="flex flex-col gap-0.5 text-[10px]">
                    <span className="font-bold text-primary">{c.count} bron</span>
                    <span className="flex items-center gap-0.5 text-muted-foreground">
                      <UsersIcon className="w-2.5 h-2.5" /> {c.guests}
                    </span>
                  </div>
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
