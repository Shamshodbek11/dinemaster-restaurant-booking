import { useEffect, useState } from 'react';
import { restaurantsApi } from '@/lib/api';
import { matchesRegion } from '@/lib/regions';
import type { Restaurant } from '@/lib/restaurantApi';

export interface SearchFilters {
  query: string;
  region: string;
  /** yyyy-MM-dd yoki '' */
  date: string;
  /** HH:mm yoki '' (istalgan vaqt) */
  time: string;
  guests: number;
}

export const EMPTY_FILTERS: SearchFilters = { query: '', region: '', date: '', time: '', guests: 2 };

/**
 * Sana va vaqt tanlangan bo'lsa — serverdan shu paytda `guests` kishiga joyi bor restoranlar
 * (ish vaqti, maxsus kunlar, sig'im va stollar hisobga olinadi). Aks holda null — filtr yo'q.
 */
export function useAvailableIds(f: SearchFilters): { ids: Set<string> | null; loading: boolean } {
  const [ids, setIds] = useState<Set<string> | null>(null);
  const [loading, setLoading] = useState(false);
  const active = Boolean(f.date && f.time);

  useEffect(() => {
    if (!active) { setIds(null); return; }
    let cancelled = false;
    setLoading(true);
    restaurantsApi.list({ date: f.date, time: f.time, guests: f.guests })
      .then(list => { if (!cancelled) setIds(new Set(list.map(r => r.id))); })
      .catch(() => { if (!cancelled) setIds(null); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [active, f.date, f.time, f.guests]);

  return { ids, loading };
}

/** Matn, hudud va (bo'lsa) bo'sh joy filtri */
export function applyFilters(list: Restaurant[], f: SearchFilters, available: Set<string> | null): Restaurant[] {
  const q = f.query.trim().toLowerCase();
  return list.filter(r =>
    (!q || r.name.toLowerCase().includes(q) || r.address.toLowerCase().includes(q) || r.cuisine_type.toLowerCase().includes(q)) &&
    matchesRegion(f.region, [r.city, r.address]) &&
    (!available || available.has(r.id)));
}

/** Ikki nuqta orasidagi masofa (km) */
export function distanceKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}
