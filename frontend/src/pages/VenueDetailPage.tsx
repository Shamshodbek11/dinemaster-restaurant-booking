import type { LucideIcon } from 'lucide-react';
import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useApp } from '@/contexts/AppContext';
import { venuesApi, ApiError, type Venue, type VenueReservation } from '@/lib/api';
import { t } from '@/lib/i18n';
import { toast } from 'sonner';
import { Star, MapPin, Phone, Users, Building2, CheckCircle2, Sparkles, ChevronRight, ChevronLeft } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { format } from 'date-fns';
import { cn } from '@/lib/utils';
import { Slider } from '@/components/ui/slider';

// Yums chegirmasi hozircha o'chirilgan (backend qoidasi tasdiqlangunga qadar)
const YUMS_ENABLED = false;

const eventTypes = [
  { value: "To'y", labelKey: 'venues.event.wedding' },
  { value: "Tug'ilgan kun", labelKey: 'venues.event.birthday' },
  { value: 'Korporativ', labelKey: 'venues.event.corporate' },
  { value: "Xatna to'yi", labelKey: 'venues.event.circumcision' },
  { value: 'Boshqa', labelKey: 'venues.event.other' },
];

const venueTypeLabelKeys: Record<string, string> = {
  "To'yxona": 'venues.type.weddingHall',
  'Banket zali': 'venues.type.banquetHall',
  'Marosim zali': 'venues.type.ceremonyHall',
  'Korporativ zal': 'venues.type.corporateHall',
};

