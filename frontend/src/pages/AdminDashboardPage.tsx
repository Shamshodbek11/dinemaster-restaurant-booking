import { useState, useEffect, useCallback, useMemo } from 'react';
import { OpeningHoursEditor } from '@/components/admin/OpeningHoursEditor';
import { SpecialDaysEditor } from '@/components/admin/SpecialDaysEditor';
import { AdminSidebar, type AdminTab } from '@/components/AdminSidebar';
import { AdminMenuBuilder } from '@/components/admin/AdminMenuBuilder';
import { AdminTablesManager } from '@/components/admin/AdminTablesManager';
import { AdminWaitlistManager } from '@/components/admin/AdminWaitlistManager';
import { AdminMonthlyCalendar } from '@/components/admin/AdminMonthlyCalendar';
import { AdminReviewsManager } from '@/components/admin/AdminReviewsManager';
import { AdminReservations } from '@/components/admin/AdminReservations';
import { AdminStaffManager } from '@/components/admin/AdminStaffManager';
import { AdminVenuesPanel } from '@/components/admin/AdminVenuesPanel';
import { RestaurantOnboardingDialog } from '@/components/admin/RestaurantOnboardingDialog';
import { ManagerSettings } from '@/components/admin/ManagerSettings';
import { AdminReports } from '@/components/admin/AdminReports';
import { StatusBadge } from '@/components/superadmin/common';
import { money } from '@/components/superadmin/utils';
import { useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { MoneyInput } from '@/components/ui/money-input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import {
  adminApi,
  restaurantsApi,
  venuesApi,
  isAdmin,
  ApiError,
  type AccessLevel,
  type Restaurant,
  type Reservation,
  type Venue,
} from '@/lib/api';
import { useApp } from '@/contexts/AppContext';
import { toast } from 'sonner';
import { CalendarDays, CheckCircle2, Clock, Menu, Plus, Store, Undo2, UserX, Users } from 'lucide-react';
import { format, addDays, startOfWeek } from 'date-fns';

const HOST_TABS: AdminTab[] = ['dashboard', 'reservations', 'calendar', 'guests', 'waitlist', 'settings'];
const MANAGER_TABS: AdminTab[] = [...HOST_TABS, 'menu', 'tables', 'reviews', 'restaurant', 'discounts', 'reports'];
const OWNER_TABS: AdminTab[] = [...MANAGER_TABS, 'staff'];
const TABS_BY_LEVEL: Record<AccessLevel, AdminTab[]> = {
  owner: OWNER_TABS, manager: MANAGER_TABS, host: HOST_TABS, none: ['settings'],
};
const LEVEL_LABEL: Record<AccessLevel, string> = {
  owner: 'Restoran egasi', manager: 'Menejer', host: 'Host (administrator)', none: '',
};
const WEEK_DAYS = ['Du', 'Se', 'Ch', 'Pa', 'Ju', 'Sh', 'Ya'];

const isActive = (r: Reservation) => r.status === 'confirmed' || r.status === 'pending';

