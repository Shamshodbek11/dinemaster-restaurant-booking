import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { useApp } from '@/contexts/AppContext';
import { t, tf, hasTranslation } from '@/lib/i18n';
import type { Restaurant } from '@/lib/restaurantApi';
import {
  reservationsApi,
  restaurantsApi,
  tablesApi,
  menuApi,
  ApiError,
  type ProcessBookingResult,
  type TableAvailability,
  type MenuCategory,
  type AvailabilityResult,
  type BookingQuote,
} from '@/lib/api';
import { DepositPayButtons } from '@/components/DepositPayButtons';
import { toast } from 'sonner';
import { format } from 'date-fns';
import {
  CalendarDays,
  Clock,
  Users,
  CheckCircle2,
  Sparkles,
  ChevronRight,
  ChevronLeft,
  ChevronDown,
  Table2,
  UtensilsCrossed,
  CreditCard,
  MailWarning,
  Loader2,
  ShieldCheck,
  Percent,
} from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { cn } from '@/lib/utils';

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  restaurant: Restaurant;
  depositPerGuest?: number;
  depositRequired?: boolean;
}

/** Backend bilan bir xil qoidalar (BookingService) */
const YUMS_FOR_DISCOUNT = 1000;
const YUMS_DISCOUNT_PERCENT = 20;
const MAX_TOTAL_DISCOUNT = 50;

/** Stol joylashuvi: tarjimasi bo'lmasa (restoran o'zi yozgan) — o'zi */
const locationLabel = (loc: string, lang: Parameters<typeof t>[1]) =>
  hasTranslation(`table.location.${loc}`, lang) ? t(`table.location.${loc}`, lang) : loc;

const money = (n: number) => `${Math.round(n).toLocaleString('uz-UZ').replace(/,/g, ' ')} so'm`;

/**
 * Bron — 2 qadam:
 *   1. Sana, vaqt, mehmonlar (+ ixtiyoriy: stol, oldindan buyurtma)
 *   2. Tekshirish: istaklar, yums, chegirma, depozit va bekor qilish shartlari
 * So'ng — natija (depozit bo'lsa, shu yerning o'zida to'lash).
 */
