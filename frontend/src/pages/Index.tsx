import { useState, useMemo, useEffect } from "react";
import { Navbar } from "@/components/Navbar";
import { SearchBar } from "@/components/SearchBar";
import { RestaurantCard } from "@/components/RestaurantCard";
import { Footer } from "@/components/Footer";
import { Button } from "@/components/ui/button";
import { motion } from "framer-motion";
import { Sparkles, TrendingUp, Star, MapPin, Loader2 } from "lucide-react";
import heroImage from "@/assets/hero-restaurant.jpg";
import { useNavigate } from "react-router-dom";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";
import { canManage, publicApi, type PublicStats } from "@/lib/api";
import { fetchRestaurants, Restaurant } from "@/lib/restaurantApi";
import { EMPTY_FILTERS, applyFilters, distanceKm, useAvailableIds, type SearchFilters } from "@/lib/search";
import { toast } from "sonner";

type Category = 'popular' | 'withOffers' | 'topRated' | 'new' | 'nearest';

const Index = () => {
  const navigate = useNavigate();
  const { language, user } = useApp();
  const [activeCategory, setActiveCategory] = useState<Category>('popular');
  const [searchFilters, setSearchFilters] = useState<SearchFilters>(EMPTY_FILTERS);
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);
  const [platform, setPlatform] = useState<PublicStats | null>(null);
  const [position, setPosition] = useState<{ lat: number; lng: number } | null>(null);
  const [locating, setLocating] = useState(false);
  const { ids: availableIds, loading: checkingAvailability } = useAvailableIds(searchFilters);

  useEffect(() => {
    fetchRestaurants().then(data => {
      setRestaurants(data);
      setLoading(false);
    });
    publicApi.stats().then(setPlatform).catch(() => setPlatform(null));
  }, []);

  const chooseCategory = (cat: Category) => {
    setActiveCategory(cat);
    if (cat !== 'nearest' || position) return;
    if (!navigator.geolocation) { toast.error(t('geo.unsupported', language)); return; }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      p => { setPosition({ lat: p.coords.latitude, lng: p.coords.longitude }); setLocating(false); },
      () => { setLocating(false); toast.error(t('geo.denied', language)); },
      { timeout: 10000, maximumAge: 600000 },
    );
  };

  const categories: { key: Category; labelKey: string }[] = [
    { key: 'popular', labelKey: 'category.popular' },
    { key: 'withOffers', labelKey: 'category.withOffers' },
    { key: 'topRated', labelKey: 'category.topRated' },
    { key: 'new', labelKey: 'category.new' },
    { key: 'nearest', labelKey: 'category.nearest' },
  ];

  const categorizedRestaurants = useMemo(() => {
    const filtered = applyFilters(restaurants, searchFilters, availableIds);

    switch (activeCategory) {
      case 'withOffers':
        return filtered.filter(r => r.discount);
      case 'topRated':
        return [...filtered].sort((a, b) => b.rating - a.rating);
      case 'new':
        return [...filtered].reverse();
      case 'nearest': {
        if (!position) return filtered;
        const dist = (r: Restaurant) => (r.latitude != null && r.longitude != null
          ? distanceKm(position.lat, position.lng, r.latitude, r.longitude) : Number.POSITIVE_INFINITY);
        return [...filtered].sort((a, b) => dist(a) - dist(b));
      }
      case 'popular':
      default:
        return [...filtered].sort((a, b) => b.reviewCount - a.reviewCount);
    }
  }, [activeCategory, searchFilters, restaurants, availableIds, position]);

  const fmt = (n: number) => n.toLocaleString('ru-RU');
  // Haqiqiy ko'rsatkichlar (bazadan). Hali yuklanmagan bo'lsa — blok ko'rsatilmaydi.
  const stats = platform ? [
    { icon: Sparkles, label: t('stats.restaurants', language), value: fmt(platform.restaurants) },
    { icon: MapPin, label: t('stats.cities', language), value: fmt(platform.cities) },
    { icon: TrendingUp, label: t('stats.bookings', language), value: fmt(platform.reservations) },
    { icon: Star, label: t('stats.rating', language), value: platform.reviews > 0 ? `${platform.avgRating}/10` : '—' },
  ] : [];

  return (
    <div className="min-h-screen bg-background">
      <Navbar />

      <section className="relative min-h-[85vh] flex items-center justify-center overflow-hidden pt-16">
        <div className="absolute inset-0">
          <img src={heroImage} alt="" className="w-full h-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-b from-background/80 via-background/70 to-background" />
        </div>

        <div className="relative z-10 container mx-auto px-4 text-center">
          <motion.div initial={{ opacity: 0, y: 30 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.8 }}>
            <span className="inline-flex items-center gap-2 bg-primary/10 text-primary text-sm font-medium px-4 py-2 rounded-full mb-6 border border-primary/20">
              <Sparkles className="w-4 h-4" />
              {t('hero.badge', language)}
            </span>
            <h1 className="font-display text-4xl md:text-6xl lg:text-7xl font-bold text-foreground mb-4 leading-tight">
              {t('hero.title1', language)}{" "}
              <span className="text-gradient-amber">{t('hero.title2', language)}</span>
            </h1>
            <p className="text-muted-foreground text-lg md:text-xl max-w-2xl mx-auto mb-10">
              {t('hero.subtitle', language)}
            </p>
          </motion.div>

          <SearchBar onSearch={setSearchFilters} />

          {stats.length > 0 && <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2, duration: 0.6 }}
            className="mt-12 md:mt-16 grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-4 max-w-3xl mx-auto"
          >
            {stats.map((stat) => (
              <div key={stat.label} className="glass rounded-xl p-4 text-center">
                <stat.icon className="w-5 h-5 text-primary mx-auto mb-2" />
                <div className="font-display text-xl font-bold text-foreground">{stat.value}</div>
                <div className="text-xs text-muted-foreground">{stat.label}</div>
              </div>
            ))}
          </motion.div>}
        </div>
      </section>

      <section className="container mx-auto px-4 py-20">
        <div className="flex flex-wrap items-end justify-between gap-3 mb-6">
          <div>
            <h2 className="font-display text-3xl md:text-4xl font-bold text-foreground mb-2">{t('featured.title', language)}</h2>
            <p className="text-muted-foreground">{t('featured.subtitle', language)}</p>
          </div>
          <Button variant="outline" onClick={() => navigate("/restaurants")}>{t('featured.viewAll', language)}</Button>
        </div>

        <div className="flex flex-wrap items-center gap-2 mb-8">
          {categories.map(cat => (
            <Button
              key={cat.key}
              variant={activeCategory === cat.key ? 'default' : 'outline'}
              size="sm"
              onClick={() => chooseCategory(cat.key)}
            >
              {cat.key === 'nearest' && locating ? <Loader2 className="w-3.5 h-3.5 animate-spin mr-1" /> : null}
              {t(cat.labelKey, language)}
            </Button>
          ))}
          {checkingAvailability && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
          {availableIds && !checkingAvailability && (
            <span className="text-xs text-muted-foreground">{t('search.availableNow', language)}</span>
          )}
        </div>

        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map(i => (
              <div key={i} className="bg-card rounded-2xl border border-border h-80 animate-pulse" />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {categorizedRestaurants.map((restaurant, index) => (
              <RestaurantCard key={restaurant.id} restaurant={restaurant} index={index} />
            ))}
          </div>
        )}

        {!loading && categorizedRestaurants.length === 0 && (
          <div className="text-center py-20 text-muted-foreground">
            <p className="text-lg">{t('restaurants.empty', language)}</p>
          </div>
        )}
      </section>

      <section className="container mx-auto px-4 pb-20">
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          whileInView={{ opacity: 1, scale: 1 }}
          viewport={{ once: true }}
          className="relative rounded-3xl overflow-hidden p-10 md:p-16 text-center bg-gradient-card border border-border"
        >
          <div className="absolute inset-0 bg-gradient-to-r from-primary/5 via-transparent to-primary/5" />
          <div className="relative z-10">
            <h2 className="font-display text-3xl md:text-4xl font-bold text-foreground mb-4">{t('cta.title', language)}</h2>
            <p className="text-muted-foreground text-lg max-w-xl mx-auto mb-8">{t('cta.subtitle', language)}</p>
            <Button variant="premium" size="xl" onClick={() => navigate(user && canManage(user) ? '/manager' : '/partner')}>
              {t('cta.button', language)}
            </Button>
          </div>
        </motion.div>
      </section>

      <Footer />
    </div>
  );
};

export default Index;
