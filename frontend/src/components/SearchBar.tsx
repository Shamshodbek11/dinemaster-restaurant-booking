import { useState } from "react";
import { Search, MapPin, CalendarDays, Users, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { motion } from "framer-motion";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";
import { uzbekistanRegions } from "@/lib/regions";
import { localDateString } from "@/lib/utils";
import { EMPTY_FILTERS, type SearchFilters } from "@/lib/search";

interface SearchBarProps {
  onSearch?: (filters: SearchFilters) => void;
}

const TIMES = Array.from({ length: 28 }, (_, i) => {
  const minutes = 10 * 60 + i * 30; // 10:00 … 23:30
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${minutes % 60 === 0 ? '00' : '30'}`;
});

export const SearchBar = ({ onSearch }: SearchBarProps) => {
  const { language } = useApp();
  const [f, setF] = useState<SearchFilters>(EMPTY_FILTERS);
  const minDate = localDateString();

  const update = (patch: Partial<SearchFilters>, emit = true) => {
    const next = { ...f, ...patch };
    if (next.date && next.date < minDate) next.date = minDate;
    // Vaqt tanlansa-yu sana tanlanmasa — bugun
    if (next.time && !next.date) next.date = minDate;
    setF(next);
    if (emit) onSearch?.(next);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSearch?.(f);
  };

  const cell = "flex items-center gap-2 px-4 py-3 rounded-xl bg-secondary/50 min-w-0";

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.3, duration: 0.6 }}
      className="w-full max-w-5xl mx-auto"
    >
      <div className="bg-gradient-card rounded-2xl p-2 shadow-elevated border border-border">
        <div className="grid grid-cols-2 md:grid-cols-[1.4fr_1fr_1fr_0.8fr_0.8fr_auto] gap-2">
          <div className={`${cell} col-span-2 md:col-span-1`}>
            <Search className="w-4 h-4 text-muted-foreground shrink-0" />
            <input
              type="text"
              placeholder={t('search.query', language)}
              value={f.query}
              onChange={(e) => update({ query: e.target.value })}
              aria-label={t('search.query', language)}
              className="w-full bg-transparent text-sm text-foreground placeholder:text-muted-foreground outline-none"
            />
          </div>

          <div className={`${cell} col-span-2 md:col-span-1`}>
            <MapPin className="w-4 h-4 text-muted-foreground shrink-0" />
            <select
              value={f.region}
              onChange={(e) => update({ region: e.target.value })}
              aria-label={t('search.location', language)}
              className="w-full bg-transparent text-sm text-foreground outline-none appearance-none"
            >
              {uzbekistanRegions.map(region => (
                <option key={region.value || 'all'} value={region.value} className="bg-card text-foreground">
                  {t(region.labelKey, language)}
                </option>
              ))}
            </select>
          </div>

          <div className={cell}>
            <CalendarDays className="w-4 h-4 text-muted-foreground shrink-0" />
            <input
              type="date"
              value={f.date}
              min={minDate}
              onChange={(e) => update({ date: e.target.value })}
              aria-label={t('search.date', language)}
              className="w-full bg-transparent text-sm text-foreground outline-none dark:[color-scheme:dark]"
            />
          </div>

          <div className={cell}>
            <Clock className="w-4 h-4 text-muted-foreground shrink-0" />
            <select
              value={f.time}
              onChange={(e) => update({ time: e.target.value })}
              aria-label={t('search.time', language)}
              className="w-full bg-transparent text-sm text-foreground outline-none appearance-none"
            >
              <option value="" className="bg-card text-foreground">{t('search.anyTime', language)}</option>
              {TIMES.map(tm => <option key={tm} value={tm} className="bg-card text-foreground">{tm}</option>)}
            </select>
          </div>

          <div className={cell}>
            <Users className="w-4 h-4 text-muted-foreground shrink-0" />
            <select
              value={f.guests}
              onChange={(e) => update({ guests: Number(e.target.value) })}
              aria-label={t('booking.guests', language)}
              className="w-full bg-transparent text-sm text-foreground outline-none appearance-none"
            >
              {Array.from({ length: 12 }, (_, i) => i + 1).map((n) => (
                <option key={n} value={n} className="bg-card text-foreground">
                  {n} {t('search.guest', language)}
                </option>
              ))}
            </select>
          </div>

          <Button variant="hero" size="lg" type="submit" className="md:rounded-xl" aria-label={t('search.button', language)}>
            <Search className="w-4 h-4" />
            <span className="md:hidden">{t('search.button', language)}</span>
          </Button>
        </div>
      </div>
      {f.date && f.time && (
        <p className="text-xs text-muted-foreground mt-2 text-center">{t('search.availabilityHint', language)}</p>
      )}
    </motion.form>
  );
};
