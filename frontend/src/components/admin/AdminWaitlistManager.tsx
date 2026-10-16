import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { waitlistApi, ApiError, type WaitlistEntry } from '@/lib/api';
import { toast } from 'sonner';
import { Clock, Users, CheckCircle2, Bell, XCircle, Loader2 } from 'lucide-react';

interface Props {
  restaurantId: string;
}

const statusLabel: Record<WaitlistEntry['status'], { label: string; color: string }> = {
  waiting: { label: 'Kutmoqda', color: 'bg-yellow-500/20 text-yellow-400' },
  notified: { label: 'Xabar berildi', color: 'bg-blue-500/20 text-blue-400' },
  seated: { label: 'Joylashtirildi', color: 'bg-green-500/20 text-green-400' },
  cancelled: { label: 'Bekor', color: 'bg-red-500/20 text-red-400' },
};

/**
 * Admin waitlist view — list entries, notify/seat/cancel.
 */
export const AdminWaitlistManager = ({ restaurantId }: Props) => {
  const [list, setList] = useState<WaitlistEntry[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const items = await waitlistApi.forRestaurant(restaurantId);
      setList(items);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setLoading(false);
    }
  }, [restaurantId]);

  useEffect(() => {
    if (restaurantId) load();
  }, [restaurantId, load]);

  const setStatus = async (id: string, status: WaitlistEntry['status']) => {
    try {
      await waitlistApi.updateStatus(id, status);
      toast.success('Yangilandi');
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="w-6 h-6 animate-spin text-primary" />
      </div>
    );
  }

  const sorted = [...list].sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
  );

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-display text-lg font-bold text-foreground">Kutish ro'yxati</h3>
        <p className="text-sm text-muted-foreground">Jami: {list.length}</p>
      </div>

      {sorted.length === 0 ? (
        <div className="bg-card border border-border rounded-xl p-12 text-center">
          <Clock className="w-12 h-12 text-muted-foreground mx-auto mb-3 opacity-50" />
          <p className="text-muted-foreground">Hozircha kutayotgan mehmon yo'q</p>
        </div>
      ) : (
        <div className="space-y-2">
          {sorted.map(w => (
            <div
              key={w.id}
              className="bg-card border border-border rounded-xl p-4 flex items-center gap-4 flex-wrap"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="text-foreground font-medium">{w.userFullName ?? 'Mehmon'}</p>
                  <span
                    className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${statusLabel[w.status].color}`}
                  >
                    {statusLabel[w.status].label}
                  </span>
                </div>
                <p className="text-sm text-muted-foreground mt-0.5 flex items-center gap-3 flex-wrap">
                  <span>{w.date.split('-').reverse().join('.')}</span>
                  {w.time && <span>{w.time.slice(0, 5)}</span>}
                  <span className="flex items-center gap-1">
                    <Users className="w-3.5 h-3.5" /> {w.guestsCount}
                  </span>
                </p>
                {w.note && <p className="text-xs text-muted-foreground italic mt-1">"{w.note}"</p>}
              </div>
              {w.status === 'waiting' && (
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" onClick={() => setStatus(w.id, 'notified')}>
                    <Bell className="w-3.5 h-3.5 mr-1" /> Xabar
                  </Button>
                  <Button size="sm" onClick={() => setStatus(w.id, 'seated')}>
                    <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Joylash
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-destructive hover:text-destructive"
                    onClick={() => setStatus(w.id, 'cancelled')}
                  >
                    <XCircle className="w-3.5 h-3.5" />
                  </Button>
                </div>
              )}
              {w.status === 'notified' && (
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => setStatus(w.id, 'seated')}>
                    <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Joylash
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-destructive hover:text-destructive"
                    onClick={() => setStatus(w.id, 'cancelled')}
                  >
                    <XCircle className="w-3.5 h-3.5" />
                  </Button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
