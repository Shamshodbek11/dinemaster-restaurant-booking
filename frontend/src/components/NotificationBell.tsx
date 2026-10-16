import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { formatDistanceToNow } from 'date-fns';
import { enUS, ru, uz } from 'date-fns/locale';
import { Bell, CheckCheck, Loader2 } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { notificationsApi, type NotificationItem } from '@/lib/api';
import { cn } from '@/lib/utils';

const POLL_MS = 30_000;
const LOCALES = { uz, ru, en: enUS } as const;

/** Brauzer bildirishnomalari yoqilganmi (Sozlamalar → "Brauzer notifikatsiyalari") */
const browserNotificationsEnabled = () => {
  try {
    return localStorage.getItem('dm-notif-browser') === 'true';
  } catch {
    return false;
  }
};

interface Props {
  /** Qorong'i yon panel uchun (admin / superadmin) */
  variant?: 'default' | 'sidebar';
}

/**
 * Bildirishnomalar qo'ng'iroqchasi: o'qilmaganlar soni (30 soniyada yangilanadi), ro'yxat,
 * bosilganda o'qilgan deb belgilab, havolaga o'tadi. Yangi xabar kelsa va ruxsat bo'lsa —
 * brauzer bildirishnomasi ham ko'rsatiladi.
 */
export const NotificationBell = ({ variant = 'default' }: Props) => {
  const { user, language, refreshUser } = useApp();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [count, setCount] = useState(0);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(false);
  const lastCount = useRef<number | null>(null);

  const loadList = useCallback(async () => {
    setLoading(true);
    try {
      setItems((await notificationsApi.mine()).slice(0, 30));
    } catch {
      /* tarmoq xatosi — keyingi safar */
    } finally {
      setLoading(false);
    }
  }, []);

  const poll = useCallback(async () => {
    try {
      const { count: c } = await notificationsApi.unreadCount();
      setCount(c);
      const prev = lastCount.current;
      lastCount.current = c;
      if (prev !== null && c > prev) {
        // Yangi xabar: ro'yxatni yangilaymiz va (ruxsat bo'lsa) brauzerda ko'rsatamiz
        const list = (await notificationsApi.mine()).slice(0, 30);
        setItems(list);
        const fresh = list.filter(n => !n.isRead).slice(0, c - prev);
        // Rol o'zgargan bo'lishi mumkin (masalan, hamkorlik arizasi tasdiqlandi)
        if (fresh.some(n => n.type === 'partner_approved')) refreshUser();
        if (browserNotificationsEnabled() && 'Notification' in window && Notification.permission === 'granted') {
          for (const n of fresh) new Notification(n.title, { body: n.body, tag: n.id });
        }
      }
    } catch {
      /* jim */
    }
  }, [refreshUser]);

  useEffect(() => {
    if (!user) return;
    lastCount.current = null;
    poll();
    const id = setInterval(poll, POLL_MS);
    return () => clearInterval(id);
  }, [user, poll]);

  useEffect(() => {
    if (open) loadList();
  }, [open, loadList]);

  if (!user) return null;

  const openItem = async (n: NotificationItem) => {
    if (!n.isRead) {
      setItems(prev => prev.map(x => (x.id === n.id ? { ...x, isRead: true } : x)));
      setCount(c => Math.max(0, c - 1));
      lastCount.current = Math.max(0, (lastCount.current ?? 1) - 1);
      notificationsApi.markRead(n.id).catch(() => {});
    }
    if (n.type === 'partner_approved') await refreshUser();
    setOpen(false);
    if (n.link) navigate(n.link);
  };

  const readAll = async () => {
    await notificationsApi.readAll().catch(() => {});
    setItems(prev => prev.map(x => ({ ...x, isRead: true })));
    setCount(0);
    lastCount.current = 0;
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className={cn('relative', variant === 'sidebar' && 'text-white/70 hover:text-white hover:bg-white/10')}
          aria-label={t('notif.title', language)}
        >
          <Bell className="w-4 h-4" />
          {count > 0 && (
            <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-primary text-primary-foreground text-[10px] font-bold flex items-center justify-center">
              {count > 99 ? '99+' : count}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[360px] max-w-[calc(100vw-24px)] p-0 bg-card border-border">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border">
          <span className="font-display font-bold text-foreground">{t('notif.title', language)}</span>
          {count > 0 && (
            <button onClick={readAll} className="text-xs text-primary hover:underline flex items-center gap-1">
              <CheckCheck className="w-3.5 h-3.5" /> {t('notif.readAll', language)}
            </button>
          )}
        </div>
        <div className="max-h-[420px] overflow-y-auto">
          {loading && items.length === 0 && (
            <div className="py-8 flex justify-center"><Loader2 className="w-5 h-5 animate-spin text-muted-foreground" /></div>
          )}
          {!loading && items.length === 0 && (
            <p className="py-10 text-center text-sm text-muted-foreground">{t('notif.empty', language)}</p>
          )}
          {items.map(n => (
            <button
              key={n.id}
              onClick={() => openItem(n)}
              className={cn(
                'w-full text-left px-4 py-3 border-b border-border/60 hover:bg-secondary transition-colors flex gap-3',
                !n.isRead && 'bg-primary/5',
              )}
            >
              <span className={cn('mt-1.5 w-2 h-2 rounded-full shrink-0', n.isRead ? 'bg-transparent' : 'bg-primary')} />
              <span className="min-w-0">
                <span className="block text-sm font-medium text-foreground">{n.title}</span>
                {n.body && <span className="block text-xs text-muted-foreground mt-0.5 line-clamp-2">{n.body}</span>}
                <span className="block text-[11px] text-muted-foreground/70 mt-1">
                  {formatDistanceToNow(new Date(n.createdAt), { addSuffix: true, locale: LOCALES[language] })}
                </span>
              </span>
            </button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  );
};
