import { useState, useMemo, useEffect } from 'react';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { motion } from 'framer-motion';
import { useApp } from '@/contexts/AppContext';
import { venuesApi, Venue } from '@/lib/api';
import { t } from '@/lib/i18n';
import { useNavigate } from 'react-router-dom';
import { Star, MapPin, Users, Building2, Search } from 'lucide-react';

const venueTypes = [
  { value: 'all', labelKey: 'common.all' },
  { value: "To'yxona", labelKey: 'venues.type.weddingHall' },
  { value: 'Banket zali', labelKey: 'venues.type.banquetHall' },
  { value: 'Marosim zali', labelKey: 'venues.type.ceremonyHall' },
  { value: 'Korporativ zal', labelKey: 'venues.type.corporateHall' },
];

const regionOptions = [
  { value: 'all', labelKey: 'venues.region.all', aliases: [] },
  { value: 'andijon', labelKey: 'regions.andijon', aliases: ['andijon', 'andijan'] },
  { value: 'buxoro', labelKey: 'regions.buxoro', aliases: ['buxoro', 'bukhara'] },
  { value: 'fargona', labelKey: 'regions.fargona', aliases: ["farg'ona", 'fargona', 'fergana'] },
  { value: 'jizzax', labelKey: 'regions.jizzax', aliases: ['jizzax', 'jizzakh'] },
  { value: 'xorazm', labelKey: 'regions.xorazm', aliases: ['xorazm', 'khorezm'] },
  { value: 'namangan', labelKey: 'regions.namangan', aliases: ['namangan'] },
  { value: 'navoiy', labelKey: 'regions.navoiy', aliases: ['navoiy', 'navoi'] },
  { value: 'qashqadaryo', labelKey: 'regions.qashqadaryo', aliases: ['qashqadaryo', 'kashkadarya'] },
  { value: 'samarqand', labelKey: 'regions.samarqand', aliases: ['samarqand', 'samarkand'] },
  { value: 'sirdaryo', labelKey: 'regions.sirdaryo', aliases: ['sirdaryo', 'syrdarya'] },
  { value: 'surxondaryo', labelKey: 'regions.surxondaryo', aliases: ['surxondaryo', 'surkhandarya'] },
  { value: 'toshkent_region', labelKey: 'regions.toshkentRegion', aliases: ['toshkent viloyati', 'tashkent region'] },
  { value: 'toshkent_city', labelKey: 'regions.toshkentCity', aliases: ['toshkent', 'tashkent'] },
];

const VenuesPage = () => {
  const { language } = useApp();
  const navigate = useNavigate();
  const [venues, setVenues] = useState<Venue[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeType, setActiveType] = useState('all');
  const [activeRegion, setActiveRegion] = useState('all');
  const [searchQuery, setSearchQuery] = useState('');
  const getVenueTypeLabel = (venueType: string) =>
    t(venueTypes.find(vt => vt.value === venueType)?.labelKey ?? '', language) || venueType;

  useEffect(() => {
    venuesApi.list()
      .then(list => {
        // Faqat tasdiqlangan va faol zallar — backend allaqachon filtrlashi mumkin,
        // lekin xavfsizlik uchun qayta tekshiramiz.
        const approved = list.filter(v => v.isApproved && !v.isSuspended);
        // Reyting bo'yicha kamayish tartibida
        approved.sort((a, b) => Number(b.rating) - Number(a.rating));
        setVenues(approved);
      })
      .catch(() => setVenues([]))
      .finally(() => setLoading(false));
  }, []);

  const filtered = useMemo(() => {
    let results = venues;
    if (activeType !== 'all') results = results.filter(v => v.venueType === activeType);
    if (activeRegion !== 'all') {
      const region = regionOptions.find(r => r.value === activeRegion);
      results = results.filter(v => {
        const city = (v.city || '').toLowerCase();
        return region?.aliases.some(alias => city.includes(alias)) ?? true;
      });
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      results = results.filter(v =>
        v.name.toLowerCase().includes(q) ||
        v.address.toLowerCase().includes(q) ||
        v.city.toLowerCase().includes(q)
      );
    }
    return results;
  }, [activeRegion, activeType, searchQuery, venues]);

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 pb-8 container mx-auto px-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          <h1 className="font-display text-3xl md:text-5xl font-bold text-foreground mb-2">{t('venues.title', language)}</h1>
          <p className="text-muted-foreground text-lg mb-8">{t('venues.subtitle', language)}</p>
        </motion.div>

        <div className="grid grid-cols-1 md:grid-cols-[1fr_280px] gap-3 max-w-3xl mb-6">
          <div className="relative">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-muted-foreground" />
            <Input value={searchQuery} onChange={e => setSearchQuery(e.target.value)} placeholder={t('venues.searchPlaceholder', language)} className="pl-12 bg-secondary h-12 rounded-xl" />
          </div>
          <div className="relative">
            <MapPin className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-muted-foreground" />
            <select
              value={activeRegion}
              onChange={e => setActiveRegion(e.target.value)}
              className="w-full h-12 rounded-xl border border-input bg-secondary pl-12 pr-4 text-sm text-foreground outline-none"
            >
              {regionOptions.map(region => (
                <option key={region.value} value={region.value} className="bg-card text-foreground">
                  {t(region.labelKey, language)}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="flex flex-wrap gap-2 mb-10">
          {venueTypes.map(vt => (
            <Button key={vt.value} variant={activeType === vt.value ? 'default' : 'outline'} size="sm" onClick={() => setActiveType(vt.value)}>
              {t(vt.labelKey, language)}
            </Button>
          ))}
        </div>

        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-16">
            {[1,2,3,4,5,6].map(i => <Skeleton key={i} className="h-80 rounded-2xl" />)}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-16">
            {filtered.map((venue, index) => (
              <motion.div
                key={venue.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.05 }}
                className="bg-card rounded-2xl border border-border overflow-hidden hover:shadow-xl transition-all group cursor-pointer"
                onClick={() => navigate(`/venue/${venue.id}`)}
              >
                <div className="relative h-48 overflow-hidden">
                  <img
                    src={venue.imagesUrl?.[0] || '/placeholder.svg'}
                    alt={venue.name}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                  />
                  <div className="absolute top-3 left-3">
                    <span className="bg-primary/90 text-primary-foreground text-xs font-bold px-2.5 py-1 rounded-full">
                      {getVenueTypeLabel(venue.venueType)}
                    </span>
                  </div>
                </div>
                <div className="p-5">
                  <h3 className="font-display text-lg font-bold text-foreground mb-1">{venue.name}</h3>
                  <div className="flex items-center gap-3 text-sm text-muted-foreground mb-3">
                    <span className="flex items-center gap-1"><Star className="w-3.5 h-3.5 fill-primary text-primary" />{Number(venue.rating).toFixed(1)}</span>
                    <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5" />{venue.city}</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-1 text-sm text-muted-foreground"><Users className="w-3.5 h-3.5" />{venue.capacityMin}-{venue.capacityMax} {t('venues.people', language)}</span>
                    <span className="text-primary font-bold text-sm">{Number(venue.pricePerHead).toLocaleString()} {t('venues.pricePerGuest', language)}</span>
                  </div>
                </div>
              </motion.div>
            ))}
          </div>
        )}

        {!loading && filtered.length === 0 && (
          <div className="text-center py-20 text-muted-foreground">
            <Building2 className="w-12 h-12 mx-auto mb-3 opacity-40" />
            <p className="text-lg">{t('venues.empty', language)}</p>
          </div>
        )}
      </div>
      <Footer />
    </div>
  );
};

export default VenuesPage;
