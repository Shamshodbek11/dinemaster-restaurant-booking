/**
 * API Client
 * ---------------------------------------------------------------
 * Bu fayl .NET backend bilan ulanish uchun barcha funksiyalarni o'z ichiga oladi.
 *
 * Backend manzili (dev): sayt bilan bir xil manzil — Vite /api so'rovlarini http://localhost:5000 ga
 * uzatadi (vite.config.ts → server.proxy). Shu sababli sayt telefondan (http://<kompyuter-IP>:8080)
 * ochilsa ham ishlaydi. Production build'da VITE_API_BASE_URL ko'rsatiladi.
 * Authentication: JWT Bearer token (localStorage'da saqlanadi)
 */

import { BRAND_SLUG } from './brand';

// ---------------------------------------------------------------
// Konfiguratsiya
// ---------------------------------------------------------------
export const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  (import.meta.env.DEV ? '' : 'http://localhost:5000');

const TOKEN_KEY = 'dm-token';
const TOKEN_EXPIRES_KEY = 'dm-token-expires';

// ---------------------------------------------------------------
// Token boshqaruvi (localStorage)
// ---------------------------------------------------------------
export const tokenStore = {
  get(): string | null {
    const token = localStorage.getItem(TOKEN_KEY);
    const expires = localStorage.getItem(TOKEN_EXPIRES_KEY);
    if (!token) return null;
    if (expires && new Date(expires).getTime() < Date.now()) {
      this.clear();
      return null;
    }
    return token;
  },
  set(token: string, expiresAt: string) {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(TOKEN_EXPIRES_KEY, expiresAt);
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(TOKEN_EXPIRES_KEY);
  },
};

// ---------------------------------------------------------------
// Fetch wrapper
// ---------------------------------------------------------------
export class ApiError extends Error {
  status: number;
  body: unknown;
  constructor(status: number, message: string, body: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

interface RequestOptions {
  method?: HttpMethod;
  body?: unknown;
  auth?: boolean; // default: true (token qo'shiladi)
  signal?: AbortSignal;
}

export interface PaginatedResponse<T> {
  total: number;
  page: number;
  size: number;
  items: T[];
}

interface MessageResponse {
  message?: string;
}

interface ReservationStatusResponse {
  id: string;
  status: Reservation['status'];
}

interface ReservationDepositResponse {
  id: string;
  depositPaid: boolean;
}

/** {a: 1, b: undefined} → "?a=1" (bo'sh qiymatlar tashlab yuboriladi) */
function qs(params: object): string {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') search.set(k, String(v));
  }
  const s = search.toString();
  return s ? `?${s}` : '';
}

function unwrapItems<T>(response: T[] | PaginatedResponse<T>): T[] {
  if (Array.isArray(response)) return response;
  return Array.isArray(response?.items) ? response.items : [];
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = true, signal } = options;

  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (auth) {
    const token = tokenStore.get();
    if (token) headers['Authorization'] = `Bearer ${token}`;
  }

  const url = path.startsWith('http') ? path : `${API_BASE_URL}${path}`;
  const response = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });

  // 204 No Content
  if (response.status === 204) return undefined as T;

  const text = await response.text();
  const json = text ? safeJsonParse(text) : null;

  if (!response.ok) {
    const message = extractErrorMessage(json) ?? response.statusText;
    // 401 — token eskirgan yoki yaroqsiz
    if (response.status === 401) {
      tokenStore.clear();
    }
    throw new ApiError(response.status, message, json);
  }

  return json as T;
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function extractErrorMessage(body: unknown): string | null {
  if (!body || typeof body !== 'object') return null;
  const obj = body as Record<string, unknown>;
  if (typeof obj.message === 'string') return obj.message;
  if (typeof obj.title === 'string') return obj.title;
  // ValidationProblemDetails
  if (obj.errors && typeof obj.errors === 'object') {
    const errs = Object.values(obj.errors as Record<string, string[]>).flat();
    if (errs.length) return errs.join('; ');
  }
  return null;
}

// ---------------------------------------------------------------
// Type definitions — backend DTO'lariga mos
// ---------------------------------------------------------------
export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  yumsBalance: number;
  avatarUrl?: string | null;
  preferredLanguage: string;
  /** false — ro'yxatdan o'tgan, lekin emailga kelgan kod hali kiritilmagan */
  emailConfirmed?: boolean;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface Profile {
  id: string;
  email: string;
  fullName: string;
  avatarUrl?: string | null;
  phone?: string | null;
  yumsBalance: number;
  preferredLanguage: string;
  isBanned: boolean;
  createdAt: string;
  updatedAt: string;
  roles: string[];
  /** Telegram bot ulanganmi (bildirishnomalar Telegram'ga ham keladi) */
  telegramLinked?: boolean;
}

export interface ReferralInfo {
  code: string;
  invited: number;
  rewarded: number;
  bonusPerFriend: number;
  earned: number;
}

/** Restoranga kirish darajasi: owner — egasi, manager — menejer, host — administrator (zal) */
export type AccessLevel = 'owner' | 'manager' | 'host' | 'none';

