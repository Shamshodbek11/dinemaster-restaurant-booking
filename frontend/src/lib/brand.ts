/**
 * Dastur nomi — bitta joyda. Nomni o'zgartirish uchun:
 *  - frontend: .env faylida VITE_BRAND_NAME=YangiNom (yoki shu yerdagi standart qiymat)
 *  - backend: appsettings.json → "Branding": { "Name": "YangiNom" } (email, PDF, Telegram)
 *  - index.html'dagi <title> build vaqtida shu qiymat bilan almashtiriladi (vite.config.ts)
 */
export const BRAND: string = (import.meta.env.VITE_BRAND_NAME as string | undefined)?.trim() || 'DineMaster';

/** Fayl nomlari uchun: "Dine Master" → "dine-master" */
export const BRAND_SLUG = BRAND.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'app';

/** Brend nomini ikki qismga bo'ladi (logotipda ikkinchi qism rangli): "DineMaster" → ["Dine", "Master"] */
export function brandParts(): [string, string] {
  const m = BRAND.match(/^([A-Z]?[a-z0-9]+)([A-Z].*)$/);
  if (m) return [m[1], m[2]];
  const space = BRAND.indexOf(' ');
  if (space > 0) return [BRAND.slice(0, space), BRAND.slice(space)];
  return [BRAND, ''];
}
