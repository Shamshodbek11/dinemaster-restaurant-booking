import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { superadminApi, ApiError, type AdminUser, type AdminUserDetail } from '@/lib/api';
import { Pager, PanelHeader, StatusBadge, Table } from './common';
import { PAGE_SIZE, dmy, dateTime, money, useDebounced } from './utils';
import { Ban, Eye, Loader2, MailCheck, Search, Shield, ShieldOff } from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

const ROLE_FILTERS = [
  { value: '', label: 'Hammasi' },
  { value: 'user', label: 'Foydalanuvchilar' },
  { value: 'admin', label: 'Restoran egalari' },
  { value: 'staff', label: 'Xodimlar' },
  { value: 'superadmin', label: 'Superadminlar' },
  { value: 'banned', label: 'Bloklanganlar' },
];

const topRole = (roles: string[]) =>
  roles.includes('superadmin') ? 'superadmin' : roles.includes('admin') ? 'admin' : roles.includes('staff') ? 'staff' : 'user';

const ROLE_NAMES: Record<string, string> = {
  superadmin: 'Superadmin', admin: 'Restoran egasi', staff: 'Xodim', user: 'Mehmon',
};

const ROLE_BADGE: Record<string, string> = {
  superadmin: 'bg-purple-500/15 text-purple-400',
  admin: 'bg-primary/15 text-primary',
  staff: 'bg-sky-500/15 text-sky-500',
  user: 'bg-muted text-muted-foreground',
};