export interface Restaurant {
  id: string;
  ownerId?: string | null;
  name: string;
  description: string;
  cuisineType: string;
  priceRange: string;
  address: string;
  city: string;
  latitude?: number | null;
  longitude?: number | null;
  phone?: string | null;
  openingHours: string;
  /** Bayram/maxsus kunlar JSON: {"2026-12-31":"closed","2027-01-01":"12:00-20:00"} */
  specialHours?: string | null;
  /** Joriy foydalanuvchining shu restorandagi huquqi (faqat /api/admin/restaurants) */
  accessLevel?: AccessLevel;
  totalSlots: number;
  /** Bir soatda boshlanadigan bronlar chegarasi (null — cheklanmagan) */
  hourlySlots?: number | null;
  /** Egasi o'zgartirgan, superadmin tasdig'ini kutayotgan maydonlar */
  pendingChanges?: Partial<Pick<Restaurant, 'name' | 'description' | 'cuisineType' | 'priceRange' | 'address' | 'city' | 'latitude' | 'longitude' | 'imagesUrl'>> | null;
  pendingChangesSubmittedAt?: string | null;
  /** Superadmin ro'yxati uchun */
  hasPendingChanges?: boolean;
  ownerName?: string | null;
  ownerEmail?: string | null;
  imagesUrl: string[];
  rating: number;
  reviewCount: number;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  amenities: string[];
  policies: string[];
  depositRequired: boolean;
  depositPerGuest: number;
  discount?: number | null;
  discountTimeStart?: string | null;
  discountTimeEnd?: string | null;
  isApproved: boolean;
  isSuspended: boolean;
  rejectionReason?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ReservationItem {
  menuItemId: string;
  menuItemName?: string | null;
  quantity: number;
  unitPrice: number;
}

export interface Reservation {
  id: string;
  userId: string;
  userFullName?: string | null;
  userPhone?: string | null;
  restaurantId: string;
  restaurantName?: string | null;
  tableId?: string | null;
  tableLabel?: string | null;
  date: string; // "yyyy-MM-dd"
  time: string; // "HH:mm:ss"
  guestsCount: number;
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed' | 'no_show';
  specialRequests?: string | null;
  yumsUsed: number;
  /** Tashrif yakunlangach beriladigan yums (completed bo'lsa — berilgan) */
  yumsEarned?: number;
  totalDiscount: number;
  depositAmount: number;
  depositPaid: boolean;
  isNoShow: boolean;
  preOrderTotal: number;
  /** Bron vaqtidagi restoran chegirmasi, % */
  restaurantDiscount?: number;
  /** Oldindan buyurtmaga qo'llangan chegirma, so'm */
  preOrderDiscount?: number;
  cancelledBy?: 'user' | 'restaurant' | 'system' | null;
  cancelReason?: string | null;
  isLateCancellation?: boolean;
  /** refund_requested — qaytarilmoqda, refunded — qaytarildi, forfeited — qaytarilmaydi */
  depositOutcome?: 'refund_requested' | 'refunded' | 'forfeited' | null;
  userEmail?: string | null;
  items: ReservationItem[];
  createdAt: string;
  updatedAt: string;
}

export interface Review {
  id: string;
  userId: string;
  userFullName?: string | null;
  restaurantId: string;
  rating: number;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  comment?: string | null;
  ownerResponse?: string | null;
  ownerRespondedAt?: string | null;
  createdAt: string;
}

// ---------------------------------------------------------------
// Menu types
// ---------------------------------------------------------------
export interface MenuItem {
  id: string;
  categoryId: string;
  restaurantId: string;
  name: string;
  description: string;
  price: number;
  imageUrl?: string | null;
  isAvailable: boolean;
  isPopular: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface MenuCategory {
  id: string;
  restaurantId: string;
  name: string;
  sortOrder: number;
  items: MenuItem[];
  createdAt: string;
}

// ---------------------------------------------------------------
// Restaurant table
// ---------------------------------------------------------------
export interface RestaurantTable {
  id: string;
  restaurantId: string;
  label: string;
  capacity: number;
  location: 'hall' | 'terrace' | 'vip' | 'garden' | string;
  isActive: boolean;
  createdAt: string;
}

export interface TableAvailability {
  tableId: string;
  label: string;
  capacity: number;
  location: string;
  isAvailable: boolean;
}

// ---------------------------------------------------------------
// Waitlist
// ---------------------------------------------------------------
export interface WaitlistEntry {
  id: string;
  userId: string;
  userFullName?: string | null;
  restaurantId: string;
  date: string;
  time?: string | null;
  guestsCount: number;
  status: 'waiting' | 'notified' | 'seated' | 'cancelled';
  note?: string | null;
  createdAt: string;
}

// ---------------------------------------------------------------
// Notification
// ---------------------------------------------------------------
export interface NotificationItem {
  id: string;
  type: 'booking_reminder' | 'booking_confirmed' | 'review_reply' | 'general' | string;
  title: string;
  body: string;
  link?: string | null;
  isRead: boolean;
  reservationId?: string | null;
  createdAt: string;
}

export interface Venue {
  id: string;
  ownerId?: string | null;
  name: string;
  description: string;
  venueType: string;
  address: string;
  city: string;
  phone?: string | null;
  capacityMin: number;
  capacityMax: number;
  pricePerHead: number;
  imagesUrl: string[];
  rating: number;
  reviewCount: number;
  isApproved: boolean;
  isSuspended: boolean;
  rejectionReason?: string | null;
  openingHours: string;
  createdAt: string;
  updatedAt: string;
}

export interface VenueReservation {
  id: string;
  userId: string;
  venueId: string;
  venueName?: string | null;
  eventDate: string;
  eventType: string;
  guestsCount: number;
  specialRequests?: string | null;
  totalPrice: number;
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed';
  yumsUsed: number;
  createdAt: string;
  updatedAt: string;
}

export interface Favorite {
  id: string;
  userId: string;
  restaurantId: string;
  createdAt: string;
}

export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  phone?: string | null;
  yumsBalance: number;
  isBanned: boolean;
  emailConfirmed?: boolean;
  roles: string[];
  /** Admin (restoran egasi) bo'lsa — restoranlari */
  restaurants?: { id: string; name: string; isApproved: boolean; isSuspended: boolean }[];
  reservationsCount?: number;
  createdAt: string;
}

export interface AdminUserDetail {
  user: AdminUser;
  emailConfirmed: boolean;
  noShowCount: number;
  reservations: { id: string; date: string; time: string; guestsCount: number; status: string; depositAmount: number; depositPaid: boolean; restaurantName: string }[];
  yums: { id: string; amount: number; reason?: string | null; createdAt: string }[];
  restaurants: { id: string; name: string; city: string; isApproved: boolean; isSuspended: boolean }[];
}

export interface SuperadminReservation extends Omit<Reservation, 'items'> {
  userEmail?: string | null;
}

export interface SuperadminReview {
  id: string;
  restaurantId: string;
  restaurantName: string;
  userId: string;
  userFullName?: string | null;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  rating: number;
  comment?: string | null;
  ownerResponse?: string | null;
  createdAt: string;
}

export interface SuperadminPayment {
  id: string;
  number: number;
  provider: string;
  providerTransactionId?: string | null;
  amount: number;
  state: number;
  reservationId: string;
  restaurantName?: string | null;
  reservationDate?: string | null;
  userFullName?: string | null;
  createdAt: string;
  performedAt?: string | null;
  cancelledAt?: string | null;
  /** Mijozga qaytarish so'ralgan vaqt (Click/Payme kabinetida qaytarilishi kerak) */
  refundRequestedAt?: string | null;
  reservationStatus?: string | null;
}

export interface SuperadminVenueReservation {
  id: string;
  venueId: string;
  venueName?: string | null;
  userFullName?: string | null;
  userEmail?: string | null;
  userPhone?: string | null;
  eventDate: string;
  eventType: string;
  guestsCount: number;
  totalPrice: number;
  status: string;
  specialRequests?: string | null;
  createdAt: string;
}

export interface PartnerApplication {
  id: string;
  userId: string;
  restaurantName: string;
  city: string;
  address: string;
  phone: string;
  message?: string | null;
  status: 'pending' | 'approved' | 'rejected';
  rejectionReason?: string | null;
  createdAt: string;
  reviewedAt?: string | null;
  userFullName?: string | null;
  userEmail?: string | null;
}

export interface AuditLogEntry {
  id: string;
  actorId?: string | null;
  actorRole: string;
  actorName?: string | null;
  actorEmail?: string | null;
  entityType: string;
  entityId?: string | null;
  restaurantId?: string | null;
  restaurantName?: string | null;
  action: string;
  summary: string;
  /** JSON: {"maydon": {"old": ..., "new": ...}} */
  changes?: string | null;
  createdAt: string;
}

export interface ReportRow {
  label: string;
  reservations: number;
  guests: number;
  completed: number;
  cancelled: number;
  noShow: number;
}

export interface RestaurantReport {
  restaurantId: string;
  restaurantName: string;
  period: 'day' | 'week' | 'month' | 'year';
  label: string;
  from: string;
  to: string;
  generatedAt: string;
  totals: {
    reservations: number; active: number; completed: number; cancelled: number; noShow: number;
    guests: number; avgGuests: number; cancelRate: number; noShowRate: number;
    depositsPaid: number; depositsUnpaid: number; preOrderTotal: number; yumsDiscounts: number;
    uniqueGuests: number; returningGuests: number; reviews: number; avgRating: number; waitlistEntries: number;
  };
  breakdown: ReportRow[];
  topHours: ReportRow[];
  topMenuItems: { name: string; quantity: number; revenue: number }[];
}

export interface PlatformStats {
  totalUsers: number;
  newUsersThisWeek?: number;
  totalAdmins: number;
  bannedUsers: number;
  totalRestaurants: number;
  activeRestaurants?: number;
  pendingRestaurants: number;
  pendingChanges?: number;
  totalVenues: number;
  pendingVenues?: number;
  pendingVenueReservations?: number;
  pendingPartnerApplications?: number;
  totalReservations: number;
  totalReservationsToday: number;
  reservationsThisMonth?: number;
  completedThisMonth?: number;
  cancelledThisMonth?: number;
  noShowThisMonth?: number;
  depositsPaidTotal?: number;
  totalYumsInCirculation: number;
}

export interface ProcessBookingResult {
  reservationId: string;
  yumsEarned: number;
  /** true — yumsEarned tashrif yakunlangach hisobga qo'shiladi */
  yumsAwardedAfterVisit?: boolean;
  yumsUsed: number;
  slotsRemaining: number;
  depositDue: number;
  /** 'restaurant' — restoran talabi, 'no_show' — oldingi kelmasliklar sababli */
  depositReason?: 'restaurant' | 'no_show' | null;
  preOrderTotal: number;
  /** Jami chegirma (restoran + yums), % */
  totalDiscount?: number;
  restaurantDiscount?: number;
  /** Oldindan buyurtmaga chegirma, so'm */
  preOrderDiscount?: number;
  /** Shuncha soat oldin bekor qilinsa depozit qaytariladi */
  freeCancellationHours?: number;
}

export interface CancelResult {
  message: string;
  depositOutcome?: Reservation['depositOutcome'];
  isLateCancellation: boolean;
}

export interface AdminStats {
  totalReservations: number;
  todayReservations: number;
  confirmed: number;
  refundsPending: number;
  restaurantCount: number;
}

export interface StaffMember {
  id: string;
  userId: string;
  role: 'manager' | 'host';
  fullName?: string | null;
  email: string;
  createdAt?: string;
}

export interface PublicStats {
  restaurants: number;
  cities: number;
  reservations: number;
  guests: number;
  reviews: number;
  avgRating: number;
  users: number;
}

export interface VenueOwnerReservation {
  id: string;
  venueId: string;
  venueName: string;
  userFullName?: string | null;
  userEmail?: string | null;
  userPhone?: string | null;
  eventDate: string;
  eventType: string;
  guestsCount: number;
  totalPrice: number;
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed';
  specialRequests?: string | null;
  createdAt: string;
}

export interface BookingQuote {
  depositPerGuest: number;
  depositAmount: number;
  depositReason?: 'restaurant' | 'no_show' | null;
  noShowCount: number;
  emailConfirmed: boolean;
  freeCancellationHours?: number;
}

export interface AvailabilitySlot {
  time: string; // "HH:mm"
  available: boolean;
  reason?: 'past' | 'day_full' | 'hour_full' | 'too_far' | null;
  /** Shu vaqtdagi restoran chegirmasi, % */
  discount?: number;
}

export interface AvailabilityResult {
  date: string;
  openingHours: string;
  totalSlots: number;
  hourlySlots?: number | null;
  booked: number;
  slots: AvailabilitySlot[];
}

export interface PendingRestaurantChange {
  id: string;
  name: string;
  ownerId?: string | null;
  submittedAt?: string | null;
  current: NonNullable<Restaurant['pendingChanges']>;
  pending: NonNullable<Restaurant['pendingChanges']>;
}

// ---------------------------------------------------------------
// API — Auth
// ---------------------------------------------------------------
export const authApi = {
  register: (data: {
    email: string; password: string; fullName: string; preferredLanguage?: string;
    /** Foydalanish shartlari va maxfiylik siyosatiga rozilik (majburiy) */
    acceptTerms: boolean;
    /** Do'st taklif kodi (ixtiyoriy) */
    referralCode?: string;
  }) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', body: data, auth: false }),

  login: (data: { email: string; password: string }) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', body: data, auth: false }),

  setupSuperadmin: (data: { email: string; password: string; fullName: string }) =>
    request<AuthResponse>('/api/auth/setup-superadmin', { method: 'POST', body: data, auth: false }),

  me: () => request<AuthUser>('/api/auth/me'),

  forgotPassword: (email: string) =>
    request<{ message: string }>('/api/auth/forgot-password', { method: 'POST', body: { email }, auth: false }),

  resetPassword: (email: string, code: string, newPassword: string) =>
    request<{ message: string }>('/api/auth/reset-password', { method: 'POST', body: { email, code, newPassword }, auth: false }),

  /** Emailni tasdiqlash kodini (qayta) yuborish */
  sendEmailCode: () =>
    request<{ message: string }>('/api/auth/email/send-code', { method: 'POST' }),

  verifyEmail: (code: string) =>
    request<{ message: string; emailConfirmed: boolean }>('/api/auth/email/verify', { method: 'POST', body: { code } }),
};

// ---------------------------------------------------------------
// API — Profile
// ---------------------------------------------------------------
export const profileApi = {
  me: () => request<Profile>('/api/profiles/me'),

  update: (data: { fullName?: string; avatarUrl?: string; phone?: string; preferredLanguage?: string }) =>
    request<Profile>('/api/profiles/me', { method: 'PUT', body: data }),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    request<{ message: string }>('/api/profiles/change-password', { method: 'POST', body: data }),

  // NOTE: redeemYums olib tashlandi. Yums faqat bron chegirmasi uchun.
  // Bron qilishda useYums: true orqali sarflanadi.

  yumsHistory: (page = 1, size = 30) =>
    request<{ total: number; items: { id: string; amount: number; reason: string; createdAt: string }[] }>(
      `/api/profiles/yums/history?page=${page}&size=${size}`
    ),

  /** Do'st taklifi: shaxsiy kod va statistika */
  referral: () => request<ReferralInfo>('/api/profiles/referral'),

  /** Telegram botga ulanish havolasi (t.me/bot?start=KOD) */
  telegramLink: () => request<{ url: string }>('/api/profiles/telegram/link', { method: 'POST' }),

  telegramUnlink: () => request<{ telegramLinked: boolean }>('/api/profiles/telegram', { method: 'DELETE' }),
};

// ---------------------------------------------------------------
// API — Ochiq statistika (bosh sahifa)
// ---------------------------------------------------------------
export const publicApi = {
  stats: () => request<PublicStats>('/api/public/stats', { auth: false }),
};

// ---------------------------------------------------------------
// API — Restaurants
// ---------------------------------------------------------------
export const restaurantsApi = {
  /** date + time berilsa — faqat shu vaqtda guests kishiga joyi bor restoranlar */
  list: (params: { city?: string; search?: string; date?: string; time?: string; guests?: number; size?: number } = {}) =>
    request<Restaurant[] | PaginatedResponse<Restaurant>>(`/api/restaurants${qs({ size: 100, ...params })}`, { auth: false })
      .then(unwrapItems),

  get: (id: string) => request<Restaurant>(`/api/restaurants/${id}`, { auth: false }),

  mine: () => request<Restaurant[]>('/api/restaurants/mine'),

  create: (data: Partial<Restaurant>) =>
    request<Restaurant>('/api/restaurants', { method: 'POST', body: data }),

  update: (id: string, data: Partial<Restaurant>) =>
    request<Restaurant>(`/api/restaurants/${id}`, { method: 'PUT', body: data }),

  delete: (id: string) => request<void>(`/api/restaurants/${id}`, { method: 'DELETE' }),

  /** Kun bo'yicha bron qilish mumkin bo'lgan vaqtlar (ish vaqti + sig'im) */
  availability: (id: string, date: string) =>
    request<AvailabilityResult>(`/api/restaurants/${id}/availability?date=${date}`, { auth: false }),
};

// ---------------------------------------------------------------
// API — Reservations (user)
// ---------------------------------------------------------------
export const reservationsApi = {
  mine: () =>
    request<Reservation[] | PaginatedResponse<Reservation>>('/api/reservations/mine')
      .then(unwrapItems),

  create: (data: {
    restaurantId: string;
    date: string;
    time: string;
    guestsCount: number;
    specialRequests?: string;
    useYums?: boolean;
    tableId?: string | null;
    items?: Array<{ menuItemId: string; quantity: number }>;
  }) =>
    request<ProcessBookingResult>('/api/reservations', {
      method: 'POST',
      body: data,
    }),

  cancel: (id: string, reason?: string) =>
    request<CancelResult>(`/api/reservations/${id}/cancel`, { method: 'POST', body: { reason: reason || undefined } }),

  /** Sana, vaqt va mehmonlar sonini o'zgartirish (boshlanishiga 2 soatdan ko'p qolganda) */
  modify: (id: string, data: { date: string; time: string; guestsCount: number; tableId?: string | null; keepTable?: boolean }) =>
    request<ProcessBookingResult>(`/api/reservations/${id}`, { method: 'PUT', body: data }),

  /** Bron oldidan: depozit kerakmi, email tasdiqlanganmi */
  quote: (restaurantId: string, guests: number) =>
    request<BookingQuote>(`/api/reservations/quote?restaurantId=${restaurantId}&guests=${guests}`),

  // Restoran egasi uchun: oyga kalendar (yyyy-MM-dd -> count)
  calendar: (restaurantId: string, yearMonth: string) =>
    request<Array<{ date: string; count: number; guests: number }>>(
      `/api/reservations/calendar?restaurantId=${restaurantId}&month=${yearMonth}`,
    ),

  markNoShow: (id: string) =>
    request<ReservationStatusResponse>(`/api/admin/reservations/${id}/no-show`, { method: 'POST' }),

  markDepositPaid: (id: string) =>
    request<ReservationDepositResponse>(`/api/admin/reservations/${id}/deposit-paid`, { method: 'POST' }),

  /** Naqd/terminal orqali olingan depozit mijozga qaytarildi */
  markDepositRefunded: (id: string) =>
    request<MessageResponse>(`/api/admin/reservations/${id}/deposit-refunded`, { method: 'POST' }),

};

// ---------------------------------------------------------------
// API — Reviews
// ---------------------------------------------------------------
export interface MyReview {
  id: string;
  restaurantId: string;
  restaurantName?: string | null;
  rating: number;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  comment?: string | null;
  ownerResponse?: string | null;
  ownerRespondedAt?: string | null;
  createdAt: string;
}

export const reviewsApi = {
  /** Foydalanuvchining o'z sharhlari */
  mine: () => request<MyReview[]>('/api/reviews/mine'),

  byRestaurant: (restaurantId: string) =>
    request<Review[] | PaginatedResponse<Review>>(`/api/reviews/restaurant/${restaurantId}`, { auth: false })
      .then(unwrapItems),

  create: (data: {
    restaurantId: string;
    rating: number;
    foodRating?: number;
    serviceRating?: number;
    interiorRating?: number;
    comment?: string;
  }) => request<Review>('/api/reviews', { method: 'POST', body: data }),

  update: (id: string, data: {
    rating?: number;
    foodRating?: number;
    serviceRating?: number;
    interiorRating?: number;
    comment?: string;
  }) => request<Review>(`/api/reviews/${id}`, { method: 'PUT', body: data }),

  delete: (id: string) => request<void>(`/api/reviews/${id}`, { method: 'DELETE' }),

  // Restoran egasining javobi
  respond: (id: string, data: { ownerResponse: string }) =>
    request<Review>(`/api/reviews/${id}/respond`, { method: 'POST', body: data }),
};

// ---------------------------------------------------------------
// API — Menu (categories + items)
// ---------------------------------------------------------------
export const menuApi = {
  // Public — faqat available items, restoran sahifasi uchun
  public: (restaurantId: string) =>
    request<MenuCategory[]>(`/api/restaurants/${restaurantId}/menu`, { auth: false }),

  // Owner — hamma itemlar (available bo'lmasa ham) menu builder uchun
  all: (restaurantId: string) =>
    request<MenuCategory[]>(`/api/restaurants/${restaurantId}/menu/all`),

  createCategory: (restaurantId: string, data: { name: string; sortOrder?: number }) =>
    request<MenuCategory>(`/api/restaurants/${restaurantId}/menu/categories`, {
      method: 'POST',
      body: data,
    }),

  updateCategory: (restaurantId: string, id: string, data: { name?: string; sortOrder?: number }) =>
    request<MenuCategory>(`/api/restaurants/${restaurantId}/menu/categories/${id}`, {
      method: 'PUT',
      body: data,
    }),

  deleteCategory: (restaurantId: string, id: string) =>
    request<void>(`/api/restaurants/${restaurantId}/menu/categories/${id}`, { method: 'DELETE' }),

  createItem: (restaurantId: string, data: {
    categoryId: string;
    name: string;
    description?: string;
    price: number;
    imageUrl?: string;
    isAvailable?: boolean;
    isPopular?: boolean;
    sortOrder?: number;
  }) =>
    request<MenuItem>(`/api/restaurants/${restaurantId}/menu/items`, {
      method: 'POST',
      body: data,
    }),

  updateItem: (restaurantId: string, id: string, data: Partial<{
    categoryId: string;
    name: string;
    description: string;
    price: number;
    imageUrl: string;
    isAvailable: boolean;
    isPopular: boolean;
    sortOrder: number;
  }>) =>
    request<MenuItem>(`/api/restaurants/${restaurantId}/menu/items/${id}`, {
      method: 'PUT',
      body: data,
    }),

  deleteItem: (restaurantId: string, id: string) =>
    request<{ message: string; hidden?: boolean }>(`/api/restaurants/${restaurantId}/menu/items/${id}`, { method: 'DELETE' }),
};

// ---------------------------------------------------------------
// API — Restaurant tables
// ---------------------------------------------------------------
export const tablesApi = {
  list: (restaurantId: string) =>
    request<RestaurantTable[]>(`/api/tables/restaurant/${restaurantId}`, { auth: false }),

  create: (restaurantId: string, data: {
    label: string;
    capacity: number;
    location?: string;
  }) =>
    request<RestaurantTable>('/api/tables', {
      method: 'POST',
      body: { restaurantId, ...data },
    }),

  update: (restaurantId: string, id: string, data: Partial<{
    label: string;
    capacity: number;
    location: string;
    isActive: boolean;
  }>) =>
    request<RestaurantTable>(`/api/tables/${id}`, {
      method: 'PUT',
      body: { restaurantId, ...data },
    }),

  delete: (restaurantId: string, id: string) =>
    request<void>(`/api/tables/${id}`, { method: 'DELETE' }),

  // Sana + vaqt + mehmonlar soni bo'yicha mavjud stollar ro'yxati
  availability: (restaurantId: string, params: {
    date: string;
    time: string;
    guests: number;
  }) => {
    const qs = new URLSearchParams({
      date: params.date,
      time: params.time,
      guests: String(params.guests),
    }).toString();
    return request<TableAvailability[]>(
      `/api/tables/restaurant/${restaurantId}/availability?${qs}`,
      { auth: false },
    );
  },
};

// ---------------------------------------------------------------
// API — Waitlist
// ---------------------------------------------------------------
export const waitlistApi = {
  join: (data: {
    restaurantId: string;
    date: string;
    time?: string;
    guestsCount: number;
    note?: string;
  }) => request<WaitlistEntry>('/api/waitlist', { method: 'POST', body: data }),

  mine: () => request<WaitlistEntry[]>('/api/waitlist/mine'),

  forRestaurant: (restaurantId: string) =>
    request<WaitlistEntry[]>(`/api/waitlist/restaurant/${restaurantId}`),

  updateStatus: (id: string, status: 'waiting' | 'notified' | 'seated' | 'cancelled') =>
    request<WaitlistEntry>(`/api/waitlist/${id}/status`, {
      method: 'PATCH',
      body: { status },
    }),

  cancel: (id: string) => request<void>(`/api/waitlist/${id}`, { method: 'DELETE' }),
};

// ---------------------------------------------------------------
// API — Payments (bron depoziti)
// ---------------------------------------------------------------
export type PaymentProvider = 'payme' | 'click' | 'test';

export const paymentsApi = {
  providers: () => request<{ providers: PaymentProvider[] }>('/api/payments/providers', { auth: false }),

  /** To'lov sahifasi havolasini oladi; test provayderda darhol "to'landi" */
  deposit: (reservationId: string, provider: PaymentProvider) =>
    request<{ provider: PaymentProvider; redirectUrl: string; paid: boolean }>('/api/payments/deposit', {
      method: 'POST',
      body: { reservationId, provider },
    }),
};

// ---------------------------------------------------------------
// API — Notifications (in-app)
// ---------------------------------------------------------------
export const notificationsApi = {
  mine: () =>
    request<NotificationItem[] | PaginatedResponse<NotificationItem>>('/api/notifications')
      .then(unwrapItems),

  unreadCount: () => request<{ count: number }>('/api/notifications/unread-count'),

  markRead: (id: string) =>
    request<void>(`/api/notifications/${id}/read`, { method: 'POST' }),

  readAll: () =>
    request<{ updated: number }>('/api/notifications/read-all', { method: 'POST' }),
};

// ---------------------------------------------------------------
// API — Favorites
// ---------------------------------------------------------------
export const favoritesApi = {
  // Backend sevimli restoranlar ro'yxatini RestaurantDto[] shaklida qaytaradi
  list: () => request<Restaurant[]>('/api/favorites'),

  add: (restaurantId: string) =>
    request<void>(`/api/favorites/${restaurantId}`, { method: 'POST' }),

  remove: (restaurantId: string) =>
    request<void>(`/api/favorites/${restaurantId}`, { method: 'DELETE' }),
};

// ---------------------------------------------------------------
// API — Venues
// ---------------------------------------------------------------
export const venuesApi = {
  list: () => request<Venue[]>('/api/venues', { auth: false }),

  get: (id: string) => request<Venue>(`/api/venues/${id}`, { auth: false }),

  create: (data: Partial<Venue>) =>
    request<Venue>('/api/venues', { method: 'POST', body: data }),

  update: (id: string, data: Partial<Venue>) =>
    request<Venue>(`/api/venues/${id}`, { method: 'PUT', body: data }),

  createReservation: (venueId: string, data: {
    eventDate: string;
    eventType: string;
    guestsCount: number;
    specialRequests?: string;
  }) =>
    request<VenueReservation>(`/api/venues/${venueId}/reservations`, {
      method: 'POST',
      body: data,
    }),

  myReservations: () => request<VenueReservation[]>('/api/venues/reservations/mine'),

  /** Zal egasi: o'z zallari (tasdiqlanmaganlari ham) */
  mine: () => request<Venue[]>('/api/venues/mine'),

  /** Zal egasi: o'z zallariga kelgan tadbir bronlari */
  ownerReservations: (status?: string) =>
    request<VenueOwnerReservation[]>(`/api/venues/owner-reservations${qs({ status })}`),

  /** Zal egasi / superadmin: confirmed | cancelled | completed */
  setReservationStatus: (reservationId: string, status: 'confirmed' | 'cancelled' | 'completed', note?: string) =>
    request<VenueReservation>(`/api/venues/reservations/${reservationId}/status`, { method: 'PUT', body: { status, note } }),

  // Kalendarda band sanalarni ko'rsatish uchun (yyyy-MM-dd massivi).
  bookedDates: (venueId: string) =>
    request<string[]>(`/api/venues/${venueId}/booked-dates`, { auth: false }),
};

// ---------------------------------------------------------------
// API — Admin (restaurant owner)
// ---------------------------------------------------------------
export const adminApi = {
  restaurants: () => request<Restaurant[]>('/api/admin/restaurants'),

  reservations: (params: { status?: string; date?: string; restaurantId?: string; size?: number } = {}) =>
    request<Reservation[] | PaginatedResponse<Reservation>>(`/api/admin/reservations${qs({ size: 500, ...params })}`)
      .then(unwrapItems),

  /** reason — restoran bekor qilganda mijozga ko'rsatiladi */
  updateReservationStatus: (id: string, status: string, reason?: string) =>
    request<ReservationStatusResponse>(`/api/admin/reservations/${id}/status`, {
      method: 'PUT',
      body: { status, reason: reason || undefined },
    }),

  /** Bronga stol biriktirish (null — olib tashlash) */
  assignTable: (id: string, tableId: string | null) =>
    request<{ id: string; tableId: string | null; tableLabel: string | null }>(`/api/admin/reservations/${id}/table`, {
      method: 'PUT',
      body: { tableId },
    }),

  stats: () => request<AdminStats>('/api/admin/stats'),

  /** period: day | week | month | year; date — davr ichidagi istalgan kun (yyyy-MM-dd) */
  report: (params: { restaurantId?: string; period: string; date: string }) =>
    request<RestaurantReport>(`/api/admin/reports${qs(params)}`),

  /** PDF faylni yuklab oladi (brauzer "Saqlash" oynasi) */
  downloadReportPdf: async (params: { restaurantId?: string; period: string; date: string }) => {
    const token = tokenStore.get();
    const res = await fetch(`${API_BASE_URL}/api/admin/reports/pdf${qs(params)}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      throw new ApiError(res.status, extractErrorMessage(body) ?? res.statusText, body);
    }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${BRAND_SLUG}-hisobot-${params.period}-${params.date}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};

// ---------------------------------------------------------------
// API — Restoran xodimlari (faqat egasi boshqaradi)
// ---------------------------------------------------------------
export const staffApi = {
  list: (restaurantId: string) => request<StaffMember[]>(`/api/restaurants/${restaurantId}/staff`),

  add: (restaurantId: string, data: { email: string; role: 'manager' | 'host' }) =>
    request<StaffMember>(`/api/restaurants/${restaurantId}/staff`, { method: 'POST', body: data }),

  remove: (restaurantId: string, staffId: string) =>
    request<MessageResponse>(`/api/restaurants/${restaurantId}/staff/${staffId}`, { method: 'DELETE' }),
};

// ---------------------------------------------------------------
// API — Hamkorlik (restoran egasi bo'lish arizasi)
// ---------------------------------------------------------------
export const partnerApi = {
  submit: (data: { restaurantName: string; city: string; address?: string; phone: string; message?: string }) =>
    request<PartnerApplication>('/api/partner-applications', { method: 'POST', body: data }),

  /** Oxirgi ariza (yo'q bo'lsa — undefined) */
  mine: () => request<PartnerApplication | undefined>('/api/partner-applications/mine'),
};

// ---------------------------------------------------------------
// API — Superadmin (platform-wide)
// ---------------------------------------------------------------
export const superadminApi = {
  /** role: user | admin | superadmin | banned; search: ism, email yoki (admin uchun) restoran nomi */
  users: (params: { search?: string; role?: string; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<AdminUser>>(`/api/superadmin/users${qs(params)}`),

  userDetail: (id: string) => request<AdminUserDetail>(`/api/superadmin/users/${id}`),

  confirmEmail: (id: string) =>
    request<{ id: string; emailConfirmed: boolean }>(`/api/superadmin/users/${id}/confirm-email`, { method: 'POST' }),

  ban: (data: { userId: string; reason?: string }) =>
    request<AdminUser>('/api/superadmin/users/ban', { method: 'POST', body: data }),

  unban: (data: { userId: string }) =>
    request<AdminUser>('/api/superadmin/users/unban', { method: 'POST', body: data }),

  adjustYums: (data: { userId: string; amount: number; reason?: string }) =>
    request<AdminUser>('/api/superadmin/users/adjust-yums', { method: 'POST', body: data }),

  assignRole: (data: { userId: string; role: 'admin' | 'user' }) =>
    request<AdminUser>('/api/superadmin/users/assign-role', { method: 'POST', body: data }),

  revokeRole: (data: { userId: string; role: 'admin' | 'user' }) =>
    request<AdminUser>('/api/superadmin/users/revoke-role', { method: 'POST', body: data }),

  restaurants: () => request<Restaurant[]>('/api/superadmin/restaurants'),

  approveRestaurant: (id: string, data: { approve: boolean; rejectionReason?: string }) =>
    request<Restaurant>(`/api/superadmin/restaurants/${id}/approval`, {
      method: 'POST',
      body: data,
    }),

  suspendRestaurant: (id: string) =>
    request<Restaurant>(`/api/superadmin/restaurants/${id}/suspend`, { method: 'POST' }),

  unsuspendRestaurant: (id: string) =>
    request<Restaurant>(`/api/superadmin/restaurants/${id}/unsuspend`, { method: 'POST' }),

  venues: () => request<Venue[]>('/api/superadmin/venues'),

  approveVenue: (id: string, data: { approve: boolean; rejectionReason?: string }) =>
    request<Venue>(`/api/superadmin/venues/${id}/approval`, {
      method: 'POST',
      body: data,
    }),

  stats: () => request<PlatformStats>('/api/superadmin/stats'),

  /** Restoran egalari o'zgartirgan, tasdiq kutayotgan ma'lumotlar */
  pendingChanges: () => request<PendingRestaurantChange[]>('/api/superadmin/restaurants/pending-changes'),

  approveChanges: (id: string) =>
    request<Restaurant>(`/api/superadmin/restaurants/${id}/changes/approve`, { method: 'POST' }),

  rejectChanges: (id: string, reason: string) =>
    request<Restaurant>(`/api/superadmin/restaurants/${id}/changes/reject`, { method: 'POST', body: { reason } }),

  reservations: (params: { restaurantId?: string; status?: string; from?: string; to?: string; search?: string; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<SuperadminReservation>>(`/api/superadmin/reservations${qs(params)}`),

  reviews: (params: { restaurantId?: string; maxRating?: number; search?: string; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<SuperadminReview>>(`/api/superadmin/reviews${qs(params)}`),

  payments: (params: { provider?: string; state?: number; refundPending?: boolean; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<SuperadminPayment> & { totals: { paid: number; refunded: number; refundsPending: number } }>(`/api/superadmin/payments${qs(params)}`),

  /** Click/Payme kabinetida pul qaytarilgach — "qaytarildi" deb belgilash */
  markPaymentRefunded: (id: string) =>
    request<{ id: string; state: number }>(`/api/superadmin/payments/${id}/refunded`, { method: 'POST' }),

  venueReservations: (params: { status?: string; venueId?: string; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<SuperadminVenueReservation>>(`/api/superadmin/venue-reservations${qs(params)}`),

  suspendVenue: (id: string) => request<Venue>(`/api/superadmin/venues/${id}/suspend`, { method: 'POST' }),
  unsuspendVenue: (id: string) => request<Venue>(`/api/superadmin/venues/${id}/unsuspend`, { method: 'POST' }),

  changeOwner: (restaurantId: string, userId: string) =>
    request<Restaurant>(`/api/superadmin/restaurants/${restaurantId}/owner`, { method: 'POST', body: { userId } }),

  partnerApplications: (status?: string) =>
    request<PartnerApplication[]>(`/api/superadmin/partner-applications${qs({ status })}`),
  approvePartner: (id: string) =>
    request<PartnerApplication>(`/api/superadmin/partner-applications/${id}/approve`, { method: 'POST' }),
  rejectPartner: (id: string, reason: string) =>
    request<PartnerApplication>(`/api/superadmin/partner-applications/${id}/reject`, { method: 'POST', body: { reason } }),

  auditLog: (params: { restaurantId?: string; entityType?: string; actorRole?: string; search?: string; page?: number; size?: number } = {}) =>
    request<PaginatedResponse<AuditLogEntry>>(`/api/superadmin/audit-log${qs(params)}`),

  broadcast: (data: { title: string; body?: string; link?: string; audience: 'all' | 'users' | 'admins' }) =>
    request<{ sent: number }>('/api/superadmin/notifications/broadcast', { method: 'POST', body: data }),

  yumsAudit: () =>
    request<Array<{
      id: string;
      userId: string;
      userFullName?: string;
      amount: number;
      reason: string;
      adminId?: string;
      createdAt: string;
    }>>('/api/superadmin/yums-audit'),
};

// ---------------------------------------------------------------
// Yordamchi funksiyalar
// ---------------------------------------------------------------
// Generic api helper — used by pages that need raw POST/GET
// ---------------------------------------------------------------
export const api = {
  get: <T = unknown>(path: string, opts?: { auth?: boolean }) =>
    request<T>(`/api${path}`, { method: 'GET', ...opts }),
  post: <T = unknown>(path: string, body?: unknown, opts?: { auth?: boolean }) =>
    request<T>(`/api${path}`, { method: 'POST', body, auth: opts?.auth ?? true }),
  put: <T = unknown>(path: string, body?: unknown, opts?: { auth?: boolean }) =>
    request<T>(`/api${path}`, { method: 'PUT', body, auth: opts?.auth ?? true }),
  delete: <T = unknown>(path: string, opts?: { auth?: boolean }) =>
    request<T>(`/api${path}`, { method: 'DELETE', ...opts }),
};

// ---------------------------------------------------------------
export function isSuperadmin(user: { roles: string[] } | null): boolean {
  return user?.roles?.some(role => role.toLowerCase() === 'superadmin') ?? false;
}

export function isAdmin(user: { roles: string[] } | null): boolean {
  return user?.roles?.some(role => ['admin', 'superadmin'].includes(role.toLowerCase())) ?? false;
}

/** Restoran xodimi (menejer yoki host) */
export function isStaff(user: { roles: string[] } | null): boolean {
  return user?.roles?.some(role => role.toLowerCase() === 'staff') ?? false;
}

/** Boshqaruv paneliga (/manager) kira oladimi: egasi, xodim yoki superadmin */
export function canManage(user: { roles: string[] } | null): boolean {
  return isAdmin(user) || isStaff(user);
}
