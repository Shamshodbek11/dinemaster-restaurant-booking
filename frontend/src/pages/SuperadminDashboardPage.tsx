import type { LucideIcon } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { BRAND } from '@/lib/brand';
import { Textarea } from '@/components/ui/textarea';
import { superadminApi, ApiError, type Restaurant, type Venue, type PlatformStats } from '@/lib/api';
import { ManagerSettings } from '@/components/admin/ManagerSettings';
import { ModerationPanel } from '@/components/admin/ModerationPanel';
import { NotificationBell } from '@/components/NotificationBell';
import { UsersPanel } from '@/components/superadmin/UsersPanel';
import { ReservationsPanel } from '@/components/superadmin/ReservationsPanel';
import { ReviewsPanel, PaymentsPanel } from '@/components/superadmin/ReviewsPaymentsPanels';
import {
  ActivityLogPanel, BroadcastPanel, PartnerApplicationsPanel, VenueReservationsPanel,
} from '@/components/superadmin/PlatformPanels';
import { Table } from '@/components/superadmin/common';
import { dateTime, money } from '@/components/superadmin/utils';
import { useApp } from '@/contexts/AppContext';
import { toast } from 'sonner';
import {
  BarChart3, Store, Users, Shield, LogOut, CheckCircle2, XCircle, Building2, ClipboardList, Settings,
  ShieldCheck, Handshake, CalendarDays, Star, PartyPopper, CreditCard, History, Megaphone, ExternalLink, Wrench, Menu, Home,
} from 'lucide-react';
import { cn } from '@/lib/utils';

type Tab =
  | 'stats' | 'partners' | 'restaurants' | 'moderation' | 'reservations' | 'users' | 'reviews'
  | 'venues' | 'venue_bookings' | 'payments' | 'activity' | 'broadcast' | 'yums_audit' | 'settings';

interface YumsAuditEntry {
  id: string;
  userId: string;
  userFullName?: string;
  amount: number;
  reason: string;
  createdAt: string;
}

const NAV: { group: string; items: { key: Tab; icon: LucideIcon; label: string }[] }[] = [
  { group: 'Umumiy', items: [
    { key: 'stats', icon: BarChart3, label: 'Statistika' },
    { key: 'activity', icon: History, label: 'Faoliyat jurnali' },
  ] },
  { group: 'Restoranlar', items: [
    { key: 'partners', icon: Handshake, label: 'Hamkorlik arizalari' },
    { key: 'restaurants', icon: Store, label: 'Restoranlar' },
    { key: 'moderation', icon: ShieldCheck, label: 'Moderatsiya' },
    { key: 'reservations', icon: CalendarDays, label: 'Bronlar' },
    { key: 'reviews', icon: Star, label: 'Sharhlar' },
  ] },
  { group: "To'yxona va zallar", items: [
    { key: 'venues', icon: Building2, label: 'Zallar' },
    { key: 'venue_bookings', icon: PartyPopper, label: 'Zal bronlari' },
  ] },
  { group: 'Mijozlar va pul', items: [
    { key: 'users', icon: Users, label: 'Foydalanuvchilar' },
    { key: 'payments', icon: CreditCard, label: "To'lovlar" },
    { key: 'yums_audit', icon: ClipboardList, label: 'Yums tarixi' },
    { key: 'broadcast', icon: Megaphone, label: 'Ommaviy xabar' },
  ] },
  { group: 'Hisob', items: [{ key: 'settings', icon: Settings, label: 'Sozlamalar' }] },
];

const StatusPill = ({ approved, suspended }: { approved: boolean; suspended: boolean }) =>
  suspended ? <span className="text-xs px-2 py-0.5 rounded-full bg-orange-500/20 text-orange-400">To'xtatilgan</span>
    : approved ? <span className="text-xs px-2 py-0.5 rounded-full bg-green-500/20 text-green-400">Faol</span>
    : <span className="text-xs px-2 py-0.5 rounded-full bg-yellow-500/20 text-yellow-400">Tasdiq kutmoqda</span>;