const AdminDashboardPage = () => {
  const { user, profile } = useApp();
  const [searchParams, setSearchParams] = useSearchParams();
  const tabParam = searchParams.get('tab') as AdminTab | null;
  const [activeTab, setActiveTabState] = useState<AdminTab>(tabParam ?? 'dashboard');
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [restaurant, setRestaurant] = useState<Restaurant | null>(null);
  // Bir nechta restoran (yoki superadmin) — ?restaurant=<id> bilan tanlanadi
  const [allRestaurants, setAllRestaurants] = useState<Restaurant[]>([]);
  const selectedRestaurantId = searchParams.get('restaurant');
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [venues, setVenues] = useState<Venue[]>([]);
  const [loading, setLoading] = useState(true);
  const [onboardingOpen, setOnboardingOpen] = useState(false);

  // Restoran tahriri
  const [editName, setEditName] = useState('');
  const [editDesc, setEditDesc] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [editCuisine, setEditCuisine] = useState('');
  const [editPriceRange, setEditPriceRange] = useState('');
  const [editSlots, setEditSlots] = useState(20);
  const [editHourlySlots, setEditHourlySlots] = useState(0); // 0 — cheklanmagan
  const [editOpeningHours, setEditOpeningHours] = useState('{}');
  const [editSpecialHours, setEditSpecialHours] = useState('{}');
  const [editAmenities, setEditAmenities] = useState('');
  const [editPolicies, setEditPolicies] = useState('');
  const [editDepositRequired, setEditDepositRequired] = useState(false);
  const [editDepositPerGuest, setEditDepositPerGuest] = useState(0);
  const [editLatitude, setEditLatitude] = useState<number | ''>('');
  const [editLongitude, setEditLongitude] = useState<number | ''>('');
  const [saving, setSaving] = useState(false);

  // Chegirma
  const [discountPct, setDiscountPct] = useState<number | ''>(0);
  const [discountStart, setDiscountStart] = useState('');
  const [discountEnd, setDiscountEnd] = useState('');

  const [filterDate, setFilterDate] = useState('');

  const accessLevel: AccessLevel = restaurant?.accessLevel ?? 'owner';
  const allowedTabs = useMemo(() => {
    const tabs = [...TABS_BY_LEVEL[accessLevel]];
    if (venues.length > 0) tabs.splice(tabs.length - 1, 0, 'venues');
    return tabs;
  }, [accessLevel, venues.length]);

  const setActiveTab = (tab: AdminTab) => {
    setActiveTabState(tab);
    const next = new URLSearchParams(searchParams);
    if (tab === 'dashboard') next.delete('tab'); else next.set('tab', tab);
    setSearchParams(next, { replace: true });
  };

  // Huquq yetmaydigan bo'lim ochiq qolmasin (masalan, host uchun "Hisobotlar")
  useEffect(() => {
    if (!loading && !allowedTabs.includes(activeTab)) setActiveTabState('dashboard');
  }, [loading, allowedTabs, activeTab]);

  const fillForm = (rest: Restaurant) => {
    setEditName(rest.name);
    setEditDesc(rest.description);
    setEditAddress(rest.address);
    setEditPhone(rest.phone || '');
    setEditCuisine(rest.cuisineType);
    setEditPriceRange(rest.priceRange);
    setEditSlots(rest.totalSlots);
    setEditHourlySlots(rest.hourlySlots ?? 0);
    setEditOpeningHours(rest.openingHours || '{}');
    setEditSpecialHours(rest.specialHours || '{}');
    setEditAmenities((rest.amenities ?? []).join(', '));
    setEditPolicies((rest.policies ?? []).join('\n'));
    setEditDepositRequired(Boolean(rest.depositRequired));
    setEditDepositPerGuest(Number(rest.depositPerGuest ?? 0));
    setEditLatitude(rest.latitude ?? '');
    setEditLongitude(rest.longitude ?? '');
    setDiscountPct(rest.discount ?? 0);
    setDiscountStart(rest.discountTimeStart ? rest.discountTimeStart.slice(0, 5) : '');
    setDiscountEnd(rest.discountTimeEnd ? rest.discountTimeEnd.slice(0, 5) : '');
  };

  const loadReservations = useCallback(async (restaurantId: string) => {
    setReservations(await adminApi.reservations({ restaurantId }));
  }, []);

  const loadData = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    try {
      const [restaurants, myVenues] = await Promise.all([
        adminApi.restaurants(),
        isAdmin(user) ? venuesApi.mine().catch(() => [] as Venue[]) : Promise.resolve([] as Venue[]),
      ]);
      setVenues(myVenues);
      setAllRestaurants(restaurants);
      if (restaurants.length === 0) {
        setRestaurant(null);
        setReservations([]);
        return;
      }
      const rest = restaurants.find(r => r.id === selectedRestaurantId) ?? restaurants[0];
      setRestaurant(rest);
      fillForm(rest);
      await loadReservations(rest.id);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ma'lumotlarni yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [user, selectedRestaurantId, loadReservations]);

  useEffect(() => {
    if (user) loadData();
  }, [user, loadData]);

  const updateReservation = (id: string, patch: Partial<Reservation>) =>
    setReservations(prev => prev.map(r => (r.id === id ? { ...r, ...patch } : r)));

  const handleSaveRestaurant = async () => {
    if (!restaurant) return;
    setSaving(true);
    try {
      const updated = await restaurantsApi.update(restaurant.id, {
        name: editName,
        description: editDesc,
        address: editAddress,
        phone: editPhone,
        cuisineType: editCuisine,
        priceRange: editPriceRange,
        totalSlots: editSlots,
        hourlySlots: editHourlySlots,
        openingHours: editOpeningHours,
        specialHours: editSpecialHours,
        amenities: editAmenities.split(',').map(s => s.trim()).filter(Boolean),
        policies: editPolicies.split('\n').map(s => s.trim()).filter(Boolean),
        depositRequired: editDepositRequired,
        depositPerGuest: editDepositPerGuest,
        latitude: typeof editLatitude === 'number' ? editLatitude : null,
        longitude: typeof editLongitude === 'number' ? editLongitude : null,
      });
      setRestaurant({ ...updated, accessLevel });
      if (updated.pendingChanges) {
        toast.info("Saqlandi. Nomi, tavsif va manzil o'zgarishlari superadmin tasdiqlagach ko'rinadi.");
      } else {
        toast.success('Saqlandi');
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Saqlab bo'lmadi");
    } finally {
      setSaving(false);
    }
  };

  const handleSaveDiscount = async () => {
    if (!restaurant) return;
    if ((discountStart && !discountEnd) || (!discountStart && discountEnd)) {
      toast.error("Chegirma vaqtining boshlanishi va tugashini birga kiriting (yoki ikkalasini bo'sh qoldiring — kun bo'yi).");
      return;
    }
    try {
      const updated = await restaurantsApi.update(restaurant.id, {
        discount: typeof discountPct === 'number' && discountPct > 0 ? discountPct : 0,
        discountTimeStart: discountStart ? `${discountStart}:00` : null,
        discountTimeEnd: discountEnd ? `${discountEnd}:00` : null,
      });
      setRestaurant({ ...updated, accessLevel });
      toast.success('Chegirma saqlandi');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Saqlab bo'lmadi");
    }
  };

  const today = format(new Date(), 'yyyy-MM-dd');
  const nowKey = format(new Date(), "yyyy-MM-dd'T'HH:mm:ss");
  const handleFilterDateChange = (value: string) => setFilterDate(value && value < today ? today : value);
  const todayReservations = reservations
    .filter(r => r.date === today && r.status !== 'cancelled')
    .sort((a, b) => a.time.localeCompare(b.time));
  const upcomingCount = reservations.filter(r => isActive(r) && `${r.date}T${r.time}` >= nowKey).length;
  const completedTotal = reservations.filter(r => r.status === 'completed').length;
  const noShowTotal = reservations.filter(r => r.status === 'no_show').length;
  const refundsPending = reservations.filter(r => r.depositOutcome === 'refund_requested');
  const canManageMoney = accessLevel === 'owner' || accessLevel === 'manager';

  const weekStart = startOfWeek(new Date(), { weekStartsOn: 1 });
  const weekDays = Array.from({ length: 7 }, (_, i) => {
    const d = addDays(weekStart, i);
    const dateStr = format(d, 'yyyy-MM-dd');
    const count = reservations.filter(r => r.date === dateStr && r.status !== 'cancelled').length;
    return { date: d, dateStr, count, label: WEEK_DAYS[i] };
  });

  if (loading) {
    return (
      <div className="flex min-h-screen bg-background">
        <div className="hidden md:block w-64 bg-[hsl(222,47%,8%)]" />
        <div className="flex-1 p-4 md:p-8">
          <div className="space-y-4">
            <Skeleton className="h-10 w-64" />
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-28" />)}
            </div>
          </div>
        </div>
      </div>
    );
  }

  const sidebar = (
    <AdminSidebar
      activeTab={activeTab}
      onTabChange={setActiveTab}
      tabs={restaurant ? allowedTabs : (venues.length ? ['venues', 'settings'] : ['settings'])}
      restaurantName={restaurant?.name}
      userName={profile?.fullName}
      roleLabel={restaurant ? LEVEL_LABEL[accessLevel] : undefined}
      mobileOpen={mobileMenuOpen}
      onMobileOpenChange={setMobileMenuOpen}
    />
  );

  const menuButton = (
    <Button variant="ghost" size="icon" className="md:hidden -ml-2" onClick={() => setMobileMenuOpen(true)} aria-label="Menyu">
      <Menu className="w-5 h-5" />
    </Button>
  );

  if (!restaurant) {
    const ownerRole = isAdmin(user);
    return (
      <div className="min-h-screen bg-background">
        {sidebar}
        <main className="md:ml-64">
          <div className="h-14 md:hidden border-b border-border flex items-center px-4 bg-card">{menuButton}</div>
          {activeTab === 'venues' && venues.length > 0 ? (
            <div className="p-4 md:p-8"><AdminVenuesPanel venues={venues} /></div>
          ) : activeTab === 'settings' ? (
            <div className="p-4 md:p-8"><ManagerSettings /></div>
          ) : (
            <div className="p-6 md:p-8 flex items-center justify-center min-h-[70vh]">
              <div className="text-center space-y-5 max-w-md">
                <div className="w-20 h-20 rounded-2xl bg-primary/10 flex items-center justify-center mx-auto">
                  <Store className="w-10 h-10 text-primary" />
                </div>
                {ownerRole ? (
                  <>
                    <div className="space-y-2">
                      <h2 className="font-display text-2xl font-bold text-foreground">Restoraningizni qo'shing</h2>
                      <p className="text-muted-foreground">
                        Hozircha hisobingizga biriktirilgan restoran yo'q. Birinchi restoraningizni yarating va bron qabul qilishni boshlang.
                      </p>
                    </div>
                    <Button variant="hero" size="lg" onClick={() => setOnboardingOpen(true)}>
                      <Plus className="w-5 h-5 mr-2" /> Restoran qo'shish
                    </Button>
                    <p className="text-xs text-muted-foreground">Restoran tasdiqlanmaguncha foydalanuvchilarga ko'rinmaydi.</p>
                  </>
                ) : (
                  <div className="space-y-2">
                    <h2 className="font-display text-2xl font-bold text-foreground">Restoran biriktirilmagan</h2>
                    <p className="text-muted-foreground">Restoran egasi sizni xodim sifatida qo'shgach, bu yerda bronlar ko'rinadi.</p>
                  </div>
                )}
              </div>
            </div>
          )}
        </main>
        <RestaurantOnboardingDialog
          open={onboardingOpen}
          onOpenChange={setOnboardingOpen}
          onCreated={() => loadData()}
        />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      {sidebar}

      <main className="md:ml-64 min-w-0">
        <div className="h-14 md:h-16 border-b border-border flex items-center gap-2 md:gap-4 px-4 md:px-8 bg-card sticky top-0 z-30">
          {menuButton}
          {allRestaurants.length > 1 ? (
            <select
              value={restaurant.id}
              onChange={e => setSearchParams({ restaurant: e.target.value })}
              className="font-display text-base md:text-lg font-bold text-foreground bg-transparent border border-border rounded-lg px-2 md:px-3 py-1 min-w-0 flex-1 md:flex-none md:max-w-md"
              aria-label="Restoranni tanlash"
            >
              {allRestaurants.map(r => (
                <option key={r.id} value={r.id} className="bg-card text-base">{r.name}{r.isApproved ? '' : ' (tasdiq kutilmoqda)'}</option>
              ))}
            </select>
          ) : (
            <h1 className="font-display text-base md:text-lg font-bold text-foreground truncate flex-1 md:flex-none">{restaurant.name}</h1>
          )}
          {!restaurant.isApproved && <span className="hidden sm:inline text-xs px-2 py-0.5 rounded-full bg-orange-500/15 text-orange-500">Tasdiq kutilmoqda</span>}
          <span className="hidden md:inline text-sm text-muted-foreground ml-auto">{profile?.fullName} · {LEVEL_LABEL[accessLevel]}</span>
        </div>

        <div className="p-4 md:p-8">
          {activeTab === 'dashboard' && (
            <div className="space-y-6">
              {canManageMoney && refundsPending.length > 0 && (
                <div className="rounded-xl border border-orange-500/30 bg-orange-500/10 p-4 text-sm flex flex-wrap items-center gap-3">
                  <Undo2 className="w-4 h-4 text-orange-500" />
                  <span className="text-foreground flex-1">
                    {refundsPending.length} ta bekor qilingan bron bo'yicha depozit mehmonga qaytarilishi kerak
                    ({money(refundsPending.reduce((s, r) => s + r.depositAmount, 0))}).
                  </span>
                  <Button size="sm" variant="outline" onClick={() => setActiveTab('reservations')}>Ko'rish</Button>
                </div>
              )}

              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-4">
                {[
                  { label: 'Bugungi bronlar', value: todayReservations.length, icon: CalendarDays, color: 'text-primary' },
                  { label: 'Kelgusi bronlar', value: upcomingCount, icon: Clock, color: 'text-blue-500' },
                  { label: 'Kelgan mehmonlar', value: completedTotal, icon: CheckCircle2, color: 'text-green-500' },
                  { label: 'Kelmaganlar', value: noShowTotal, icon: UserX, color: 'text-red-500' },
                ].map(stat => (
                  <div key={stat.label} className="bg-card rounded-xl border border-border p-4 md:p-5">
                    <stat.icon className={`w-6 h-6 ${stat.color} mb-2`} />
                    <div className="font-display text-2xl font-bold text-foreground">{stat.value}</div>
                    <div className="text-sm text-muted-foreground">{stat.label}</div>
                  </div>
                ))}
              </div>

              <div>
                <h3 className="font-display text-lg font-bold text-foreground mb-4">Shu hafta</h3>
                <div className="grid grid-cols-7 gap-1 md:gap-2">
                  {weekDays.map(d => (
                    <button
                      key={d.dateStr}
                      onClick={() => { handleFilterDateChange(d.dateStr); setActiveTab('reservations'); }}
                      className={`min-w-0 bg-card rounded-lg md:rounded-xl border px-0.5 py-2 md:p-4 text-center transition-all hover:border-primary/50 ${d.dateStr === today ? 'border-primary' : 'border-border'}`}
                    >
                      <div className="text-xs text-muted-foreground">{d.label}</div>
                      <div className="font-display text-base md:text-lg font-bold text-foreground">{format(d.date, 'd')}</div>
                      <div className={`text-[11px] md:text-xs font-medium mt-1 ${d.count > 0 ? 'text-primary' : 'text-muted-foreground'}`}>
                        {d.count}<span className="hidden md:inline"> bron</span>
                      </div>
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <h3 className="font-display text-lg font-bold text-foreground mb-4">Bugun</h3>
                {todayReservations.length === 0 ? (
                  <div className="text-center py-8 text-muted-foreground">
                    <CalendarDays className="w-10 h-10 mx-auto mb-2 opacity-40" />
                    <p>Bugun bronlar yo'q</p>
                  </div>
                ) : (
                  <div className="space-y-2">
                    {todayReservations.map(r => (
                      <button key={r.id} onClick={() => { handleFilterDateChange(today); setActiveTab('reservations'); }}
                        className="w-full text-left bg-card rounded-xl border border-border p-4 flex items-center justify-between gap-3 hover:border-primary/40">
                        <div className="min-w-0">
                          <p className="text-foreground font-medium truncate">{r.time?.slice(0, 5)} · {r.userFullName || 'Mehmon'}</p>
                          <p className="text-muted-foreground text-sm">
                            {r.guestsCount} kishi{r.tableLabel ? ` · stol ${r.tableLabel}` : ''}
                            {r.depositAmount > 0 && !r.depositPaid ? ' · depozit to\'lanmagan' : ''}
                          </p>
                        </div>
                        <StatusBadge status={r.status} />
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {activeTab === 'reservations' && (
            <AdminReservations
              restaurantId={restaurant.id}
              accessLevel={accessLevel}
              reservations={reservations}
              onUpdate={updateReservation}
              filterDate={filterDate}
              onFilterDate={handleFilterDateChange}
              today={today}
              onReload={() => { loadReservations(restaurant.id).catch(() => {}); }}
            />
          )}

          {activeTab === 'calendar' && (
            <AdminMonthlyCalendar
              restaurantId={restaurant.id}
              onSelectDate={d => { handleFilterDateChange(d); setActiveTab('reservations'); }}
            />
          )}

          {activeTab === 'menu' && <AdminMenuBuilder restaurantId={restaurant.id} />}
          {activeTab === 'tables' && <AdminTablesManager restaurantId={restaurant.id} />}
          {activeTab === 'waitlist' && <AdminWaitlistManager restaurantId={restaurant.id} />}
          {activeTab === 'staff' && <AdminStaffManager restaurantId={restaurant.id} />}
          {activeTab === 'venues' && <AdminVenuesPanel venues={venues} />}

          {activeTab === 'reviews' && (
            <AdminReviewsManager
              restaurantId={restaurant.id}
              restaurantRating={restaurant.rating}
              foodRating={restaurant.foodRating ?? 0}
              serviceRating={restaurant.serviceRating ?? 0}
              interiorRating={restaurant.interiorRating ?? 0}
              reviewCount={restaurant.reviewCount}
            />
          )}

          {activeTab === 'guests' && (
            <div className="space-y-4">
              <Input type="date" value={filterDate} min={today} onChange={e => handleFilterDateChange(e.target.value)} className="w-44 bg-secondary" aria-label="Sana" />
              {(() => {
                const guests = reservations.filter(r => (!filterDate || r.date === filterDate) && r.status !== 'cancelled');
                return guests.length === 0 ? (
                  <div className="text-center py-12 text-muted-foreground bg-card rounded-xl border border-border">
                    <Users className="w-10 h-10 mx-auto mb-2 opacity-40" />
                    <p>Mehmonlar topilmadi</p>
                  </div>
                ) : (
                  <div className="bg-card rounded-xl border border-border overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-border text-left text-muted-foreground">
                          {['Sana', 'Vaqt', 'Ism', 'Telefon', 'Kishi', 'Maxsus istak', 'Oldindan buyurtma', 'Holat'].map(h => (
                            <th key={h} className="p-3 font-medium whitespace-nowrap">{h}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {guests.map(r => (
                          <tr key={r.id} className="border-b border-border last:border-0">
                            <td className="p-3 text-foreground whitespace-nowrap">{r.date.split('-').reverse().join('.')}</td>
                            <td className="p-3 text-foreground">{r.time?.slice(0, 5)}</td>
                            <td className="p-3 text-foreground font-medium whitespace-nowrap">{r.userFullName || '—'}</td>
                            <td className="p-3 text-muted-foreground whitespace-nowrap">{r.userPhone || '—'}</td>
                            <td className="p-3 text-foreground">{r.guestsCount}</td>
                            <td className="p-3 text-muted-foreground italic min-w-[10rem]">{r.specialRequests || '—'}</td>
                            <td className="p-3 text-muted-foreground whitespace-nowrap">{r.preOrderTotal > 0 ? money(r.preOrderTotal) : '—'}</td>
                            <td className="p-3"><StatusBadge status={r.status} /></td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                );
              })()}
            </div>
          )}

          {activeTab === 'restaurant' && (
            <div className="max-w-2xl space-y-4">
              <h3 className="font-display text-lg font-bold text-foreground mb-4">Restoran ma'lumotlari</h3>
              {restaurant.pendingChanges && (
                <div className="bg-primary/10 border border-primary/30 rounded-xl p-4 text-sm text-foreground space-y-1">
                  <p className="font-medium">Moderatsiyada</p>
                  <p className="text-muted-foreground">
                    Quyidagi o'zgarishlar superadmin tasdig'ini kutmoqda (hozircha mijozlarga eski qiymatlar ko'rinadi):
                    {' '}{Object.keys(restaurant.pendingChanges).join(', ')}
                  </p>
                </div>
              )}
              <div className="space-y-2">
                <Label>Nomi</Label>
                <Input value={editName} onChange={e => setEditName(e.target.value)} className="bg-secondary" />
              </div>
              <div className="space-y-2">
                <Label>Tavsif</Label>
                <Textarea value={editDesc} onChange={e => setEditDesc(e.target.value)} className="bg-secondary" rows={3} />
              </div>
              <div className="space-y-2">
                <Label>Manzil</Label>
                <Input value={editAddress} onChange={e => setEditAddress(e.target.value)} className="bg-secondary" />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <CoordinateInput label="Kenglik (lat)" value={editLatitude} onChange={setEditLatitude} placeholder="41.2995" />
                <CoordinateInput label="Uzunlik (lng)" value={editLongitude} onChange={setEditLongitude} placeholder="69.2401" />
              </div>
              <div className="space-y-2">
                <Label>Telefon</Label>
                <Input value={editPhone} onChange={e => setEditPhone(e.target.value)} className="bg-secondary" />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Oshxona turi</Label>
                  <Input value={editCuisine} onChange={e => setEditCuisine(e.target.value)} className="bg-secondary" />
                </div>
                <div className="space-y-2">
                  <Label>Narx darajasi</Label>
                  <Input value={editPriceRange} onChange={e => setEditPriceRange(e.target.value)} className="bg-secondary" />
                </div>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Bir kunda qabul qilinadigan bronlar</Label>
                  <MoneyInput value={editSlots} onChange={setEditSlots} step={5} min={1} formatThousands={false} placeholder="20" className="bg-secondary" />
                </div>
                <div className="space-y-2">
                  <Label>Bir soatda (0 — cheklanmagan)</Label>
                  <MoneyInput value={editHourlySlots} onChange={setEditHourlySlots} step={1} min={0} formatThousands={false} placeholder="0" className="bg-secondary" />
                </div>
              </div>
              <OpeningHoursEditor value={editOpeningHours} onChange={setEditOpeningHours} />
              <SpecialDaysEditor value={editSpecialHours} onChange={setEditSpecialHours} />
              <div className="space-y-2">
                <Label>Qulayliklar (vergul bilan ajratilgan)</Label>
                <Input value={editAmenities} onChange={e => setEditAmenities(e.target.value)} className="bg-secondary" placeholder="Wi-Fi, Avtoturargoh, Bolalar zonasi, Veranda" />
              </div>
              <div className="space-y-2">
                <Label>Qoidalar (har bir qoida yangi qatorda)</Label>
                <Textarea value={editPolicies} onChange={e => setEditPolicies(e.target.value)} className="bg-secondary" rows={4} placeholder={"Chekish taqiqlangan\nHayvonlar ruxsat etilmaydi"} />
              </div>
              <div className="rounded-xl border border-border p-4 space-y-3">
                <div className="flex items-center gap-3">
                  <input id="dep-req" type="checkbox" checked={editDepositRequired} onChange={e => setEditDepositRequired(e.target.checked)} className="w-4 h-4" />
                  <Label htmlFor="dep-req">Depozit talab qilinadi</Label>
                </div>
                <div className="space-y-2">
                  <Label>Har bir mehmon uchun depozit</Label>
                  <MoneyInput value={editDepositPerGuest} onChange={setEditDepositPerGuest} step={10000} min={0} placeholder="100 000" suffix="so'm" className="bg-secondary" disabled={!editDepositRequired} />
                </div>
                <p className="text-xs text-muted-foreground">
                  Mehmon bron vaqtidan 24 soat oldin bekor qilsa depozit qaytariladi; kechroq bekor qilsa yoki kelmasa — restoranda qoladi.
                  Siz bekor qilsangiz, depozit to'liq qaytariladi.
                </p>
              </div>
              <Button variant="hero" onClick={handleSaveRestaurant} disabled={saving}>{saving ? 'Saqlanmoqda...' : 'Saqlash'}</Button>
            </div>
          )}

          {activeTab === 'discounts' && (
            <div className="max-w-lg space-y-6">
              <div>
                <h3 className="font-display text-lg font-bold text-foreground">Chegirmalar</h3>
                <p className="text-sm text-muted-foreground mt-1">
                  Bo'sh soatlarga mehmon jalb qiling: chegirma tanlangan vaqt oralig'idagi bronlarga avtomatik qo'llanadi,
                  bron oynasida va "Chegirmalar" sahifasida ko'rinadi. Yums chegirmasi bilan birga jami 50% dan oshmaydi.
                </p>
              </div>
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>Chegirma (%)</Label>
                  <MoneyInput value={discountPct} onChange={v => setDiscountPct(v)} step={5} min={0} max={100} formatThousands={false} placeholder="0" suffix="%" className="bg-secondary" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Boshlanish vaqti</Label>
                    <Input type="time" value={discountStart} onChange={e => setDiscountStart(e.target.value)} className="bg-secondary" />
                  </div>
                  <div className="space-y-2">
                    <Label>Tugash vaqti</Label>
                    <Input type="time" value={discountEnd} onChange={e => setDiscountEnd(e.target.value)} className="bg-secondary" />
                  </div>
                </div>
                <p className="text-xs text-muted-foreground">Vaqt ko'rsatilmasa — chegirma kun bo'yi amal qiladi. 0% — chegirmani o'chirish.</p>
                <Button variant="hero" onClick={handleSaveDiscount}>Chegirmani saqlash</Button>
              </div>
            </div>
          )}

          {activeTab === 'reports' && <AdminReports restaurantId={restaurant.id} />}
          {activeTab === 'settings' && <ManagerSettings />}
        </div>
      </main>
    </div>
  );
};

const CoordinateInput = ({ label, value, onChange, placeholder }: {
  label: string; value: number | ''; onChange: (v: number | '') => void; placeholder: string;
}) => (
  <div className="space-y-2">
    <Label>{label}</Label>
    <Input
      type="text"
      inputMode="decimal"
      value={value === '' ? '' : String(value)}
      onChange={e => {
        const v = e.target.value.replace(/[^0-9.-]/g, '');
        if (v === '' || v === '-' || v === '.') { onChange(''); return; }
        const n = Number(v);
        if (!Number.isNaN(n)) onChange(n);
      }}
      className="bg-secondary"
      placeholder={placeholder}
    />
  </div>
);

export default AdminDashboardPage;
