import { useState, useEffect } from 'react';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { RestaurantCard } from '@/components/RestaurantCard';
import { Button } from '@/components/ui/button';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { fetchRestaurants, Restaurant } from '@/lib/restaurantApi';
import { motion } from 'framer-motion';
import { Percent, Clock, Flame } from 'lucide-react';

const OffersPage = () => {
  const { language } = useApp();
  const [filter, setFilter] = useState<'all' | '20' | '30' | '50'>('all');
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchRestaurants().then(data => {
      setRestaurants(data.filter(r => r.discount));
      setLoading(false);
    });
  }, []);

  const filtered = filter === 'all'
    ? restaurants
    : restaurants.filter(r => (r.discount || 0) >= parseInt(filter));

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 pb-16 container mx-auto px-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-10">
          <div className="inline-flex items-center gap-2 bg-primary/10 text-primary text-sm font-medium px-4 py-2 rounded-full mb-4 border border-primary/20">
            <Flame className="w-4 h-4" />
            {t('offers.badge', language)}
          </div>
          <h1 className="font-display text-3xl md:text-5xl font-bold text-foreground mb-2">{t('offers.title', language)}</h1>
          <p className="text-muted-foreground text-lg">{t('offers.subtitle', language)}</p>
        </motion.div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-10">
          <div className="bg-card rounded-xl border border-border p-5 flex items-center gap-4">
            <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center"><Percent className="w-6 h-6 text-primary" /></div>
            <div>
              <p className="text-2xl font-display font-bold text-foreground">{t('offers.upTo', language)} 50%</p>
              <p className="text-muted-foreground text-sm">{t('offers.discounts', language)}</p>
            </div>
          </div>
          <div className="bg-card rounded-xl border border-border p-5 flex items-center gap-4">
            <div className="w-12 h-12 rounded-full bg-success/10 flex items-center justify-center"><Clock className="w-6 h-6 text-success" /></div>
            <div>
              <p className="text-2xl font-display font-bold text-foreground">{restaurants.length}</p>
              <p className="text-muted-foreground text-sm">{t('offers.activeOffers', language)}</p>
            </div>
          </div>
          <div className="bg-card rounded-xl border border-border p-5 flex items-center gap-4">
            <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center"><Flame className="w-6 h-6 text-primary" /></div>
            <div>
              <p className="text-2xl font-display font-bold text-foreground">{t('offers.limited', language)}</p>
              <p className="text-muted-foreground text-sm">{t('offers.hurry', language)}</p>
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2 mb-8">
          {([
            { key: 'all' as const, label: t('restaurants.all', language) },
            { key: '20' as const, label: '20%+' },
            { key: '30' as const, label: '30%+' },
            { key: '50' as const, label: '50%+' },
          ]).map(f => (
            <Button key={f.key} variant={filter === f.key ? 'default' : 'outline'} size="sm" onClick={() => setFilter(f.key)}>
              {f.label}
            </Button>
          ))}
        </div>

        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map(i => <div key={i} className="bg-card rounded-2xl border border-border h-80 animate-pulse" />)}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {filtered.map((r, i) => <RestaurantCard key={r.id} restaurant={r} index={i} />)}
          </div>
        )}

        {!loading && filtered.length === 0 && (
          <div className="text-center py-20 text-muted-foreground">
            <p className="text-lg">{t('restaurants.empty', language)}</p>
          </div>
        )}
      </div>
      <Footer />
    </div>
  );
};

export default OffersPage;