const SuperadminDashboardPage = () => {
  const { profile, logout } = useApp();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<Tab>('stats');
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [venues, setVenues] = useState<Venue[]>([]);
  const [yumsLog, setYumsLog] = useState<YumsAuditEntry[]>([]);
  const [stats, setStats] = useState<PlatformStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [reject, setReject] = useState<{ id: string; type: 'restaurant' | 'venue' } | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  const loadAll = useCallback(async () => {
    try {
      const [r, v, y, s] = await Promise.all([
        superadminApi.restaurants(), superadminApi.venues(), superadminApi.yumsAudit(), superadminApi.stats(),
      ]);
      setRestaurants(r);
      setVenues(v);
      setYumsLog(y);
      setStats(s);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ma'lumotlarni yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadAll(); }, [loadAll]);

  // Bo'lim almashganda hisoblagichlar (kutilayotganlar) yangilansin
  useEffect(() => { superadminApi.stats().then(setStats).catch(() => {}); }, [activeTab]);

  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn();
      toast.success(ok);
      loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Xatolik');
    }
  };

  const submitReject = () => {
    if (!reject) return;
    const data = { approve: false, rejectionReason: rejectReason };
    act(() => (reject.type === 'restaurant'
      ? superadminApi.approveRestaurant(reject.id, data)
      : superadminApi.approveVenue(reject.id, data)), 'Rad etildi');
    setReject(null);
    setRejectReason('');
  };

  const badges: Partial<Record<Tab, number>> = {
    partners: stats?.pendingPartnerApplications ?? 0,
    restaurants: stats?.pendingRestaurants ?? 0,
    moderation: stats?.pendingChanges ?? 0,
    venues: stats?.pendingVenues ?? 0,
    venue_bookings: stats?.pendingVenueReservations ?? 0,
  };

  if (loading) {
    return (
      <div className="flex min-h-screen bg-background">
        <div className="hidden md:block w-64 bg-[hsl(222,47%,5%)]" />
        <div className="flex-1 p-4 md:p-8 space-y-4">
          <Skeleton className="h-10 w-64" />
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">{[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-28" />)}</div>
        </div>
      </div>
    );
  }

  const pendingRestaurants = restaurants.filter(r => !r.isApproved);
  const pendingVenues = venues.filter(v => !v.isApproved);
  const s = stats;

  const sidebarBody = (inSheet: boolean) => (
      <>
        <div className={cn('p-5 border-b border-white/10 flex items-center gap-2', inSheet && 'pr-12')}>
          <div className="w-9 h-9 rounded-lg bg-gradient-amber flex items-center justify-center">
            <Shield className="w-5 h-5 text-primary-foreground" />
          </div>
          <div className="flex-1 min-w-0">
            <span className="font-display text-sm font-bold text-white">Superadmin</span>
            <p className="text-xs text-white/50 truncate">{BRAND}</p>
          </div>
          <NotificationBell variant="sidebar" />
        </div>
        <nav className="flex-1 py-3 px-3 overflow-y-auto space-y-4">
          {NAV.map(group => (
            <div key={group.group}>
              <p className="px-3 mb-1 text-[11px] uppercase tracking-wider text-white/30">{group.group}</p>
              {group.items.map(item => (
                <button
                  key={item.key}
                  onClick={() => { setActiveTab(item.key); setMobileMenuOpen(false); }}
                  className={cn('w-full flex items-center gap-3 px-3 py-2 rounded-xl text-sm font-medium transition-all',
                    activeTab === item.key ? 'bg-primary/15 text-primary' : 'text-white/60 hover:text-white hover:bg-white/5')}
                >
                  <item.icon className="w-4 h-4 shrink-0" />
                  <span className="flex-1 text-left">{item.label}</span>
                  {!!badges[item.key] && (
                    <span className="min-w-[20px] h-5 px-1.5 rounded-full bg-primary text-primary-foreground text-[11px] font-bold flex items-center justify-center">
                      {badges[item.key]}
                    </span>
                  )}
                </button>
              ))}
            </div>
          ))}
        </nav>
        <div className="p-4 border-t border-white/10">
          <p className="text-xs text-white/70 mb-3 truncate">{profile?.fullName}</p>
          <button onClick={() => navigate('/')}
            className="w-full flex items-center gap-2 px-4 py-2 rounded-xl text-sm text-white/60 hover:text-white hover:bg-white/5">
            <Home className="w-4 h-4" /> Saytga o'tish
          </button>
          <button onClick={() => { logout(); navigate('/login'); }}
            className="w-full flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm text-red-400 hover:bg-red-500/10 transition-all">
            <LogOut className="w-4 h-4" /> Chiqish
          </button>
        </div>
      </>
  );

  const activeLabel = NAV.flatMap(g => g.items).find(i => i.key === activeTab)?.label ?? '';
  const totalBadges = Object.values(badges).reduce((a, b) => a + (b ?? 0), 0);

  return (
    <div className="min-h-screen bg-background">
      <aside className="hidden md:flex fixed left-0 top-0 bottom-0 w-64 bg-[hsl(222,47%,5%)] border-r border-border flex-col z-40">
        {sidebarBody(false)}
      </aside>
      <Sheet open={mobileMenuOpen} onOpenChange={setMobileMenuOpen}>
        <SheetContent side="left" className="md:hidden w-72 p-0 bg-[hsl(222,47%,5%)] border-border flex flex-col">
          <SheetTitle className="sr-only">Menyu</SheetTitle>
          {sidebarBody(true)}
        </SheetContent>
      </Sheet>

      <main className="md:ml-64 min-w-0">
        <div className="md:hidden h-14 border-b border-border flex items-center gap-2 px-4 bg-card sticky top-0 z-30">
          <Button variant="ghost" size="icon" className="-ml-2 relative" onClick={() => setMobileMenuOpen(true)} aria-label="Menyu">
            <Menu className="w-5 h-5" />
            {totalBadges > 0 && <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-primary" />}
          </Button>
          <span className="font-display font-bold text-foreground truncate">{activeLabel}</span>
        </div>
        <div className="p-4 md:p-8">
          {activeTab === 'stats' && s && (
            <div className="space-y-6">
              <h3 className="font-display text-2xl font-bold text-foreground">Platforma statistikasi</h3>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {[
                  { label: 'Foydalanuvchilar', value: s.totalUsers, hint: `+${s.newUsersThisWeek ?? 0} shu hafta`, tab: 'users' as Tab },
                  { label: 'Faol restoranlar', value: s.activeRestaurants ?? 0, hint: `jami ${s.totalRestaurants}`, tab: 'restaurants' as Tab },
                  { label: 'Bugungi bronlar', value: s.totalReservationsToday, hint: `jami ${s.totalReservations}`, tab: 'reservations' as Tab },
                  { label: "To'langan depozitlar", value: money(s.depositsPaidTotal ?? 0), hint: 'jami', tab: 'payments' as Tab },
                ].map(k => (
                  <button key={k.label} onClick={() => setActiveTab(k.tab)} className="bg-card rounded-xl border border-border p-5 text-left hover:border-primary/40 transition-colors">
                    <div className="font-display text-2xl font-bold text-foreground">{k.value}</div>
                    <div className="text-sm text-foreground mt-1">{k.label}</div>
                    <div className="text-xs text-muted-foreground">{k.hint}</div>
                  </button>
                ))}
              </div>

              <h4 className="font-medium text-foreground">Shu oy</h4>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {[
                  { label: 'Bronlar', value: s.reservationsThisMonth ?? 0 },
                  { label: 'Yakunlangan', value: s.completedThisMonth ?? 0 },
                  { label: 'Bekor qilingan', value: s.cancelledThisMonth ?? 0 },
                  { label: 'Kelmagan', value: s.noShowThisMonth ?? 0 },
                ].map(k => (
                  <div key={k.label} className="bg-card rounded-xl border border-border p-5">
                    <div className="font-display text-2xl font-bold text-foreground">{k.value}</div>
                    <div className="text-sm text-muted-foreground">{k.label}</div>
                  </div>
                ))}
              </div>

              <h4 className="font-medium text-foreground">E'tibor talab qiladi</h4>
              <div className="grid grid-cols-2 md:grid-cols-5 gap-3 md:gap-4">
                {[
                  { label: 'Hamkorlik arizalari', value: s.pendingPartnerApplications ?? 0, tab: 'partners' as Tab },
                  { label: 'Yangi restoranlar', value: s.pendingRestaurants, tab: 'restaurants' as Tab },
                  { label: 'Moderatsiya', value: s.pendingChanges ?? 0, tab: 'moderation' as Tab },
                  { label: 'Yangi zallar', value: s.pendingVenues ?? 0, tab: 'venues' as Tab },
                  { label: 'Zal bronlari', value: s.pendingVenueReservations ?? 0, tab: 'venue_bookings' as Tab },
                ].map(k => (
                  <button key={k.label} onClick={() => setActiveTab(k.tab)}
                    className={cn('rounded-xl border p-5 text-left transition-colors',
                      k.value > 0 ? 'bg-primary/10 border-primary/40' : 'bg-card border-border')}>
                    <div className={cn('font-display text-2xl font-bold', k.value > 0 ? 'text-primary' : 'text-foreground')}>{k.value}</div>
                    <div className="text-sm text-muted-foreground">{k.label}</div>
                  </button>
                ))}
              </div>
              <p className="text-xs text-muted-foreground">
                Restoran egalari: {s.totalAdmins} · Bloklanganlar: {s.bannedUsers} · Muomaladagi yums: {s.totalYumsInCirculation}
              </p>
            </div>
          )}

          {activeTab === 'restaurants' && (
            <div className="space-y-6">
              <h3 className="font-display text-2xl font-bold text-foreground">Restoranlar</h3>
              {pendingRestaurants.length > 0 && (
                <div className="space-y-2">
                  <h4 className="font-medium text-foreground">Tasdiq kutmoqda ({pendingRestaurants.length})</h4>
                  {pendingRestaurants.map(r => (
                    <div key={r.id} className="bg-card rounded-xl border border-primary/30 p-5 flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="text-foreground font-medium">{r.name}</p>
                        <p className="text-muted-foreground text-sm">{r.cuisineType} · {r.city}, {r.address} · {r.ownerName} ({r.ownerEmail})</p>
                      </div>
                      <div className="flex gap-2">
                        <Button size="sm" onClick={() => act(() => superadminApi.approveRestaurant(r.id, { approve: true }), 'Tasdiqlandi')}>
                          <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Tasdiqlash
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => setReject({ id: r.id, type: 'restaurant' })}>
                          <XCircle className="w-3.5 h-3.5 mr-1" /> Rad etish
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
              <Table head={['Nomi', 'Egasi', 'Reyting', 'Holat', 'Amallar']}>
                {restaurants.map(r => (
                  <tr key={r.id} className="border-b border-border/50">
                    <td className="p-3"><div className="font-medium text-foreground">{r.name}</div><div className="text-xs text-muted-foreground">{r.cuisineType} · {r.city}</div></td>
                    <td className="p-3 text-xs">{r.ownerName ?? '—'}<div className="text-muted-foreground">{r.ownerEmail}</div></td>
                    <td className="p-3">{Number(r.rating).toFixed(1)} <span className="text-xs text-muted-foreground">({r.reviewCount})</span></td>
                    <td className="p-3"><StatusPill approved={r.isApproved} suspended={r.isSuspended} />
                      {r.hasPendingChanges && <div className="text-xs text-primary mt-1">o'zgarish kutilmoqda</div>}</td>
                    <td className="p-3">
                      <div className="flex flex-wrap gap-1">
                        <Button size="sm" variant="outline" onClick={() => navigate(`/manager?restaurant=${r.id}`)} title="Menyu, stollar, sozlamalar, hisobotlar">
                          <Wrench className="w-3.5 h-3.5 mr-1" /> Boshqarish
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => window.open(`/restaurant/${r.id}`, '_blank')} title="Mijozlar sahifasi">
                          <ExternalLink className="w-3.5 h-3.5" />
                        </Button>
                        {r.isApproved && (
                          <Button size="sm" variant="ghost" className={r.isSuspended ? 'text-success' : 'text-orange-400'}
                            onClick={() => act(() => (r.isSuspended ? superadminApi.unsuspendRestaurant(r.id) : superadminApi.suspendRestaurant(r.id)),
                              r.isSuspended ? 'Faollashtirildi' : "To'xtatildi")}>
                            {r.isSuspended ? 'Tiklash' : "To'xtatish"}
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </Table>
            </div>
          )}

          {activeTab === 'venues' && (
            <div className="space-y-6">
              <h3 className="font-display text-2xl font-bold text-foreground">Zallar</h3>
              {pendingVenues.length > 0 && (
                <div className="space-y-2">
                  <h4 className="font-medium text-foreground">Tasdiq kutmoqda ({pendingVenues.length})</h4>
                  {pendingVenues.map(v => (
                    <div key={v.id} className="bg-card rounded-xl border border-primary/30 p-5 flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="text-foreground font-medium">{v.name}</p>
                        <p className="text-muted-foreground text-sm">{v.venueType} · {v.address}</p>
                      </div>
                      <div className="flex gap-2">
                        <Button size="sm" onClick={() => act(() => superadminApi.approveVenue(v.id, { approve: true }), 'Tasdiqlandi')}>
                          <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Tasdiqlash
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => setReject({ id: v.id, type: 'venue' })}>
                          <XCircle className="w-3.5 h-3.5 mr-1" /> Rad etish
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
              <Table head={['Nomi', 'Turi', "Sig'im", 'Narx', 'Holat', 'Amallar']} empty={venues.length === 0}>
                {venues.map(v => (
                  <tr key={v.id} className="border-b border-border/50">
                    <td className="p-3 font-medium text-foreground">{v.name}<div className="text-xs text-muted-foreground">{v.city}</div></td>
                    <td className="p-3 text-muted-foreground">{v.venueType}</td>
                    <td className="p-3">{v.capacityMin}–{v.capacityMax}</td>
                    <td className="p-3 text-xs">{money(v.pricePerHead)} / mehmon</td>
                    <td className="p-3"><StatusPill approved={v.isApproved} suspended={v.isSuspended} /></td>
                    <td className="p-3">
                      {v.isApproved && (
                        <Button size="sm" variant="ghost" className={v.isSuspended ? 'text-success' : 'text-orange-400'}
                          onClick={() => act(() => (v.isSuspended ? superadminApi.unsuspendVenue(v.id) : superadminApi.suspendVenue(v.id)),
                            v.isSuspended ? 'Faollashtirildi' : "To'xtatildi")}>
                          {v.isSuspended ? 'Tiklash' : "To'xtatish"}
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </Table>
            </div>
          )}

          {activeTab === 'yums_audit' && (
            <div className="space-y-4">
              <h3 className="font-display text-2xl font-bold text-foreground">Yums tarixi</h3>
              <Table head={['Foydalanuvchi', 'Miqdor', 'Sabab', 'Sana']} empty={yumsLog.length === 0}>
                {yumsLog.map(a => (
                  <tr key={a.id} className="border-b border-border/50">
                    <td className="p-3 font-medium text-foreground">{a.userFullName || a.userId?.slice(0, 8)}</td>
                    <td className="p-3"><span className={cn('font-bold', a.amount > 0 ? 'text-green-400' : 'text-red-400')}>{a.amount > 0 ? '+' : ''}{a.amount}</span></td>
                    <td className="p-3 text-muted-foreground">{a.reason || '—'}</td>
                    <td className="p-3 text-muted-foreground text-xs">{dateTime(a.createdAt)}</td>
                  </tr>
                ))}
              </Table>
            </div>
          )}

          {activeTab === 'partners' && <PartnerApplicationsPanel />}
          {activeTab === 'moderation' && <ModerationPanel />}
          {activeTab === 'reservations' && <ReservationsPanel restaurants={restaurants} />}
          {activeTab === 'users' && <UsersPanel />}
          {activeTab === 'reviews' && <ReviewsPanel />}
          {activeTab === 'venue_bookings' && <VenueReservationsPanel />}
          {activeTab === 'payments' && <PaymentsPanel />}
          {activeTab === 'activity' && <ActivityLogPanel restaurants={restaurants} />}
          {activeTab === 'broadcast' && <BroadcastPanel />}
          {activeTab === 'settings' && <ManagerSettings />}
        </div>
      </main>

      <Dialog open={!!reject} onOpenChange={o => !o && setReject(null)}>
        <DialogContent className="bg-card border-border">
          <DialogHeader><DialogTitle className="font-display text-foreground">Rad etish sababi</DialogTitle></DialogHeader>
          <Textarea value={rejectReason} onChange={e => setRejectReason(e.target.value)} placeholder="Sabab egasiga yuboriladi..." className="bg-secondary" rows={3} />
          <Button variant="hero" onClick={submitReject}>Rad etish</Button>
        </DialogContent>
      </Dialog>
    </div>
  );
};

export default SuperadminDashboardPage;
