import type { LucideIcon } from 'lucide-react';
import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { PartnerApplicationForm } from '@/components/PartnerApplicationForm';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { BookingCard } from '@/components/profile/BookingCard';
import { ReferralPanel } from '@/components/profile/ReferralPanel';
import { TelegramCard } from '@/components/profile/TelegramCard';
import { useApp, type ThemeMode } from '@/contexts/AppContext';
import { t, languages } from '@/lib/i18n';
import {
  reservationsApi,
  favoritesApi,
  profileApi,
  reviewsApi,
  ApiError,
  type Reservation as ApiReservation,
  type Restaurant as ApiRestaurant,
  type MyReview,
} from '@/lib/api';
import { mapApiToRestaurant, type Restaurant } from '@/lib/restaurantApi';
import { toast } from 'sonner';
import { localDateTimeString } from '@/lib/utils';
import { motion } from 'framer-motion';
import { RestaurantCard } from '@/components/RestaurantCard';
import {
  CalendarDays, Heart, Star, Store, Users, Settings, User,
  Globe, LogOut, ChevronRight, Sparkles, Sun, Moon, Monitor,
} from 'lucide-react';
import { cn } from '@/lib/utils';

type Tab = 'bookings' | 'favorites' | 'reviews' | 'addRestaurant' | 'refer' | 'settings' | 'personal';
const TABS: Tab[] = ['bookings', 'favorites', 'reviews', 'addRestaurant', 'refer', 'settings', 'personal'];

