import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { useApp } from '@/contexts/AppContext';
import { t, tf } from '@/lib/i18n';
import { fetchRestaurantById, Restaurant } from '@/lib/restaurantApi';
import { ProBookingModal } from '@/components/ProBookingModal';
import { ProMenuSection } from '@/components/ProMenuSection';
import { ProReviewSection } from '@/components/ProReviewSection';
import { RestaurantMap } from '@/components/RestaurantMap';
import {
  Star,
  MapPin,
  Clock,
  Phone,
  Heart,
  Percent,
  CheckCircle2,
  ShieldAlert,
  Users,
  CalendarClock,
  Loader2,
} from 'lucide-react';
import { motion } from 'framer-motion';
import {
  favoritesApi,
  menuApi,
  reviewsApi,
  waitlistApi,
  ApiError,
  type MenuCategory,
  type Review,
} from '@/lib/api';
import { toast } from 'sonner';
import { format } from 'date-fns';

type Tab = 'about' | 'menu' | 'reviews' | 'photos';

const dayLabels = {
  uz: { mon: 'Du', tue: 'Se', wed: 'Ch', thu: 'Pa', fri: 'Ju', sat: 'Sh', sun: 'Ya' },
  en: { mon: 'Mon', tue: 'Tue', wed: 'Wed', thu: 'Thu', fri: 'Fri', sat: 'Sat', sun: 'Sun' },
  ru: { mon: 'Пн', tue: 'Вт', wed: 'Ср', thu: 'Чт', fri: 'Пт', sat: 'Сб', sun: 'Вс' },
} as const;

const parseOpeningHours = (raw: string | undefined, language: keyof typeof dayLabels) => {
  const fallback = [{ day: t('detail.hours', language), hours: raw || '10:00-23:00' }];
  if (!raw) return fallback;
  try {
    const parsed = JSON.parse(raw) as Record<string, string>;
    const days = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const;
    return days.map(day => ({
      day: dayLabels[language][day],
      hours: parsed[day]?.replace('-', ' - ') || 'Closed',
    }));
  } catch {
    return fallback;
  }
};