export const UsersPanel = () => {
  const [role, setRole] = useState('');
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: AdminUser[] }>({ total: 0, items: [] });
  const [loading, setLoading] = useState(true);
  const [yums, setYums] = useState<Record<string, string>>({});
  const [detail, setDetail] = useState<AdminUserDetail | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.users({ role, search: debounced, page, size: PAGE_SIZE });
      setData({ total: res.total, items: res.items });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [role, debounced, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [role, debounced]);

  const replace = (u: AdminUser) => setData(d => ({ ...d, items: d.items.map(x => (x.id === u.id ? { ...x, ...u } : x)) }));

  const run = async (fn: () => Promise<AdminUser | unknown>, ok: string) => {
    try {
      const res = await fn();
      if (res && typeof res === 'object' && 'id' in res && 'roles' in res) replace(res as AdminUser);
      toast.success(ok);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  const adjustYums = (u: AdminUser) => {
    const amount = Number(yums[u.id]);
    if (!amount) return;
    run(() => superadminApi.adjustYums({ userId: u.id, amount, reason: amount > 0 ? "Superadmin qo'shdi" : 'Superadmin ayirdi' }),
      `Yums ${amount > 0 ? '+' : ''}${amount}`);
    setYums(s => ({ ...s, [u.id]: '' }));
  };

  return (
    <div>
      <PanelHeader title="Foydalanuvchilar" subtitle="Rol bo'yicha filtr; ism, email yoki restoran nomi bo'yicha qidiruv" />

      <div className="flex flex-wrap items-center gap-3 mb-4">
        <div className="flex rounded-xl border border-border overflow-hidden">
          {ROLE_FILTERS.map(f => (
            <button key={f.value} onClick={() => setRole(f.value)}
              className={cn('px-3 py-2 text-sm', role === f.value ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:text-foreground')}>
              {f.label}
            </button>
          ))}
        </div>
        <div className="relative flex-1 min-w-[240px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input value={search} onChange={e => setSearch(e.target.value)} className="pl-9 bg-secondary"
            placeholder={role === 'admin' ? 'Ism, email yoki restoran nomi...' : 'Ism, email, telefon yoki restoran nomi...'} />
        </div>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>

      <Table head={['Foydalanuvchi', 'Rol', 'Restoranlari', 'Bronlar', 'Yums', 'Amallar']} empty={!loading && data.items.length === 0}>
        {data.items.map(u => {
          const r = topRole(u.roles);
          return (
            <tr key={u.id} className={cn('border-b border-border/50 align-top', u.isBanned && 'opacity-60')}>
              <td className="p-3">
                <div className="font-medium text-foreground">{u.fullName}</div>
                <div className="text-xs text-muted-foreground">{u.email}</div>
                <div className="text-xs text-muted-foreground">
                  {u.emailConfirmed === false && <span className="text-orange-400 mr-2">email tasdiqlanmagan</span>}
                  {u.isBanned && <span className="text-destructive">bloklangan</span>}
                </div>
              </td>
              <td className="p-3"><span className={cn('text-xs px-2 py-0.5 rounded-full', ROLE_BADGE[r])}>{ROLE_NAMES[r] ?? r}</span></td>
              <td className="p-3 text-xs">
                {u.restaurants?.length ? u.restaurants.map(x => (
                  <div key={x.id}>{x.name}{!x.isApproved && ' (kutilmoqda)'}{x.isSuspended && " (to'xtatilgan)"}</div>
                )) : <span className="text-muted-foreground">—</span>}
              </td>
              <td className="p-3">{u.reservationsCount ?? 0}</td>
              <td className="p-3">
                <div className="font-medium">{u.yumsBalance}</div>
                {r !== 'superadmin' && (
                  <div className="flex gap-1 mt-1">
                    <Input value={yums[u.id] ?? ''} onChange={e => setYums(s => ({ ...s, [u.id]: e.target.value.replace(/[^\d-]/g, '') }))}
                      placeholder="±" className="h-8 w-20 bg-secondary text-xs" aria-label="yums miqdori" />
                    <Button size="sm" variant="outline" className="h-8" onClick={() => adjustYums(u)}>OK</Button>
                  </div>
                )}
              </td>
              <td className="p-3">
                <div className="flex flex-wrap gap-1">
                  <Button size="sm" variant="ghost" onClick={async () => setDetail(await superadminApi.userDetail(u.id))} title="Batafsil">
                    <Eye className="w-4 h-4" />
                  </Button>
                  {r !== 'superadmin' && (
                    <>
                      {r === 'admin' ? (
                        <Button size="sm" variant="ghost" title="Admin rolini olish"
                          onClick={() => run(() => superadminApi.revokeRole({ userId: u.id, role: 'admin' }), 'Admin roli olindi')}>
                          <ShieldOff className="w-4 h-4" />
                        </Button>
                      ) : (
                        <Button size="sm" variant="ghost" title="Restoran egasi (admin) qilish"
                          onClick={() => run(() => superadminApi.assignRole({ userId: u.id, role: 'admin' }), 'Admin qilindi')}>
                          <Shield className="w-4 h-4" />
                        </Button>
                      )}
                      <Button size="sm" variant="ghost" title={u.isBanned ? 'Blokdan chiqarish' : 'Bloklash'}
                        className={u.isBanned ? 'text-success' : 'text-destructive'}
                        onClick={() => run(() => (u.isBanned ? superadminApi.unban({ userId: u.id }) : superadminApi.ban({ userId: u.id })),
                          u.isBanned ? 'Blokdan chiqarildi' : 'Bloklandi')}>
                        <Ban className="w-4 h-4" />
                      </Button>
                    </>
                  )}
                  {u.emailConfirmed === false && (
                    <Button size="sm" variant="ghost" title="Emailni qo'lda tasdiqlash"
                      onClick={() => run(async () => { await superadminApi.confirmEmail(u.id); replace({ ...u, emailConfirmed: true }); }, 'Email tasdiqlandi')}>
                      <MailCheck className="w-4 h-4" />
                    </Button>
                  )}
                </div>
              </td>
            </tr>
          );
        })}
      </Table>
      <Pager page={page} total={data.total} onPage={setPage} />

      <Dialog open={!!detail} onOpenChange={o => !o && setDetail(null)}>
        <DialogContent className="max-w-2xl bg-card border-border max-h-[85vh] overflow-y-auto">
          {detail && (
            <>
              <DialogHeader>
                <DialogTitle className="font-display">{detail.user.fullName}</DialogTitle>
              </DialogHeader>
              <div className="text-sm space-y-1">
                <p>{detail.user.email} · {detail.user.phone || 'telefon yo\'q'}</p>
                <p>Rol: {detail.user.roles.join(', ')} · Yums: {detail.user.yumsBalance} · Kelmagan: {detail.noShowCount}</p>
                <p className="text-muted-foreground">Ro'yxatdan o'tgan: {dateTime(detail.user.createdAt)}</p>
              </div>
              {detail.restaurants.length > 0 && (
                <div>
                  <h4 className="font-medium mt-3 mb-1">Restoranlari</h4>
                  {detail.restaurants.map(r => <p key={r.id} className="text-sm">{r.name} — {r.city}</p>)}
                </div>
              )}
              <h4 className="font-medium mt-3 mb-1">So'nggi bronlar</h4>
              {detail.reservations.length === 0 ? <p className="text-sm text-muted-foreground">Bron yo'q.</p> : (
                <div className="space-y-1">
                  {detail.reservations.map(r => (
                    <div key={r.id} className="flex items-center justify-between gap-2 text-sm border-b border-border/40 py-1">
                      <span>{r.restaurantName} · {dmy(r.date)} {r.time?.slice(0, 5)} · {r.guestsCount} kishi
                        {r.depositAmount > 0 && ` · depozit ${money(r.depositAmount)}${r.depositPaid ? ' ✓' : ''}`}</span>
                      <StatusBadge status={r.status} />
                    </div>
                  ))}
                </div>
              )}
              <h4 className="font-medium mt-3 mb-1">Yums tarixi</h4>
              {detail.yums.length === 0 ? <p className="text-sm text-muted-foreground">Yozuv yo'q.</p> : (
                detail.yums.map(y => (
                  <p key={y.id} className="text-sm"><b className={y.amount >= 0 ? 'text-success' : 'text-destructive'}>{y.amount > 0 ? '+' : ''}{y.amount}</b> — {y.reason} <span className="text-muted-foreground">({dateTime(y.createdAt)})</span></p>
                ))
              )}
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
};
