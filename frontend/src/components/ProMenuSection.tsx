import { useMemo, useState } from 'react';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import type { MenuCategory } from '@/lib/api';
import { Flame, Leaf, UtensilsCrossed } from 'lucide-react';
import { motion } from 'framer-motion';

interface Props {
  categories: MenuCategory[];
}

/**
 * Real-API menu display for the public restaurant detail page.
 * Shows categories as tabs, items with images, price, popular badge,
 * and handles the empty state gracefully.
 */
export const ProMenuSection = ({ categories }: Props) => {
  const { language } = useApp();
  const [activeIndex, setActiveIndex] = useState(0);

  // Only show categories that have at least one available item
  const visibleCategories = useMemo(
    () =>
      categories
        .filter(c => (c.items ?? []).some(i => i.isAvailable))
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [categories],
  );

  if (visibleCategories.length === 0) {
    return (
      <div className="bg-card rounded-2xl border border-border p-12 text-center">
        <UtensilsCrossed className="w-12 h-12 text-muted-foreground mx-auto mb-3 opacity-50" />
        <p className="text-muted-foreground">
          {t('menu.empty', language)}
        </p>
      </div>
    );
  }

  const active = visibleCategories[Math.min(activeIndex, visibleCategories.length - 1)];
  const items = [...(active.items ?? [])]
    .filter(i => i.isAvailable)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  return (
    <div className="space-y-6">
      {/* Category tabs */}
      <div className="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
        {visibleCategories.map((cat, i) => (
          <button
            key={cat.id}
            onClick={() => setActiveIndex(i)}
            className={`whitespace-nowrap px-4 py-2 rounded-full text-sm font-medium transition-all border ${
              activeIndex === i
                ? 'bg-primary text-primary-foreground border-primary'
                : 'bg-card text-muted-foreground border-border hover:border-primary/50'
            }`}
          >
            {cat.name}
          </button>
        ))}
      </div>

      {/* Items grid */}
      <motion.div
        key={active.id}
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        className="grid grid-cols-1 md:grid-cols-2 gap-4"
      >
        {items.map(item => (
          <div
            key={item.id}
            className="bg-card border border-border rounded-2xl p-4 flex gap-4 hover:border-primary/40 transition-colors"
          >
            <div className="w-24 h-24 rounded-xl shrink-0 bg-secondary overflow-hidden flex items-center justify-center">
              {item.imageUrl ? (
                <img
                  src={item.imageUrl}
                  alt={item.name}
                  className="w-full h-full object-cover"
                  loading="lazy"
                  onError={(e) => {
                    // Buzilgan rasmda placeholder ko'rsatish
                    const img = e.currentTarget;
                    img.style.display = 'none';
                    const parent = img.parentElement;
                    if (parent) {
                      parent.innerHTML = '<div class="w-full h-full flex items-center justify-center text-muted-foreground"><svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 11h3a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H4a1 1 0 0 0-1 1v7zm0 0v6a2 2 0 0 0 2 2h2"/><path d="M21 11h-3a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h2a1 1 0 0 1 1 1v7zm0 0v6a2 2 0 0 1-2 2h-2"/><path d="M11 17l-2-3"/><path d="m13 17 2-3"/></svg></div>';
                    }
                  }}
                />
              ) : (
                <UtensilsCrossed className="w-8 h-8 text-muted-foreground/50" />
              )}
            </div>
            <div className="flex-1 min-w-0 flex flex-col">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <h4 className="text-foreground font-medium truncate">{item.name}</h4>
                    {item.isPopular && (
                      <span className="flex items-center gap-1 text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-primary/15 text-primary">
                        <Flame className="w-3 h-3" /> Top
                      </span>
                    )}
                  </div>
                  {item.description && (
                    <p className="text-muted-foreground text-sm mt-1 line-clamp-2">
                      {item.description}
                    </p>
                  )}
                </div>
                <span className="text-foreground font-bold shrink-0">
                  {item.price.toLocaleString('uz-UZ')} so'm
                </span>
              </div>
            </div>
          </div>
        ))}
      </motion.div>
    </div>
  );
};
