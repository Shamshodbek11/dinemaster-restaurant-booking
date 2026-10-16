import { restaurantsApi, type Restaurant as ApiRestaurant } from '@/lib/api';

import restaurant1 from '@/assets/restaurant-1.jpg';
import restaurant2 from '@/assets/restaurant-2.jpg';
import restaurant3 from '@/assets/restaurant-3.jpg';
import restaurant4 from '@/assets/restaurant-4.jpg';
import restaurant5 from '@/assets/restaurant-5.jpg';
import restaurant6 from '@/assets/restaurant-6.jpg';

const fallbackImages = [restaurant1, restaurant2, restaurant3, restaurant4, restaurant5, restaurant6];

/**
 * Frontend UI'da ishlatiladigan Restaurant shape — API'dagi Restaurant'ga
 * qo'shimcha rasm (image) maydoni va formatlangan nextAvailable qo'shiladi.
 */
export interface Restaurant {
  id: string;
  name: string;
  description: string;
  cuisine_type: string;
  price_range: string;
  address: string;
  rating: number;
  reviewCount: number;
  discount?: number;
  nextAvailable?: string;
  image: string;
  images: string[];
  phone?: string;
  city?: string;
  opening_hours?: string;
  totalSlots: number;
  discount_time_start?: string | null;
  discount_time_end?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  amenities: string[];
  policies: string[];
  depositRequired: boolean;
  depositPerGuest: number;
}

export function mapApiToRestaurant(api: ApiRestaurant, index: number = 0): Restaurant {
  const rawImages = (api.imagesUrl ?? []).filter(u => typeof u === 'string' && u.length > 0);
  const fallback = fallbackImages[index % fallbackImages.length];
  const images = rawImages.length > 0 ? rawImages : [fallback];
  const img = images[0];

  // Chegirma vaqti oralig'i ("12:00–16:00"); matni kartochkada tarjima qilinadi
  const nextAvailable = api.discount && api.discountTimeStart && api.discountTimeEnd
    ? `${api.discountTimeStart.slice(0, 5)}–${api.discountTimeEnd.slice(0, 5)}`
    : undefined;

  return {
    id: api.id,
    name: api.name,
    description: api.description,
    cuisine_type: api.cuisineType,
    price_range: api.priceRange,
    address: api.address,
    rating: Number(api.rating),
    reviewCount: api.reviewCount,
    discount: api.discount ?? undefined,
    nextAvailable,
    image: img,
    images,
    phone: api.phone ?? undefined,
    city: api.city,
    opening_hours: api.openingHours,
    totalSlots: api.totalSlots,
    discount_time_start: api.discountTimeStart ?? null,
    discount_time_end: api.discountTimeEnd ?? null,
    latitude: api.latitude ?? null,
    longitude: api.longitude ?? null,
    foodRating: Number(api.foodRating ?? 0),
    serviceRating: Number(api.serviceRating ?? 0),
    interiorRating: Number(api.interiorRating ?? 0),
    amenities: api.amenities ?? [],
    policies: api.policies ?? [],
    depositRequired: Boolean(api.depositRequired),
    depositPerGuest: Number(api.depositPerGuest ?? 0),
  };
}

export async function fetchRestaurants(): Promise<Restaurant[]> {
  try {
    const list = await restaurantsApi.list();
    // Backend allaqachon is_approved = true va is_suspended = false filtrini ishlatadi
    // Review soni bo'yicha saralash — kerak bo'lsa backend ham qo'shishi mumkin
    const sorted = [...list].sort((a, b) => b.reviewCount - a.reviewCount);
    return sorted.map((r, i) => mapApiToRestaurant(r, i));
  } catch {
    return [];
  }
}

export async function fetchRestaurantById(id: string): Promise<Restaurant | null> {
  try {
    const r = await restaurantsApi.get(id);
    if (!r.isApproved || r.isSuspended) return null;
    return mapApiToRestaurant(r);
  } catch {
    return null;
  }
}