export const ProBookingModal = ({
  open,
  onOpenChange,
  restaurant,
  depositPerGuest = 0,
  depositRequired = false,
}: Props) => {
  const { language, user, profile } = useApp();
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2 | 3>(1);

  // Vaqtlar serverdan: ish vaqti (maxsus kunlar ham), kunlik va soatlik sig'im hisobga olingan
  const [availability, setAvailability] = useState<AvailabilityResult | null>(null);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [quote, setQuote] = useState<BookingQuote | null>(null);

  const [date, setDate] = useState<Date>();
  const [time, setTime] = useState('');
  const [guests, setGuests] = useState(2);

  // Ixtiyoriy bo'limlar
  const [showTables, setShowTables] = useState(false);
  const [tables, setTables] = useState<TableAvailability[]>([]);
  const [tableId, setTableId] = useState<string | null>(null);
  const [loadingTables, setLoadingTables] = useState(false);
  const [showMenu, setShowMenu] = useState(false);
  const [menu, setMenu] = useState<MenuCategory[] | null>(null);
  const [quantities, setQuantities] = useState<Record<string, number>>({});

  const [specialRequests, setSpecialRequests] = useState('');
  const [useYums, setUseYums] = useState(false);

  const [loading, setLoading] = useState(false);
  const [bookingResult, setBookingResult] = useState<ProcessBookingResult | null>(null);

  const yumsBalance = profile?.yumsBalance ?? 0;
  const canUseYums = yumsBalance >= YUMS_FOR_DISCOUNT;

  const preOrderTotal = useMemo(() => {
    let sum = 0;
    for (const c of menu ?? []) for (const it of c.items) sum += (quantities[it.id] ?? 0) * it.price;
    return sum;
  }, [menu, quantities]);

  // Depozit serverdan (restoran talabi yoki kelmaslik tarixi); kelmaguncha — restoran sozlamasi
  const depositAmount = quote ? quote.depositAmount : depositRequired ? depositPerGuest * guests : 0;
  const freeCancelHours = quote?.freeCancellationHours ?? 24;
  const emailNotConfirmed = user?.emailConfirmed === false || quote?.emailConfirmed === false;
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const selectedTable = tables.find((x) => x.tableId === tableId);
  const slotDiscount = availability?.slots.find(s => s.time === time)?.discount ?? 0;
  const yumsDiscount = useYums && canUseYums ? YUMS_DISCOUNT_PERCENT : 0;
  const totalDiscount = Math.min(MAX_TOTAL_DISCOUNT, slotDiscount + yumsDiscount);
  const preOrderDiscount = Math.round((preOrderTotal * totalDiscount) / 100);
  const hasDiscountSlots = availability?.slots.some(s => (s.discount ?? 0) > 0) ?? false;

  // Sana tanlanganda — shu kun uchun bo'sh vaqtlar
  useEffect(() => {
    if (!open || !date) return;
    let cancelled = false;
    setLoadingSlots(true);
    setTime('');
    setTableId(null);
    restaurantsApi
      .availability(restaurant.id, format(date, 'yyyy-MM-dd'))
      .then(res => { if (!cancelled) setAvailability(res); })
      .catch(() => { if (!cancelled) setAvailability(null); })
      .finally(() => { if (!cancelled) setLoadingSlots(false); });
    return () => { cancelled = true; };
  }, [open, date, restaurant.id]);

  // Depozit, email holati va bekor qilish shartlari (mehmonlar soniga bog'liq)
  useEffect(() => {
    if (!open || !user) return;
    reservationsApi.quote(restaurant.id, guests).then(setQuote).catch(() => setQuote(null));
  }, [open, user, guests, restaurant.id]);

  // Stollar — bo'lim ochilganda va vaqt tanlanganda
  useEffect(() => {
    if (!showTables || !date || !time) return;
    let cancelled = false;
    setLoadingTables(true);
    tablesApi
      .availability(restaurant.id, { date: format(date, 'yyyy-MM-dd'), time: time + ':00', guests })
      .then(list => {
        if (cancelled) return;
        setTables(list);
        setTableId(prev => (list.some(x => x.tableId === prev && x.isAvailable) ? prev : null));
      })
      .catch(() => { if (!cancelled) setTables([]); })
      .finally(() => { if (!cancelled) setLoadingTables(false); });
    return () => { cancelled = true; };
  }, [showTables, date, time, guests, restaurant.id]);

  // Menyu — bo'lim birinchi ochilganda
  useEffect(() => {
    if (showMenu && menu === null) {
      menuApi.public(restaurant.id).then(setMenu).catch(() => setMenu([]));
    }
  }, [showMenu, menu, restaurant.id]);

  const handleConfirm = async () => {
    if (!user || !date || !time) return;
    setLoading(true);
    try {
      const items = Object.entries(quantities)
        .filter(([, q]) => q > 0)
        .map(([menuItemId, quantity]) => ({ menuItemId, quantity }));
      const data = await reservationsApi.create({
        restaurantId: restaurant.id,
        date: format(date, 'yyyy-MM-dd'),
        time: time + ':00',
        guestsCount: guests,
        specialRequests: specialRequests.trim() || undefined,
        useYums: useYums && canUseYums,
        tableId: tableId || undefined,
        items: items.length ? items : undefined,
      });
      setBookingResult(data);
      setStep(3);
      toast.success(t('booking.confirmed', language));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('booking.error', language));
    } finally {
      setLoading(false);
    }
  };

  const reset = () => {
    setStep(1);
    setDate(undefined);
    setTime('');
    setGuests(2);
    setShowTables(false);
    setTables([]);
    setTableId(null);
    setShowMenu(false);
    setQuantities({});
    setSpecialRequests('');
    setUseYums(false);
    setBookingResult(null);
    setAvailability(null);
    setQuote(null);
    onOpenChange(false);
  };

  const adjustQty = (id: string, delta: number) =>
    setQuantities(prev => ({ ...prev, [id]: Math.max(0, (prev[id] ?? 0) + delta) }));

  const dateLabel = date ? format(date, 'dd.MM.yyyy') : '';
  const tableLabel = selectedTable ? `${selectedTable.label} (${tf('booking.guestsShort', language, { n: selectedTable.capacity })})` : t('booking.tableNotSet', language);
  const itemsCount = Object.values(quantities).reduce((a, b) => a + b, 0);

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) reset(); }}>
      <DialogContent className="sm:max-w-lg bg-card border-border p-0 overflow-hidden max-h-[92vh] overflow-y-auto">
        {step < 3 && (
          <div className="flex gap-1 px-6 pt-6">
            {[1, 2].map((s) => (
              <div key={s} className={cn('h-1 flex-1 rounded-full transition-all', s <= step ? 'bg-primary' : 'bg-secondary')} />
            ))}
          </div>
        )}

        <div className="px-4 sm:px-6 pb-6 pt-2">
          <DialogHeader className="mb-4">
            <DialogTitle className="font-display text-xl text-foreground">
              {step === 1 && t('booking.step1', language)}
              {step === 2 && t('booking.reviewTitle', language)}
              {step === 3 && t('booking.confirmed', language)}
            </DialogTitle>
          </DialogHeader>

          <AnimatePresence mode="wait">
            {step === 1 && (
              <motion.div key="s1" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="space-y-5">
                {emailNotConfirmed && (
                  <div className="bg-primary/10 border border-primary/30 rounded-xl p-3 text-sm flex items-start gap-2">
                    <MailWarning className="w-4 h-4 shrink-0 mt-0.5 text-primary" />
                    <div className="space-y-2">
                      <p className="text-foreground">{t('verify.requiredForBooking', language)}</p>
                      <Button size="sm" variant="hero" onClick={() => navigate(`/verify-email?next=${encodeURIComponent(window.location.pathname)}`)}>
                        {t('verify.title', language)}
                      </Button>
                    </div>
                  </div>
                )}

                <div>
                  <Label className="text-sm text-muted-foreground flex items-center gap-1 mb-2">
                    <CalendarDays className="w-3.5 h-3.5" /> {t('booking.selectDate', language)}
                  </Label>
                  <Calendar
                    mode="single"
                    selected={date}
                    onSelect={setDate}
                    disabled={(d) => d < today}
                    className="p-3 pointer-events-auto rounded-xl border border-border w-fit mx-auto sm:mx-0"
                  />
                </div>

                <div>
                  <Label className="text-sm text-muted-foreground flex items-center gap-1 mb-2">
                    <Users className="w-3.5 h-3.5" /> {t('booking.guests', language)}
                  </Label>
                  <div className="flex items-center gap-3">
                    <Button variant="outline" size="icon" aria-label="-" onClick={() => setGuests(Math.max(1, guests - 1))}>-</Button>
                    <span className="text-foreground font-bold text-lg w-8 text-center">{guests}</span>
                    <Button variant="outline" size="icon" aria-label="+" onClick={() => setGuests(Math.min(50, guests + 1))}>+</Button>
                  </div>
                </div>

                <div>
                  <Label className="text-sm text-muted-foreground flex items-center gap-1 mb-2">
                    <Clock className="w-3.5 h-3.5" /> {t('booking.selectTime', language)}
                  </Label>
                  {!date && <p className="text-sm text-muted-foreground">{t('booking.pickDateFirst', language)}</p>}
                  {date && loadingSlots && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
                  {date && !loadingSlots && availability && (
                    <>
                      <p className="text-xs text-muted-foreground mb-2">
                        {t('booking.openingHours', language)}: {availability.openingHours}
                        {hasDiscountSlots && <> · <span className="text-primary">{t('booking.discountSlotsHint', language)}</span></>}
                      </p>
                      {availability.slots.length === 0 ? (
                        <p className="text-sm text-muted-foreground">{t('booking.closedThatDay', language)}</p>
                      ) : (
                        <div className="grid grid-cols-4 gap-2 max-h-52 overflow-y-auto pr-1">
                          {availability.slots.map((slot) => (
                            <button
                              key={slot.time}
                              disabled={!slot.available}
                              title={slot.reason ? t(`booking.slot.${slot.reason}`, language) : undefined}
                              onClick={() => setTime(slot.time)}
                              className={cn(
                                'relative py-2 rounded-lg text-sm font-medium transition-all border',
                                !slot.available && 'opacity-40 line-through cursor-not-allowed',
                                time === slot.time ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary text-foreground border-border hover:border-primary/50'
                              )}
                            >
                              {slot.time}
                              {slot.available && (slot.discount ?? 0) > 0 && (
                                <span className={cn(
                                  'absolute -top-2 -right-1 text-[10px] leading-none px-1 py-0.5 rounded font-bold',
                                  time === slot.time ? 'bg-background text-primary' : 'bg-primary text-primary-foreground'
                                )}>
                                  -{slot.discount}%
                                </span>
                              )}
                            </button>
                          ))}
                        </div>
                      )}
                    </>
                  )}
                </div>

                {/* Ixtiyoriy: stol */}
                <OptionalSection
                  icon={Table2}
                  title={t('booking.tableOptional', language)}
                  summary={selectedTable ? selectedTable.label : undefined}
                  open={showTables}
                  onToggle={() => setShowTables(v => !v)}
                >
                  {!time ? (
                    <p className="text-sm text-muted-foreground">{t('booking.pickTimeForTables', language)}</p>
                  ) : loadingTables ? (
                    <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />
                  ) : (
                    <div className="space-y-2">
                      {tables.length === 0 && <p className="text-sm text-muted-foreground">{t('booking.noTables', language)}</p>}
                      <div className="grid grid-cols-2 gap-2">
                        <button
                          onClick={() => setTableId(null)}
                          className={cn('py-2.5 px-3 rounded-xl text-sm font-medium border text-left',
                            tableId === null ? 'border-primary bg-primary/10' : 'border-border bg-secondary')}
                        >
                          {t('booking.tableAny', language)}
                        </button>
                        {tables.map((tb) => (
                          <button
                            key={tb.tableId}
                            disabled={!tb.isAvailable}
                            onClick={() => setTableId(tb.tableId)}
                            className={cn('py-2.5 px-3 rounded-xl text-sm font-medium border text-left',
                              !tb.isAvailable && 'opacity-50 cursor-not-allowed',
                              tableId === tb.tableId ? 'border-primary bg-primary/10' : 'border-border bg-secondary')}
                          >
                            <div className="font-bold">{tb.label}</div>
                            <div className="text-xs text-muted-foreground">
                              {tf('booking.guestsShort', language, { n: tb.capacity })} · {locationLabel(tb.location, language)}
                              {!tb.isAvailable && ` · ${t('booking.tableBusy', language)}`}
                            </div>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </OptionalSection>

                {/* Ixtiyoriy: oldindan buyurtma */}
                <OptionalSection
                  icon={UtensilsCrossed}
                  title={t('booking.preorderOptional', language)}
                  summary={preOrderTotal > 0 ? `${itemsCount} · ${money(preOrderTotal)}` : undefined}
                  open={showMenu}
                  onToggle={() => setShowMenu(v => !v)}
                >
                  {menu === null ? (
                    <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />
                  ) : menu.length === 0 ? (
                    <p className="text-muted-foreground text-sm">{t('booking.menuEmpty', language)}</p>
                  ) : (
                    <div className="space-y-4 max-h-64 overflow-y-auto pr-1">
                      {menu.map((cat) => (
                        <div key={cat.id}>
                          <h4 className="font-bold text-foreground text-sm mb-2">{cat.name}</h4>
                          <div className="space-y-2">
                            {cat.items.map((it) => (
                              <div key={it.id} className="flex items-center gap-3 bg-secondary rounded-lg p-2.5">
                                <div className="flex-1 min-w-0">
                                  <div className="text-sm font-medium text-foreground truncate">{it.name}</div>
                                  <div className="text-xs text-muted-foreground">{money(Number(it.price))}</div>
                                </div>
                                <div className="flex items-center gap-2">
                                  <Button variant="outline" size="icon" className="h-7 w-7" onClick={() => adjustQty(it.id, -1)}>-</Button>
                                  <span className="w-5 text-center text-sm">{quantities[it.id] ?? 0}</span>
                                  <Button variant="outline" size="icon" className="h-7 w-7" onClick={() => adjustQty(it.id, 1)}>+</Button>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </OptionalSection>

                <Button variant="hero" className="w-full" disabled={!date || !time || emailNotConfirmed} onClick={() => setStep(2)}>
                  {t('booking.next', language)} <ChevronRight className="w-4 h-4" />
                </Button>
              </motion.div>
            )}

            {step === 2 && (
              <motion.div key="s2" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="space-y-4">
                <div className="bg-secondary rounded-xl p-4 border border-border space-y-2 text-sm">
                  <Row label={t('booking.restaurant', language)} value={restaurant.name} />
                  <Row label={t('booking.date', language)} value={`${dateLabel}, ${time}`} />
                  <Row label={t('booking.guests', language)} value={String(guests)} />
                  <Row label={t('booking.table', language)} value={tableLabel} />
                  {preOrderTotal > 0 && <Row label={t('booking.preorderTotal', language)} value={money(preOrderTotal)} />}
                  {totalDiscount > 0 && (
                    <Row
                      label={t('booking.discount', language)}
                      value={`${totalDiscount}%${preOrderDiscount > 0 ? ` (−${money(preOrderDiscount)})` : ''}`}
                      highlight
                    />
                  )}
                  {depositAmount > 0 && <Row label={t('booking.deposit', language)} value={money(depositAmount)} highlight />}
                </div>

                {slotDiscount > 0 && (
                  <p className="text-xs text-primary flex items-center gap-1">
                    <Percent className="w-3.5 h-3.5" /> {tf('booking.slotDiscountNote', language, { n: slotDiscount })}
                  </p>
                )}

                {canUseYums && (
                  <div className="flex items-center justify-between bg-secondary rounded-xl p-4 border border-border gap-3">
                    <div>
                      <div className="flex items-center gap-2 text-foreground font-medium">
                        <Sparkles className="w-4 h-4 text-primary" /> {t('booking.useYums', language)}
                      </div>
                      <p className="text-xs text-muted-foreground mt-1">
                        {tf('booking.yumsHint', language, { y: YUMS_FOR_DISCOUNT, p: YUMS_DISCOUNT_PERCENT, b: yumsBalance })}
                      </p>
                    </div>
                    <Switch checked={useYums} onCheckedChange={setUseYums} />
                  </div>
                )}

                <div>
                  <Label className="text-sm text-muted-foreground mb-2 block">{t('booking.specialRequests', language)}</Label>
                  <Textarea
                    value={specialRequests}
                    onChange={(e) => setSpecialRequests(e.target.value)}
                    rows={2}
                    maxLength={1000}
                    placeholder={t('booking.specialRequestsPlaceholder', language)}
                    className="bg-secondary border-border"
                  />
                </div>

                {depositAmount > 0 && (
                  <div className="bg-primary/10 border border-primary/30 rounded-xl p-3 text-sm text-foreground flex items-start gap-2">
                    <CreditCard className="w-4 h-4 shrink-0 mt-0.5 text-primary" />
                    <div>
                      {quote?.depositReason === 'no_show'
                        ? tf('booking.depositNoShow', language, { n: quote.noShowCount })
                        : t('booking.depositRestaurant', language)}
                    </div>
                  </div>
                )}

                <div className="rounded-xl border border-border p-3 text-xs text-muted-foreground flex items-start gap-2">
                  <ShieldCheck className="w-4 h-4 shrink-0 mt-0.5 text-success" />
                  <div className="space-y-1">
                    <p className="font-medium text-foreground text-sm">{t('booking.policyTitle', language)}</p>
                    {depositAmount > 0 ? (
                      <>
                        <p>{tf('booking.policyFree', language, { h: freeCancelHours })}</p>
                        <p>{t('booking.policyLate', language)}</p>
                      </>
                    ) : (
                      <p>{t('booking.policyNoDeposit', language)}</p>
                    )}
                    <p>{t('booking.policyModify', language)}</p>
                  </div>
                </div>

                <div className="flex gap-3">
                  <Button variant="outline" className="flex-1" onClick={() => setStep(1)}>
                    <ChevronLeft className="w-4 h-4" /> {t('booking.back', language)}
                  </Button>
                  <Button variant="hero" className="flex-1" onClick={handleConfirm} disabled={loading}>
                    {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : t('booking.confirm', language)}
                  </Button>
                </div>
              </motion.div>
            )}

            {step === 3 && bookingResult && (
              <motion.div key="s3" initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} className="text-center space-y-4">
                <div className="w-16 h-16 rounded-full bg-success/20 flex items-center justify-center mx-auto">
                  <CheckCircle2 className="w-8 h-8 text-success" />
                </div>
                <p className="text-foreground font-medium">{t('booking.confirmationText', language)}</p>
                <div className="bg-secondary rounded-xl p-4 border border-border space-y-2 text-left text-sm">
                  <Row label={t('booking.bookingId', language)} value={bookingResult.reservationId.slice(0, 8).toUpperCase()} />
                  <Row label={t('booking.restaurant', language)} value={restaurant.name} />
                  <Row label={t('booking.date', language)} value={`${dateLabel}, ${time}`} />
                  <Row label={t('booking.guests', language)} value={String(guests)} />
                  <Row label={t('booking.table', language)} value={tableLabel} />
                  {bookingResult.preOrderTotal > 0 && <Row label={t('booking.preorderTotal', language)} value={money(Number(bookingResult.preOrderTotal))} />}
                  {(bookingResult.totalDiscount ?? 0) > 0 && (
                    <Row label={t('booking.discount', language)} value={`${bookingResult.totalDiscount}%`} highlight />
                  )}
                  {bookingResult.depositDue > 0 && <Row label={t('booking.deposit', language)} value={money(Number(bookingResult.depositDue))} />}
                  <Row label="Yums" value={tf('booking.yumsAfterVisit', language, { n: bookingResult.yumsEarned })} highlight />
                </div>
                {bookingResult.depositDue > 0 && (
                  <div className="bg-secondary rounded-xl p-4 border border-border text-left">
                    <DepositPayButtons reservationId={bookingResult.reservationId} amount={Number(bookingResult.depositDue)} onPaid={reset} />
                  </div>
                )}
                <p className="text-xs text-muted-foreground">{t('booking.manageInProfile', language)}</p>
                <Button variant={bookingResult.depositDue > 0 ? 'outline' : 'hero'} className="w-full" onClick={reset}>
                  {bookingResult.depositDue > 0 ? t('payment.later', language) : t('booking.done', language)}
                </Button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </DialogContent>
    </Dialog>
  );
};

const OptionalSection = ({
  icon: Icon, title, summary, open, onToggle, children,
}: {
  icon: typeof Table2; title: string; summary?: string; open: boolean; onToggle: () => void; children: React.ReactNode;
}) => (
  <div className="rounded-xl border border-border">
    <button type="button" onClick={onToggle} aria-expanded={open}
      className="w-full flex items-center gap-2 px-3 py-2.5 text-sm text-foreground hover:bg-secondary/60 rounded-xl">
      <Icon className="w-4 h-4 text-muted-foreground" />
      <span className="flex-1 text-left font-medium">{title}</span>
      {summary && <span className="text-xs text-primary truncate max-w-[40%]">{summary}</span>}
      <ChevronDown className={cn('w-4 h-4 text-muted-foreground transition-transform', open && 'rotate-180')} />
    </button>
    {open && <div className="px-3 pb-3 pt-1">{children}</div>}
  </div>
);

const Row = ({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) => (
  <div className="flex justify-between items-start gap-2">
    <span className="text-muted-foreground text-sm">{label}</span>
    <span className={cn('text-sm font-medium text-right', highlight ? 'text-primary' : 'text-foreground')}>{value}</span>
  </div>
);
