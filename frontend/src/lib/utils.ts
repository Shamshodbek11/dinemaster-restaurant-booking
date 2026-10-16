import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

const pad = (n: number) => String(n).padStart(2, "0");

/**
 * Mahalliy sana "yyyy-MM-dd" ko'rinishida.
 * `toISOString()` UTC sanani beradi — Toshkentda 00:00–05:00 orasida u kechagi kun bo'lib qoladi.
 */
export function localDateString(d: Date = new Date()): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Mahalliy sana+vaqt "yyyy-MM-ddTHH:mm:ss" — backenddan kelgan date+time bilan solishtirish uchun. */
export function localDateTimeString(d: Date = new Date()): string {
  return `${localDateString(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}