const RestaurantDetailPage = () => {
  const { id } = useParams();
  const { language, user } = useApp();
  const navigate = useNavigate();

  const [bookingOpen, setBookingOpen] = useState(false);
  const [isFavorite, setIsFavorite] = useState(false);
  const [activeTab, setActiveTab] = useState<Tab>('about');
  const [restaurant, setRestaurant] = useState<Restaurant | null>(null);
  const [loading, setLoading] = useState(true);

  // Live data for pro sections
  const [menuCategories, setMenuCategories] = useState<MenuCategory[]>([]);
  const [menuLoading, setMenuLoading] = useState(false);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [reviewsLoading, setReviewsLoading] = useState(false);

  // Waitlist state
  const [waitlistOpen, setWaitlistOpen] = useState(false);
  const [wlDate, setWlDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [wlTime, setWlTime] = useState('19:00');
  const [wlGuests, setWlGuests] = useState(2);
  const [wlNote, setWlNote] = useState('');
  const [wlSubmitting, setWlSubmitting] = useState(false);
  const openingHours = restaurant ? parseOpeningHours(restaurant.opening_hours, language) : [];

  // -------------------------------------------------------------
  // Data loading
  // -------------------------------------------------------------
  useEffect(() => {
    if (!id) return;
    setLoading(true);
    fetchRestaurantById(id).then(r => {
      setRestaurant(r);
      setLoading(false);
    });
  }, [id]);

  const loadMenu = useCallback(() => {
    if (!id) return;
    setMenuLoading(true);
    menuApi
      .public(id)
      .then(setMenuCategories)
      .catch(() => setMenuCategories([]))
      .finally(() => setMenuLoading(false));
  }, [id]);

  const loadReviews = useCallback(() => {
    if (!id) return;
    setReviewsLoading(true);
    reviewsApi
      .byRestaurant(id)
      .then(setReviews)
      .catch(() => setReviews([]))
      .finally(() => setReviewsLoading(false));
  }, [id]);

  useEffect(() => {
    loadMenu();
    loadReviews();
  }, [loadMenu, loadReviews]);

  useEffect(() => {
    if (user && restaurant) {
      favoritesApi
        .list()
        .then(list => setIsFavorite(list.some(r => r.id === restaurant.id)))
        .catch(() => setIsFavorite(false));
    }
  }, [user, restaurant]);

  // -------------------------------------------------------------
  // Handlers
  // -------------------------------------------------------------
  const toggleFavorite = async () => {
    if (!user) {
      navigate('/login');
      return;
    }
    if (!restaurant) return;
    try {
      if (isFavorite) {
        await favoritesApi.remove(restaurant.id);
        setIsFavorite(false);
        toast.success(t('favorite.removed', language));
      } else {
        await favoritesApi.add(restaurant.id);
        setIsFavorite(true);
        toast.success(t('favorite.added', language));
      }
    } catch {
      toast.error(t('favorite.updateError', language));
    }
  };

  const joinWaitlist = async () => {
    if (!user) {
      navigate('/login');
      return;
    }
    if (!restaurant) return;
    setWlSubmitting(true);
    try {
      await waitlistApi.join({
        restaurantId: restaurant.id,
        date: wlDate,
        time: wlTime ? (wlTime.length === 5 ? `${wlTime}:00` : wlTime) : undefined,
        guestsCount: wlGuests,
        note: wlNote.trim() || undefined,
      });
      toast.success(t('waitlist.joined', language));
      setWaitlistOpen(false);
      setWlNote('');
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : t('common.error', language);
      toast.error(msg);
    } finally {
      setWlSubmitting(false);
    }
  };

  // -------------------------------------------------------------
  // Render
  // -------------------------------------------------------------
  if (loading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="pt-16">
          <div className="h-[50vh] bg-card animate-pulse" />
          <div className="container mx-auto px-4 py-8">
            <div className="h-8 w-64 bg-card animate-pulse rounded mb-4" />
          </div>
        </div>
      </div>
    );
  }

  if (!restaurant) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="pt-24 container mx-auto px-4 text-center text-foreground">{t('detail.notFound', language)}</div>
      </div>
    );
  }

  const tabs: Tab[] = ['about', 'menu', 'reviews', 'photos'];
  const gallery = restaurant.images.length > 0 ? restaurant.images : [restaurant.image];

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-16">
        {/* Hero */}
        <div className="relative h-[50vh] overflow-hidden">
          <img src={restaurant.image} alt={restaurant.name} className="w-full h-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-t from-background via-background/40 to-transparent" />
          <div className="absolute bottom-0 left-0 right-0 container mx-auto px-4 pb-8">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
              <div className="flex items-center gap-3 mb-3 flex-wrap">
                {restaurant.discount && (
                  <span className="flex items-center gap-1 bg-gradient-amber text-primary-foreground text-sm font-bold px-3 py-1 rounded-full">
                    <Percent className="w-3.5 h-3.5" /> {restaurant.discount}% {t('card.discount', language)}
                  </span>
                )}
                <span className="glass text-sm font-medium px-3 py-1 rounded-full text-foreground">
                  {restaurant.cuisine_type}
                </span>
                {restaurant.depositRequired && (
                  <span className="glass text-xs font-medium px-3 py-1 rounded-full text-foreground flex items-center gap-1">
                    <ShieldAlert className="w-3 h-3" /> Depozit talab qilinadi
                  </span>
                )}
              </div>
              <h1 className="font-display text-4xl md:text-5xl font-bold text-foreground mb-2">
                {restaurant.name}
              </h1>
              <div className="flex items-center gap-4 text-muted-foreground text-sm flex-wrap">
                <span className="flex items-center gap-1">
                  <Star className="w-4 h-4 fill-primary text-primary" />
                  {restaurant.rating.toFixed(1)} ({restaurant.reviewCount} {t('card.reviews', language)})
                </span>
                <span className="flex items-center gap-1">
                  <MapPin className="w-4 h-4" />
                  {restaurant.address}
                </span>
                <span>{restaurant.price_range}</span>
              </div>
            </motion.div>
          </div>
        </div>

        <div className="container mx-auto px-4 py-8">
          <div className="flex flex-col lg:flex-row gap-8">
            {/* Main column */}
            <div className="flex-1 min-w-0">
              <div className="flex gap-1 mb-6 bg-secondary rounded-xl p-1">
                {tabs.map(tab => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`flex-1 py-2.5 px-4 rounded-lg text-sm font-medium transition-all ${
                      activeTab === tab
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {t(`detail.${tab}`, language)}
                  </button>
                ))}
              </div>

              {/* About tab */}
              {activeTab === 'about' && (
                <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
                  <p className="text-foreground/80 text-lg leading-relaxed">{restaurant.description}</p>

                  {/* Info grid */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <div className="flex items-center gap-2 text-muted-foreground mb-1 text-sm">
                        <MapPin className="w-4 h-4" />
                        {t('detail.address', language)}
                      </div>
                      <p className="text-foreground font-medium">{restaurant.address}</p>
                    </div>
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <div className="flex items-center gap-2 text-muted-foreground mb-1 text-sm">
                        <Clock className="w-4 h-4" />
                        {t('detail.hours', language)}
                      </div>
                      <div className="mt-3 grid grid-cols-1 gap-2">
                        {openingHours.map(item => (
                          <div key={item.day} className="flex items-center justify-between rounded-lg bg-secondary/60 px-3 py-2">
                            <span className="text-xs font-semibold text-muted-foreground">{item.day}</span>
                            <span className="text-sm font-medium text-foreground">{item.hours}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <div className="flex items-center gap-2 text-muted-foreground mb-1 text-sm">
                        <Phone className="w-4 h-4" />
                        {t('detail.phone', language)}
                      </div>
                      <p className="text-foreground font-medium">{restaurant.phone || '+998 90 123 45 67'}</p>
                    </div>
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <div className="flex items-center gap-2 text-muted-foreground mb-1 text-sm">
                        <Star className="w-4 h-4" />
                        {t('reviews.food', language)} / {t('reviews.service', language)} / {t('reviews.ambience', language)}
                      </div>
                      <p className="text-foreground font-medium">
                        {restaurant.foodRating.toFixed(1)} / {restaurant.serviceRating.toFixed(1)} /{' '}
                        {restaurant.interiorRating.toFixed(1)}
                      </p>
                    </div>
                  </div>

                  {/* Amenities */}
                  {restaurant.amenities.length > 0 && (
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <h4 className="font-display text-lg font-bold text-foreground mb-3">{t('detail.amenities', language)}</h4>
                      <div className="flex flex-wrap gap-2">
                        {restaurant.amenities.map(a => (
                          <span
                            key={a}
                            className="flex items-center gap-1.5 bg-secondary text-foreground/90 text-sm px-3 py-1.5 rounded-full"
                          >
                            <CheckCircle2 className="w-3.5 h-3.5 text-primary" />
                            {a}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Policies */}
                  {restaurant.policies.length > 0 && (
                    <div className="bg-card rounded-xl p-5 border border-border">
                      <h4 className="font-display text-lg font-bold text-foreground mb-3">{t('detail.policies', language)}</h4>
                      <ul className="space-y-2">
                        {restaurant.policies.map((p, i) => (
                          <li key={i} className="flex items-start gap-2 text-foreground/80 text-sm">
                            <ShieldAlert className="w-4 h-4 text-primary shrink-0 mt-0.5" />
                            <span>{p}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {/* Location map (TheFork style) */}
                  {restaurant.latitude && restaurant.longitude && (
                    <div>
                      <h4 className="font-display text-lg font-bold text-foreground mb-3">{t('detail.addressMap', language)}</h4>
                      <RestaurantMap
                        latitude={Number(restaurant.latitude)}
                        longitude={Number(restaurant.longitude)}
                        name={restaurant.name}
                        address={restaurant.address}
                        height={340}
                      />
                    </div>
                  )}
                </motion.div>
              )}

              {/* Menu tab */}
              {activeTab === 'menu' && (
                <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
                  {menuLoading ? (
                    <div className="flex items-center justify-center py-16">
                      <Loader2 className="w-6 h-6 animate-spin text-primary" />
                    </div>
                  ) : (
                    <ProMenuSection categories={menuCategories} />
                  )}
                </motion.div>
              )}

              {/* Reviews tab */}
              {activeTab === 'reviews' && (
                <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
                  {reviewsLoading ? (
                    <div className="flex items-center justify-center py-16">
                      <Loader2 className="w-6 h-6 animate-spin text-primary" />
                    </div>
                  ) : (
                    <ProReviewSection
                      restaurantId={restaurant.id}
                      reviews={reviews}
                      restaurantRating={restaurant.rating}
                      foodRating={restaurant.foodRating}
                      serviceRating={restaurant.serviceRating}
                      interiorRating={restaurant.interiorRating}
                      reviewCount={restaurant.reviewCount}
                      onReviewsChanged={loadReviews}
                    />
                  )}
                </motion.div>
              )}

              {/* Photos tab */}
              {activeTab === 'photos' && (
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="grid grid-cols-2 md:grid-cols-3 gap-3"
                >
                  {gallery.map((img, i) => (
                    <div key={i} className="relative group">
                      <img src={img} alt="" className="rounded-xl w-full h-40 object-cover" />
                    </div>
                  ))}
                </motion.div>
              )}
            </div>

            {/* Sidebar: booking card */}
            <div className="w-full lg:w-96 shrink-0">
              <div className="sticky top-24 bg-card rounded-2xl border border-border p-6 space-y-4">
                <h3 className="font-display text-xl font-bold text-foreground">
                  {t('detail.bookTable', language)}
                </h3>
                {restaurant.discount && (
                  <div className="bg-primary/10 border border-primary/20 rounded-xl p-3 text-sm text-primary font-medium flex items-center gap-2">
                    <Percent className="w-4 h-4" />
                    {restaurant.discount}% {t('booking.off', language)}
                  </div>
                )}
                {restaurant.depositRequired && restaurant.depositPerGuest > 0 && (
                  <div className="bg-secondary/60 border border-border rounded-xl p-3 text-sm flex items-start gap-2">
                    <ShieldAlert className="w-4 h-4 text-primary shrink-0 mt-0.5" />
                    <div>
                      <p className="text-foreground font-medium">{t('detail.depositRequired', language)}</p>
                      <p className="text-muted-foreground text-xs mt-0.5">
                        {tf('detail.depositPerGuest', language, { amount: restaurant.depositPerGuest.toLocaleString('ru-RU') })}
                      </p>
                      <p className="text-muted-foreground text-xs mt-0.5">{t('detail.depositPolicy', language)}</p>
                    </div>
                  </div>
                )}
                {restaurant.nextAvailable && (
                  <div className="flex items-center gap-2 text-success text-sm">
                    <Clock className="w-4 h-4" />
                    {restaurant.nextAvailable}
                  </div>
                )}
                <Button
                  variant="hero"
                  size="lg"
                  className="w-full"
                  onClick={() => {
                    if (!user) {
                      navigate('/login');
                      return;
                    }
                    setBookingOpen(true);
                  }}
                >
                  {t('detail.bookTable', language)}
                </Button>
                <Button
                  variant="outline"
                  className="w-full"
                  onClick={() => {
                    if (!user) {
                      navigate('/login');
                      return;
                    }
                    setWaitlistOpen(true);
                  }}
                >
                  <CalendarClock className="w-4 h-4 mr-2" />
                  Kutish ro'yxatiga qo'shilish
                </Button>
                <Button variant="outline" className="w-full" onClick={toggleFavorite}>
                  <Heart className={`w-4 h-4 mr-2 ${isFavorite ? 'fill-destructive text-destructive' : ''}`} />
                  {isFavorite ? t('favorite.remove', language) : t('favorite.add', language)}
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
      <Footer />

      {/* Pro booking modal */}
      <ProBookingModal
        open={bookingOpen}
        onOpenChange={setBookingOpen}
        restaurant={restaurant}
        depositPerGuest={restaurant.depositPerGuest}
        depositRequired={restaurant.depositRequired}
      />

      {/* Waitlist dialog */}
      <Dialog open={waitlistOpen} onOpenChange={setWaitlistOpen}>
        <DialogContent className="bg-card border-border">
          <DialogHeader>
            <DialogTitle className="font-display text-foreground">{t('waitlist.title', language)}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-muted-foreground text-sm">
              {t('waitlist.description', language)}
            </p>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm text-muted-foreground mb-1.5 block">{t('booking.date', language)}</label>
                <Input type="date" value={wlDate} onChange={e => setWlDate(e.target.value)} />
              </div>
              <div>
                <label className="text-sm text-muted-foreground mb-1.5 block">{t('booking.time', language)}</label>
                <Input type="time" value={wlTime} onChange={e => setWlTime(e.target.value)} />
              </div>
            </div>
            <div>
              <label className="text-sm text-muted-foreground mb-1.5 block">{t('booking.guests', language)}</label>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => setWlGuests(Math.max(1, wlGuests - 1))}
                  disabled={wlGuests <= 1}
                >
                  -
                </Button>
                <div className="flex-1 text-center">
                  <span className="font-display text-xl font-bold text-foreground flex items-center justify-center gap-2">
                    <Users className="w-4 h-4" />
                    {wlGuests}
                  </span>
                </div>
                <Button variant="outline" size="icon" onClick={() => setWlGuests(Math.min(20, wlGuests + 1))}>
                  +
                </Button>
              </div>
            </div>
            <div>
              <label className="text-sm text-muted-foreground mb-1.5 block">{t('waitlist.note', language)}</label>
              <Textarea
                rows={3}
                value={wlNote}
                onChange={e => setWlNote(e.target.value)}
                placeholder={t('waitlist.notePlaceholder', language)}
                className="bg-secondary border-border"
              />
            </div>
            <Button variant="hero" className="w-full" onClick={joinWaitlist} disabled={wlSubmitting}>
              {wlSubmitting ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : null}
              {t('waitlist.join', language)}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
};

export default RestaurantDetailPage;