const ProfilePage = () => {
  const { language, setLanguage, theme, setTheme, user, profile, loading, logout, refreshProfile } = useApp();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const tabParam = searchParams.get('tab') as Tab | null;
  const [activeTab, setActiveTabState] = useState<Tab>(tabParam && TABS.includes(tabParam) ? tabParam : 'bookings');
  const [bookings, setBookings] = useState<ApiReservation[]>([]);
  const [favorites, setFavorites] = useState<Restaurant[]>([]);
  const [reviews, setReviews] = useState<MyReview[] | null>(null);
  const [bookingFilter, setBookingFilter] = useState<'upcoming' | 'history'>('upcoming');

  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [savingPersonal, setSavingPersonal] = useState(false);

  const setActiveTab = (tab: Tab) => {
    setActiveTabState(tab);
    const next = new URLSearchParams(searchParams);
    if (tab === 'bookings') next.delete('tab'); else next.set('tab', tab);
    setSearchParams(next, { replace: true });
  };

  useEffect(() => {
    if (!loading && !user) navigate('/login');
  }, [loading, user, navigate]);

  useEffect(() => {
    if (profile) {
      setFullName(profile.fullName || '');
      setPhone(profile.phone || '');
    }
  }, [profile]);

  const loadBookings = useCallback(
    () => reservationsApi.mine().then(setBookings).catch(() => setBookings([])),
    [],
  );

  // Payme/Click sahifasidan qaytganda (?payment=return) — holatni yangilaymiz
  useEffect(() => {
    if (!user || searchParams.get('payment') !== 'return') return;
    toast.info(t('payment.returned', language));
    const timer = setTimeout(() => { loadBookings(); }, 1500);
    const next = new URLSearchParams(searchParams);
    next.delete('payment');
    setSearchParams(next, { replace: true });
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user]);

  useEffect(() => {
    if (!user) return;
    loadBookings();
    favoritesApi.list().then((restaurants: ApiRestaurant[]) => {
      setFavorites(restaurants.map((r, i) => mapApiToRestaurant(r, i)));
    }).catch(() => setFavorites([]));
  }, [user, loadBookings]);

  useEffect(() => {
    if (user && activeTab === 'reviews' && reviews === null) {
      reviewsApi.mine().then(setReviews).catch(() => setReviews([]));
    }
  }, [user, activeTab, reviews]);

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const handleSavePersonal = async () => {
    if (!user) return;
    setSavingPersonal(true);
    try {
      await profileApi.update({ fullName, phone });
      await refreshProfile();
      toast.success(t('common.saved', language));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('profile.saveError', language));
    } finally {
      setSavingPersonal(false);
    }
  };

  const handleRemoveFavorite = async (restaurantId: string) => {
    try {
      await favoritesApi.remove(restaurantId);
      setFavorites(prev => prev.filter(f => f.id !== restaurantId));
      toast.success(t('favorite.removed', language));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('favorite.removeError', language));
    }
  };

  // Mahalliy vaqt bilan solishtiriladi (toISOString UTC beradi — tunda kun siljiydi)
  const nowLocal = localDateTimeString();
  const isUpcoming = (b: ApiReservation) =>
    (b.status === 'confirmed' || b.status === 'pending') && `${b.date}T${b.time}` > nowLocal;
  const filteredBookings = bookings
    .filter(b => (bookingFilter === 'upcoming' ? isUpcoming(b) : !isUpcoming(b)))
    .sort((a, b) => {
      const ka = `${a.date}T${a.time}`, kb = `${b.date}T${b.time}`;
      return bookingFilter === 'upcoming' ? ka.localeCompare(kb) : kb.localeCompare(ka);
    });

  const menuItems: { key: Tab; icon: LucideIcon; label: string }[] = [
    { key: 'bookings', icon: CalendarDays, label: t('profile.myBookings', language) },
    { key: 'favorites', icon: Heart, label: t('profile.myFavorites', language) },
    { key: 'reviews', icon: Star, label: t('profile.myReviews', language) },
    { key: 'refer', icon: Users, label: t('profile.referFriend', language) },
    { key: 'addRestaurant', icon: Store, label: t('profile.addRestaurant', language) },
    { key: 'personal', icon: User, label: t('profile.personalDetails', language) },
    { key: 'settings', icon: Settings, label: t('profile.settings', language) },
  ];

  const themeOptions: { mode: ThemeMode; icon: LucideIcon; label: string }[] = [
    { mode: 'system', icon: Monitor, label: t('theme.system', language) },
    { mode: 'light', icon: Sun, label: t('theme.lightShort', language) },
    { mode: 'dark', icon: Moon, label: t('theme.darkShort', language) },
  ];

  if (loading) return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 container mx-auto px-4 flex justify-center">
        <div className="animate-pulse text-muted-foreground">{t('common.loading', language)}</div>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-20 sm:pt-24 container mx-auto px-4 pb-16">
        <div className="flex flex-col lg:flex-row gap-6 lg:gap-8">
          {/* Sidebar */}
          <div className="w-full lg:w-72 shrink-0">
            <div className="bg-card rounded-2xl border border-border p-5 mb-4">
              <div className="flex lg:flex-col items-center gap-4 lg:gap-0 lg:text-center">
                <div className="w-14 h-14 lg:w-16 lg:h-16 rounded-full bg-gradient-amber flex items-center justify-center lg:mx-auto lg:mb-3 text-primary-foreground font-bold text-xl shrink-0">
                  {(profile?.fullName || user?.email || 'U')[0].toUpperCase()}
                </div>
                <div className="min-w-0">
                  <h2 className="font-display text-lg font-bold text-foreground truncate">{profile?.fullName}</h2>
                  <p className="text-muted-foreground text-sm truncate">{user?.email}</p>
                  <Link to="/yums" className="inline-flex items-center gap-1 mt-2 bg-primary/10 rounded-full px-3 py-1">
                    <Sparkles className="w-3.5 h-3.5 text-primary" />
                    <span className="text-primary font-bold text-sm">{profile?.yumsBalance || 0} Yums</span>
                  </Link>
                </div>
              </div>
            </div>

            {/* Mobil: gorizontal tablar; kompyuter: ro'yxat */}
            <div className="lg:hidden -mx-4 px-4 overflow-x-auto">
              <div className="flex gap-2 pb-1 w-max">
                {menuItems.map(item => (
                  <button key={item.key} onClick={() => setActiveTab(item.key)}
                    className={cn('flex items-center gap-1.5 px-3 py-2 rounded-full text-sm font-medium border whitespace-nowrap',
                      activeTab === item.key ? 'bg-primary text-primary-foreground border-primary' : 'bg-card text-muted-foreground border-border')}>
                    <item.icon className="w-4 h-4" /> {item.label}
                  </button>
                ))}
              </div>
            </div>
            <div className="hidden lg:block bg-card rounded-2xl border border-border overflow-hidden">
              {menuItems.map(item => (
                <button
                  key={item.key}
                  onClick={() => setActiveTab(item.key)}
                  className={`w-full flex items-center gap-3 px-5 py-3.5 text-sm font-medium transition-all ${activeTab === item.key ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:text-foreground hover:bg-secondary'}`}
                >
                  <item.icon className="w-4 h-4" />
                  {item.label}
                  <ChevronRight className="w-3.5 h-3.5 ml-auto" />
                </button>
              ))}
              <button onClick={handleLogout} className="w-full flex items-center gap-3 px-5 py-3.5 text-sm font-medium text-destructive hover:bg-destructive/10 transition-all">
                <LogOut className="w-4 h-4" />
                {t('profile.logout', language)}
              </button>
            </div>
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            <motion.div key={activeTab} initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
              {activeTab === 'bookings' && (
                <div>
                  {user?.emailConfirmed === false && (
                    <div className="mb-4 bg-primary/10 border border-primary/30 rounded-xl p-4 text-sm flex flex-wrap items-center justify-between gap-3">
                      <span className="text-foreground">{t('verify.requiredForBooking', language)}</span>
                      <Link to="/verify-email?next=/profile"><Button size="sm" variant="hero">{t('verify.title', language)}</Button></Link>
                    </div>
                  )}
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.myBookings', language)}</h2>
                  <div className="flex gap-2 mb-6">
                    <Button variant={bookingFilter === 'upcoming' ? 'default' : 'outline'} size="sm" onClick={() => setBookingFilter('upcoming')}>{t('profile.upcoming', language)}</Button>
                    <Button variant={bookingFilter === 'history' ? 'default' : 'outline'} size="sm" onClick={() => setBookingFilter('history')}>{t('profile.history', language)}</Button>
                  </div>
                  {filteredBookings.length === 0 ? (
                    <div className="text-center py-12 space-y-3">
                      <p className="text-muted-foreground">{t('profile.noBookings', language)}</p>
                      {bookingFilter === 'upcoming' && (
                        <Button variant="hero" size="sm" asChild><Link to="/restaurants">{t('profile.findRestaurant', language)}</Link></Button>
                      )}
                    </div>
                  ) : (
                    <div className="space-y-3">
                      {filteredBookings.map(b => <BookingCard key={b.id} booking={b} onChanged={loadBookings} />)}
                    </div>
                  )}
                </div>
              )}

              {activeTab === 'favorites' && (
                <div>
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.myFavorites', language)}</h2>
                  {favorites.length === 0 ? (
                    <p className="text-muted-foreground text-center py-12">{t('profile.noFavorites', language)}</p>
                  ) : (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      {favorites.map((r, i) => (
                        <div key={r.id} className="relative">
                          <RestaurantCard restaurant={r} index={i} />
                          <button
                            onClick={() => handleRemoveFavorite(r.id)}
                            aria-label={t('favorite.removed', language)}
                            className="absolute top-3 right-3 z-10 w-8 h-8 rounded-full bg-destructive/90 flex items-center justify-center text-destructive-foreground hover:bg-destructive transition-colors"
                          >
                            <Heart className="w-4 h-4 fill-current" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {activeTab === 'reviews' && (
                <div>
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.myReviews', language)}</h2>
                  {reviews === null ? (
                    <div className="h-24 rounded-xl bg-card border border-border animate-pulse" />
                  ) : reviews.length === 0 ? (
                    <p className="text-muted-foreground text-center py-12">{t('profile.noReviews', language)}</p>
                  ) : (
                    <div className="space-y-3">
                      {reviews.map(r => (
                        <div key={r.id} className="bg-card rounded-xl border border-border p-4 space-y-2">
                          <div className="flex items-center justify-between gap-2">
                            <Link to={`/restaurant/${r.restaurantId}`} className="font-medium text-foreground hover:text-primary">{r.restaurantName}</Link>
                            <span className="inline-flex items-center gap-1 text-primary font-bold text-sm"><Star className="w-4 h-4 fill-current" /> {r.rating}</span>
                          </div>
                          {r.comment && <p className="text-sm text-muted-foreground">{r.comment}</p>}
                          {r.ownerResponse && (
                            <div className="text-sm bg-secondary rounded-lg p-3">
                              <p className="text-xs font-medium text-foreground mb-1">{t('review.ownerReply', language)}</p>
                              <p className="text-muted-foreground">{r.ownerResponse}</p>
                            </div>
                          )}
                          <p className="text-xs text-muted-foreground">{new Date(r.createdAt).toLocaleDateString('uz-UZ')}</p>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {activeTab === 'addRestaurant' && (
                <div>
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.addRestaurant', language)}</h2>
                  <p className="text-muted-foreground mb-6 max-w-lg">{t('cta.subtitle', language)}</p>
                  <div className="max-w-lg"><PartnerApplicationForm /></div>
                </div>
              )}

              {activeTab === 'refer' && (
                <div className="max-w-2xl">
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.referFriend', language)}</h2>
                  <ReferralPanel />
                </div>
              )}

              {activeTab === 'personal' && (
                <div>
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.personalDetails', language)}</h2>
                  <div className="bg-card rounded-xl border border-border p-6 space-y-4 max-w-md">
                    <div className="space-y-2"><Label>{t('auth.name', language)}</Label><Input value={fullName} onChange={e => setFullName(e.target.value)} className="bg-secondary" /></div>
                    <div className="space-y-2"><Label>{t('auth.email', language)}</Label><Input value={user?.email || ''} disabled className="bg-secondary" /></div>
                    <div className="space-y-2">
                      <Label>{t('detail.phone', language)}</Label>
                      <Input value={phone} onChange={e => setPhone(e.target.value)} placeholder="+998 90 123 45 67" className="bg-secondary" />
                      <p className="text-xs text-muted-foreground">{t('profile.phoneHint', language)}</p>
                    </div>
                    <Button variant="hero" onClick={handleSavePersonal} disabled={savingPersonal}>
                      {savingPersonal ? '...' : t('common.save', language)}
                    </Button>
                  </div>
                </div>
              )}

              {activeTab === 'settings' && (
                <div>
                  <h2 className="font-display text-2xl font-bold text-foreground mb-4">{t('profile.settings', language)}</h2>
                  <div className="space-y-4 max-w-md">
                    <div className="bg-card rounded-xl border border-border p-5">
                      <div className="flex items-center gap-3 mb-3"><Moon className="w-5 h-5 text-muted-foreground" /><span className="text-foreground font-medium">{t('theme.title', language)}</span></div>
                      <div className="grid grid-cols-3 gap-2">
                        {themeOptions.map(o => (
                          <button key={o.mode} onClick={() => setTheme(o.mode)}
                            className={cn('flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium border',
                              theme === o.mode ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary text-foreground border-border hover:border-primary/50')}>
                            <o.icon className="w-4 h-4" /> {o.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="bg-card rounded-xl border border-border p-5">
                      <div className="flex items-center gap-3 mb-3"><Globe className="w-5 h-5 text-muted-foreground" /><span className="text-foreground font-medium">{t('profile.language', language)}</span></div>
                      <div className="flex flex-wrap gap-2">
                        {languages.map(l => (
                          <button key={l.code} onClick={() => setLanguage(l.code)} className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all border ${language === l.code ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary text-foreground border-border hover:border-primary/50'}`}>
                            {l.flag} {l.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <TelegramCard />
                    <Button variant="outline" className="w-full lg:hidden text-destructive" onClick={handleLogout}>
                      <LogOut className="w-4 h-4 mr-2" /> {t('profile.logout', language)}
                    </Button>
                  </div>
                </div>
              )}
            </motion.div>
          </div>
        </div>
      </div>
      <Footer />
    </div>
  );
};

export default ProfilePage;