const VenueDetailPage = () => {
  const { id } = useParams();
  const { user, profile, refreshProfile, language } = useApp();
  const navigate = useNavigate();
  const [venue, setVenue] = useState<Venue | null>(null);
  const [loading, setLoading] = useState(true);
  const [bookingOpen, setBookingOpen] = useState(false);

  // Booking state
  const [step, setStep] = useState(1);
  const [date, setDate] = useState<Date>();
  const [guests, setGuests] = useState(100);
  const [eventType, setEventType] = useState("To'y");
  const [specialRequests, setSpecialRequests] = useState('');
  const [useYums, setUseYums] = useState(false);
  const [bookingLoading, setBookingLoading] = useState(false);
  const [bookingResult, setBookingResult] = useState<VenueReservation | null>(null);
  const [bookedDates, setBookedDates] = useState<string[]>([]);

  useEffect(() => {
    if (!id) return;
    venuesApi.get(id)
      .then(v => {
        setVenue(v);
        setGuests(v.capacityMin);
      })
      .catch(() => setVenue(null))
      .finally(() => setLoading(false));

    venuesApi.bookedDates(id)
      .then(setBookedDates)
      .catch(() => setBookedDates([]));
  }, [id]);

  const yumsBalance = profile?.yumsBalance || 0;
  const canUseYums = yumsBalance >= 1000;
  const totalPrice = venue ? guests * Number(venue.pricePerHead || 0) : 0;
  const venueTypeLabel = venue ? t(venueTypeLabelKeys[venue.venueType] ?? '', language) || venue.venueType : '';
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const handleConfirm = async () => {
    if (!user || !date || !venue) return;
    setBookingLoading(true);
    try {
      const data = await venuesApi.createReservation(venue.id, {
        eventDate: format(date, 'yyyy-MM-dd'),
        eventType,
        guestsCount: guests,
        specialRequests: specialRequests || undefined,
      });
      setBookingResult(data);
      setStep(5);
      // Profildagi yums balansi serverda yangilanishi mumkin — kontekstni yangilab olamiz
      refreshProfile().catch(() => {});
      toast.success(t('booking.confirmed', language));
    } catch (err) {
      const message = err instanceof ApiError ? err.message : t('venues.bookingError', language);
      toast.error(message);
    } finally {
      setBookingLoading(false);
    }
  };

  const resetBooking = () => {
    setStep(1);
    setDate(undefined);
    setGuests(venue?.capacityMin || 100);
    setEventType("To'y");
    setSpecialRequests('');
    setUseYums(false);
    setBookingResult(null);
    setBookingOpen(false);
  };

  if (loading) return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-16"><Skeleton className="h-[50vh] w-full" /></div>
    </div>
  );

  if (!venue) return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 container mx-auto px-4 text-center">
        <Building2 className="w-16 h-16 text-muted-foreground mx-auto mb-4" />
        <p className="text-foreground text-lg">{t('venues.notFound', language)}</p>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-16">
        <div className="relative h-[50vh] overflow-hidden">
          <img src={venue.imagesUrl?.[0] || '/placeholder.svg'} alt={venue.name} className="w-full h-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-t from-background via-background/40 to-transparent" />
          <div className="absolute bottom-0 left-0 right-0 container mx-auto px-4 pb-8">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
              <span className="inline-block bg-primary/90 text-primary-foreground text-sm font-bold px-3 py-1 rounded-full mb-3">{venueTypeLabel}</span>
              <h1 className="font-display text-4xl md:text-5xl font-bold text-foreground mb-2">{venue.name}</h1>
              <div className="flex items-center gap-4 text-muted-foreground text-sm flex-wrap">
                <span className="flex items-center gap-1"><Star className="w-4 h-4 fill-primary text-primary" />{Number(venue.rating).toFixed(1)}</span>
                <span className="flex items-center gap-1"><MapPin className="w-4 h-4" />{venue.address}</span>
                <span className="flex items-center gap-1"><Users className="w-4 h-4" />{venue.capacityMin}-{venue.capacityMax} {t('venues.people', language)}</span>
              </div>
            </motion.div>
          </div>
        </div>

        <div className="container mx-auto px-4 py-8">
          <div className="flex flex-col lg:flex-row gap-8">
            <div className="flex-1 space-y-6">
              <p className="text-foreground/80 text-lg leading-relaxed">{venue.description}</p>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <InfoCard icon={MapPin} label={t('detail.address', language)} value={venue.address} />
                <InfoCard icon={Users} label={t('venues.capacity', language)} value={`${venue.capacityMin} - ${venue.capacityMax} ${t('venues.people', language)}`} />
                <InfoCard icon={Phone} label={t('detail.phone', language)} value={venue.phone || '+998 90 123 45 67'} />
                <div className="bg-card rounded-xl p-5 border border-border">
                  <div className="text-sm text-muted-foreground mb-1">{t('venues.price', language)}</div>
                  <p className="text-primary font-bold text-lg">{Number(venue.pricePerHead).toLocaleString('ru-RU')} {t('venues.pricePerGuest', language)}</p>
                </div>
              </div>
            </div>

            <div className="w-full lg:w-96 shrink-0">
              <div className="sticky top-24 bg-card rounded-2xl border border-border p-6 space-y-4">
                <h3 className="font-display text-xl font-bold text-foreground">{t('venues.bookVenue', language)}</h3>
                <Button variant="hero" size="lg" className="w-full" onClick={() => {
                  if (!user) { navigate('/login'); return; }
                  setBookingOpen(true);
                }}>
                  {t('card.book', language)}
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
      <Footer />

      {/* Booking Modal */}
      <Dialog open={bookingOpen} onOpenChange={resetBooking}>
        <DialogContent className="sm:max-w-lg bg-card border-border p-0 overflow-hidden">
          <div className="flex gap-1 px-6 pt-6">
            {[1,2,3,4].map(s => <div key={s} className={`h-1 flex-1 rounded-full transition-all ${s <= step ? 'bg-primary' : 'bg-secondary'}`} />)}
          </div>
          <div className="px-6 pb-6">
            <DialogHeader className="mb-4">
              <DialogTitle className="font-display text-xl text-foreground">
                {step === 1 && t('venues.stepDate', language)}
                {step === 2 && t('venues.stepDetails', language)}
                {step === 3 && t('booking.specialRequests', language)}
                {step === 4 && t('common.confirm', language)}
                {step === 5 && t('booking.confirmed', language)}
              </DialogTitle>
            </DialogHeader>

            <AnimatePresence mode="wait">
              {step === 1 && (
                <motion.div key="s1" initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }} className="space-y-4">
                  <Calendar mode="single" selected={date} onSelect={setDate}
                    disabled={(d) => d < today || bookedDates.includes(format(d, 'yyyy-MM-dd'))}
                    className={cn("p-3 pointer-events-auto rounded-xl border border-border")} />
                  <Button variant="hero" className="w-full" disabled={!date} onClick={() => setStep(2)}>
                    {t('common.next', language)} <ChevronRight className="w-4 h-4" />
                  </Button>
                </motion.div>
              )}
              {step === 2 && (
                <motion.div key="s2" initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }} className="space-y-4">
                  <div>
                    <Label className="text-sm text-muted-foreground mb-2 block">{t('booking.guests', language)}: {guests}</Label>
                    <Slider value={[guests]} onValueChange={v => setGuests(v[0])} min={venue.capacityMin} max={venue.capacityMax} step={10} className="py-2" />
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground mb-2 block">{t('venues.eventType', language)}</Label>
                    <div className="grid grid-cols-2 gap-2">
                      {eventTypes.map(et => (
                        <button key={et.value} onClick={() => setEventType(et.value)} className={`py-2 px-3 rounded-lg text-sm font-medium border transition-all ${eventType === et.value ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary text-foreground border-border hover:border-primary/50'}`}>
                          {t(et.labelKey, language)}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div className="flex gap-3">
                    <Button variant="outline" className="flex-1" onClick={() => setStep(1)}><ChevronLeft className="w-4 h-4" /> {t('common.back', language)}</Button>
                    <Button variant="hero" className="flex-1" onClick={() => setStep(3)}>{t('common.next', language)} <ChevronRight className="w-4 h-4" /></Button>
                  </div>
                </motion.div>
              )}
              {step === 3 && (
                <motion.div key="s3" initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }} className="space-y-4">
                  <Textarea value={specialRequests} onChange={e => setSpecialRequests(e.target.value)} placeholder={t('booking.specialRequestsPlaceholder', language)} rows={3} className="bg-secondary border-border" />
                  {canUseYums && (
                    <div className="flex items-center justify-between bg-secondary rounded-xl p-4 border border-border">
                      <div>
                        <div className="flex items-center gap-2 text-foreground font-medium"><Sparkles className="w-4 h-4 text-primary" /> {t('booking.useYums', language)}</div>
                        <p className="text-xs text-muted-foreground mt-1">1000 Yums = {t('booking.discount', language)} - {t('profile.yums', language)}: {yumsBalance}</p>
                      </div>
                      <Switch checked={useYums} onCheckedChange={setUseYums} />
                    </div>
                  )}
                  {YUMS_ENABLED && canUseYums && (
                    <div className="flex items-center justify-between bg-secondary rounded-xl p-4 border border-border">
                      <div>
                        <div className="flex items-center gap-2 text-foreground font-medium"><Sparkles className="w-4 h-4 text-primary" /> {t('booking.useYums', language)}</div>
                        <p className="text-xs text-muted-foreground mt-1">1000 Yums = chegirma • Balansingiz: {yumsBalance}</p>
                      </div>
                      <Switch checked={useYums} onCheckedChange={setUseYums} />
                    </div>
                  )}
                  <div className="flex gap-3">
                    <Button variant="outline" className="flex-1" onClick={() => setStep(2)}><ChevronLeft className="w-4 h-4" /> {t('common.back', language)}</Button>
                    <Button variant="hero" className="flex-1" onClick={() => setStep(4)}>{t('common.next', language)} <ChevronRight className="w-4 h-4" /></Button>
                  </div>
                </motion.div>
              )}
              {step === 4 && (
                <motion.div key="s4" initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }} className="space-y-4">
                  <div className="bg-secondary rounded-xl p-4 border border-border space-y-3">
                    <Row label={t('venues.venue', language)} value={venue.name} />
                    <Row label={t('booking.date', language)} value={date ? format(date, 'dd.MM.yyyy') : ''} />
                    <Row label={t('search.guests', language)} value={`${guests} ${t('venues.people', language)}`} />
                    <Row label={t('venues.eventType', language)} value={t(eventTypes.find(et => et.value === eventType)?.labelKey ?? 'venues.event.other', language)} />
                    <Row label={t('venues.totalPrice', language)} value={`${totalPrice.toLocaleString('ru-RU')} ${t('venues.currency', language)}`} highlight />
                    {specialRequests && <Row label={t('booking.specialRequests', language)} value={specialRequests} />}
                  </div>
                  <div className="flex gap-3">
                    <Button variant="outline" className="flex-1" onClick={() => setStep(3)}><ChevronLeft className="w-4 h-4" /> {t('common.back', language)}</Button>
                    <Button variant="hero" className="flex-1" onClick={handleConfirm} disabled={bookingLoading}>
                      {bookingLoading ? '...' : t('common.confirm', language)}
                    </Button>
                  </div>
                </motion.div>
              )}
              {step === 5 && bookingResult && (
                <motion.div key="s5" initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} className="text-center space-y-4">
                  <div className="w-16 h-16 rounded-full bg-green-500/20 flex items-center justify-center mx-auto">
                    <CheckCircle2 className="w-8 h-8 text-green-400" />
                  </div>
                  <p className="text-foreground font-medium">{t('booking.confirmationText', language)}</p>
                  <div className="bg-secondary rounded-xl p-4 border border-border space-y-2 text-left text-sm">
                    <Row label={t('booking.bookingId', language)} value={bookingResult.id.slice(0, 8).toUpperCase()} />
                    <Row label={t('venues.venue', language)} value={venue.name} />
                    <Row label={t('booking.date', language)} value={date ? format(date, 'dd.MM.yyyy') : ''} />
                    <Row label={t('search.guests', language)} value={`${guests}`} />
                    <Row label={t('venues.price', language)} value={`${Number(bookingResult.totalPrice).toLocaleString('ru-RU')} ${t('venues.currency', language)}`} highlight />
                    <Row label={t('common.status', language)} value={bookingResult.status} />
                  </div>
                  <Button variant="hero" className="w-full" onClick={resetBooking}>{t('booking.done', language)}</Button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
};

const InfoCard = ({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: string }) => (
  <div className="bg-card rounded-xl p-5 border border-border">
    <div className="flex items-center gap-2 text-muted-foreground mb-1 text-sm"><Icon className="w-4 h-4" />{label}</div>
    <p className="text-foreground font-medium">{value}</p>
  </div>
);

const Row = ({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) => (
  <div className="flex justify-between items-start">
    <span className="text-muted-foreground text-sm">{label}</span>
    <span className={`text-sm font-medium ${highlight ? 'text-primary' : 'text-foreground'}`}>{value}</span>
  </div>
);

export default VenueDetailPage;
