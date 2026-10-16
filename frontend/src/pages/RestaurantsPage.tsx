import { useState, useMemo, useEffect } from "react";
import { Navbar } from "@/components/Navbar";
import { SearchBar } from "@/components/SearchBar";
import { RestaurantCard } from "@/components/RestaurantCard";
import { Footer } from "@/components/Footer";
import { motion } from "framer-motion";
import { Loader2 } from "lucide-react";
import { useApp } from "@/contexts/AppContext";
import { t, tf } from "@/lib/i18n";
import { fetchRestaurants, Restaurant } from "@/lib/restaurantApi";
import { EMPTY_FILTERS, applyFilters, useAvailableIds, type SearchFilters } from "@/lib/search";

const RestaurantsPage = () => {
  const [searchFilters, setSearchFilters] = useState<SearchFilters>(EMPTY_FILTERS);
  const { language } = useApp();
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading]         = useState(true);

  useEffect(() => {
    fetchRestaurants().then(data => {
      setRestaurants(data);
      setLoading(false);
    });
  }, []);

  const { ids: availableIds, loading: checking } = useAvailableIds(searchFilters);
  const filtered = useMemo(
    () => applyFilters(restaurants, searchFilters, availableIds),
    [searchFilters, restaurants, availableIds],
  );

  return (
    <div className="min-h-screen bg-background">
      <Navbar />

      <div className="pt-24 pb-8 container mx-auto px-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          <h1 className="font-display text-3xl md:text-5xl font-bold text-foreground mb-2">
            {t('restaurants.title', language)}
          </h1>
          <p className="text-muted-foreground text-lg mb-8">
            {t('restaurants.subtitle', language)}
          </p>
        </motion.div>

        <SearchBar onSearch={setSearchFilters} />

        {/* Natija sarlavhasi */}
        {!loading && (
          <p className="text-sm text-muted-foreground mb-6 mt-6 flex items-center gap-2">
            {tf('restaurants.found', language, { n: filtered.length })}
            {availableIds && ` · ${tf('search.availableAt', language, { date: searchFilters.date.split('-').reverse().join('.'), time: searchFilters.time, n: searchFilters.guests })}`}
            {checking && <Loader2 className="w-4 h-4 animate-spin" />}
          </p>
        )}

        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-16">
            {[1, 2, 3, 4, 5, 6].map(i => (
              <div key={i} className="bg-card rounded-2xl border border-border h-80 animate-pulse" />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-16">
            {filtered.map((restaurant, index) => (
              <RestaurantCard key={restaurant.id} restaurant={restaurant} index={index} />
            ))}
          </div>
        )}

        {!loading && filtered.length === 0 && (
          <div className="text-center py-20 text-muted-foreground">
            <p className="text-xl mb-2">{t('restaurants.emptyTitle', language)}</p>
            <p className="text-sm">{availableIds ? t('restaurants.emptyAvailability', language) : t('restaurants.emptyHint', language)}</p>
          </div>
        )}
      </div>

      <Footer />
    </div>
  );
};

export default RestaurantsPage;
