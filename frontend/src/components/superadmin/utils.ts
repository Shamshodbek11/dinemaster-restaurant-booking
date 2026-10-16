import { useEffect, useState } from 'react';

export const PAGE_SIZE = 25;

export const STATUS_LABELS: Record<string, string> = {
  confirmed: 'Tasdiqlangan', pending: 'Kutilmoqda', completed: 'Yakunlangan', cancelled: 'Bekor qilingan',
  no_show: 'Kelmadi', approved: 'Tasdiqlangan', rejected: 'Rad etilgan',
};

/** "2026-10-16" (yoki ISO vaqt) → "16.10.2026" */
export const dmy = (d?: string | null) => (d ? d.slice(0, 10).split('-').reverse().join('.') : '');

export const money = (v: number) => `${Math.round(v).toLocaleString('ru-RU')} so'm`;

export const dateTime = (iso: string) => new Date(iso).toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'short' });

/** Qidiruv maydoni uchun kechikish (har harfda so'rov yubormaslik) */
export const useDebounced = <T,>(value: T, ms = 350) => {
  const [v, setV] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setV(value), ms);
    return () => clearTimeout(id);
  }, [value, ms]);
  return v;
};
