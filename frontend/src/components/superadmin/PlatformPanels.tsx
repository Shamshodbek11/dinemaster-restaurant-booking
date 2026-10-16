import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  superadminApi, venuesApi, ApiError,
  type AuditLogEntry, type PartnerApplication, type Restaurant, type SuperadminVenueReservation,
} from '@/lib/api';
import { Pager, PanelHeader, Select, StatusBadge, Table } from './common';
import { PAGE_SIZE, dmy, dateTime, money, useDebounced } from './utils';
import { CheckCircle2, ChevronDown, ChevronRight, Loader2, Search, Send, XCircle } from 'lucide-react';
import { toast } from 'sonner';

// ───────────────────────────── Zal bronlari ─────────────────────────────

export const VenueReservationsPanel = () => {
  const [status, setStatus] = useState('pending');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: SuperadminVenueReservation[] }>({ total: 0, items: [] });
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.venueReservations({ status, page, size: PAGE_SIZE });
      setData({ total: res.total, items: res.items });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [status, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [status]);

  const setResStatus = async (id: string, s: 'confirmed' | 'cancelled' | 'completed') => {
    try {
      await venuesApi.setReservationStatus(id, s);
      toast.success('Holat yangilandi, mijozga xabar yuborildi');
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  return (
    <div>
      <PanelHeader title="Zal bronlari" subtitle="To'y va tadbirlar uchun so'rovlar — tasdiqlang yoki rad eting" />
      <div className="flex items-center gap-3 mb-4">
        <Select label="Holat" value={status} onChange={setStatus} options={[
          { value: 'pending', label: 'Kutilmoqda' }, { value: 'confirmed', label: 'Tasdiqlangan' },
          { value: 'completed', label: 'Yakunlangan' }, { value: 'cancelled', label: 'Bekor qilingan' }, { value: '', label: 'Hammasi' },
        ]} />
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>
      <Table head={['Tadbir sanasi', 'Zal', 'Mijoz', 'Mehmon', 'Narx', 'Holat', 'Amallar']} empty={!loading && data.items.length === 0}>
        {data.items.map(r => (
          <tr key={r.id} className="border-b border-border/50 align-top">
            <td className="p-3 whitespace-nowrap">{dmy(r.eventDate)}<div className="text-xs text-muted-foreground">{r.eventType}</div></td>
            <td className="p-3">{r.venueName}</td>
            <td className="p-3"><div>{r.userFullName}</div><div className="text-xs text-muted-foreground">{r.userEmail} {r.userPhone}</div>
              {r.specialRequests && <div className="text-xs italic text-muted-foreground">"{r.specialRequests}"</div>}</td>
            <td className="p-3">{r.guestsCount}</td>
            <td className="p-3">{money(r.totalPrice)}</td>
            <td className="p-3"><StatusBadge status={r.status} /></td>
            <td className="p-3">
              <div className="flex flex-wrap gap-1">
                {r.status === 'pending' && <Button size="sm" variant="hero" onClick={() => setResStatus(r.id, 'confirmed')}>Tasdiqlash</Button>}
                {r.status === 'confirmed' && <Button size="sm" variant="outline" onClick={() => setResStatus(r.id, 'completed')}>Yakunlandi</Button>}
                {(r.status === 'pending' || r.status === 'confirmed') &&
                  <Button size="sm" variant="ghost" className="text-destructive" onClick={() => setResStatus(r.id, 'cancelled')}>Bekor</Button>}
              </div>
            </td>
          </tr>
        ))}
      </Table>
      <Pager page={page} total={data.total} onPage={setPage} />
    </div>
  );
};

// ───────────────────────────── Hamkorlik arizalari ─────────────────────────────

export const PartnerApplicationsPanel = () => {
  const [status, setStatus] = useState('pending');
  const [items, setItems] = useState<PartnerApplication[]>([]);
  const [loading, setLoading] = useState(true);
  const [rejecting, setRejecting] = useState<string | null>(null);
  const [reason, setReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await superadminApi.partnerApplications(status || undefined));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => { load(); }, [load]);

  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn();
      toast.success(ok);
      setRejecting(null);
      setReason('');
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  return (
    <div>
      <PanelHeader title="Hamkorlik arizalari" subtitle="«Restoran egasimisiz?» orqali kelgan arizalar. Tasdiqlansa — foydalanuvchiga restoran egasi roli beriladi." />
      <div className="flex items-center gap-3 mb-4">
        <Select label="Holat" value={status} onChange={setStatus} options={[
          { value: 'pending', label: 'Kutilmoqda' }, { value: 'approved', label: 'Tasdiqlangan' },
          { value: 'rejected', label: 'Rad etilgan' }, { value: '', label: 'Hammasi' },
        ]} />
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>
      {!loading && items.length === 0 && <p className="text-center text-muted-foreground py-10">Ariza yo'q.</p>}
      <div className="space-y-3">
        {items.map(a => (
          <div key={a.id} className="bg-card rounded-xl border border-border p-5 space-y-3">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="font-display text-lg font-bold text-foreground">{a.restaurantName}</p>
                <p className="text-sm text-muted-foreground">{a.city}{a.address && `, ${a.address}`} · {a.phone}</p>
                <p className="text-sm">{a.userFullName} — {a.userEmail}</p>
                {a.message && <p className="text-sm italic text-muted-foreground mt-1">"{a.message}"</p>}
                <p className="text-xs text-muted-foreground mt-1">{dateTime(a.createdAt)}</p>
                {a.rejectionReason && <p className="text-xs text-destructive">Sabab: {a.rejectionReason}</p>}
              </div>
              <div className="flex items-center gap-2">
                <StatusBadge status={a.status} />
                {a.status === 'pending' && (
                  <>
                    <Button size="sm" variant="hero" onClick={() => act(() => superadminApi.approvePartner(a.id), 'Tasdiqlandi — admin roli berildi')}>
                      <CheckCircle2 className="w-4 h-4 mr-1" /> Tasdiqlash
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setRejecting(a.id)}><XCircle className="w-4 h-4 mr-1" /> Rad etish</Button>
                  </>
                )}
              </div>
            </div>
            {rejecting === a.id && (
              <div className="space-y-2">
                <Textarea value={reason} onChange={e => setReason(e.target.value)} rows={2} className="bg-secondary" placeholder="Sabab (arizachiga yuboriladi)" />
                <div className="flex gap-2">
                  <Button size="sm" variant="destructive" onClick={() => act(() => superadminApi.rejectPartner(a.id, reason), 'Rad etildi')}>Rad etish</Button>
                  <Button size="sm" variant="ghost" onClick={() => setRejecting(null)}>Bekor</Button>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};

// ───────────────────────────── Faoliyat jurnali ─────────────────────────────

const ENTITY_TYPES = [
  { value: '', label: 'Barcha obyektlar' }, { value: 'restaurant', label: 'Restoran sozlamalari' },
  { value: 'menu_item', label: 'Menyu taomlari' }, { value: 'menu_category', label: 'Menyu kategoriyalari' },
  { value: 'table', label: 'Stollar' }, { value: 'reservation', label: 'Bronlar' }, { value: 'review', label: 'Sharhlar' },
  { value: 'venue', label: 'Zallar' }, { value: 'venue_reservation', label: 'Zal bronlari' },
  { value: 'user', label: 'Foydalanuvchilar' }, { value: 'partner_application', label: 'Hamkorlik' },
  { value: 'notification', label: 'Ommaviy xabarlar' },
];

const formatValue = (v: unknown) => {
  if (v === null || v === undefined || v === '') return '—';
  if (Array.isArray(v)) return v.join(', ') || '—';
  if (typeof v === 'boolean') return v ? 'ha' : "yo'q";
  return String(v);
};

/** Admin va superadminlar qilgan barcha o'zgarishlar: kim, qachon, nima (eski → yangi). */
export const ActivityLogPanel = ({ restaurants }: { restaurants: Restaurant[] }) => {
  const [entityType, setEntityType] = useState('');
  const [actorRole, setActorRole] = useState('');
  const [restaurantId, setRestaurantId] = useState('');
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: AuditLogEntry[] }>({ total: 0, items: [] });
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState<Record<string, boolean>>({});

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await superadminApi.auditLog({ entityType, actorRole, restaurantId, search: debounced, page, size: PAGE_SIZE });
      setData({ total: res.total, items: res.items });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Yuklab bo'lmadi");
    } finally {
      setLoading(false);
    }
  }, [entityType, actorRole, restaurantId, debounced, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [entityType, actorRole, restaurantId, debounced]);

  return (
    <div>
      <PanelHeader title="Faoliyat jurnali" subtitle="Restoran egalari va superadminlar qilgan barcha o'zgarishlar: menyu narxi, depozit, stollar, sozlamalar, statuslar" />
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <Select label="Obyekt" value={entityType} onChange={setEntityType} options={ENTITY_TYPES} />
        <Select label="Kim" value={actorRole} onChange={setActorRole} options={[
          { value: '', label: 'Hamma' }, { value: 'admin', label: 'Restoran egalari' },
          { value: 'superadmin', label: 'Superadminlar' }, { value: 'system', label: 'Tizim' },
        ]} />
        <Select label="Restoran" value={restaurantId} onChange={setRestaurantId}
          options={[{ value: '', label: 'Barcha restoranlar' }, ...restaurants.map(r => ({ value: r.id, label: r.name }))]} />
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input value={search} onChange={e => setSearch(e.target.value)} placeholder="Qidirish (masalan: narx, Plov)..." className="pl-9 bg-secondary" />
        </div>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-muted-foreground" />}
      </div>

      {!loading && data.items.length === 0 && <p className="text-center text-muted-foreground py-10">Yozuv yo'q.</p>}
      <div className="bg-card rounded-xl border border-border divide-y divide-border/60">
        {data.items.map(a => {
          const changes: Record<string, { old: unknown; new: unknown }> | null = a.changes ? JSON.parse(a.changes) : null;
          const expanded = !!open[a.id];
          return (
            <div key={a.id} className="p-4">
              <button className="w-full text-left flex gap-3" onClick={() => changes && setOpen(o => ({ ...o, [a.id]: !expanded }))}>
                <span className="mt-0.5 w-4 shrink-0 text-muted-foreground">
                  {changes ? (expanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />) : null}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block text-sm text-foreground">{a.summary}</span>
                  <span className="block text-xs text-muted-foreground mt-0.5">
                    {dateTime(a.createdAt)} · {a.actorName ?? 'Tizim'}{a.actorEmail && ` (${a.actorEmail})`} · {a.actorRole}
                    {a.restaurantName && ` · ${a.restaurantName}`}
                  </span>
                </span>
              </button>
              {expanded && changes && (
                <table className="mt-2 ml-7 text-xs">
                  <tbody>
                    {Object.entries(changes).map(([field, c]) => (
                      <tr key={field}>
                        <td className="pr-4 py-0.5 text-muted-foreground">{field}</td>
                        <td className="pr-2 py-0.5 line-through text-muted-foreground max-w-xs truncate">{formatValue(c.old)}</td>
                        <td className="py-0.5 text-foreground max-w-xs truncate">→ {formatValue(c.new)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          );
        })}
      </div>
      <Pager page={page} total={data.total} onPage={setPage} />
    </div>
  );
};

// ───────────────────────────── Ommaviy xabar ─────────────────────────────

export const BroadcastPanel = () => {
  const [audience, setAudience] = useState<'all' | 'users' | 'admins'>('all');
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [link, setLink] = useState('');
  const [sending, setSending] = useState(false);

  const send = async () => {
    if (!title.trim()) return;
    if (!confirm('Xabar tanlangan barcha foydalanuvchilarga yuboriladi. Davom etasizmi?')) return;
    setSending(true);
    try {
      const res = await superadminApi.broadcast({ audience, title: title.trim(), body: body.trim() || undefined, link: link.trim() || undefined });
      toast.success(`${res.sent} kishiga yuborildi`);
      setTitle(''); setBody(''); setLink('');
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="max-w-xl">
      <PanelHeader title="Ommaviy xabar" subtitle="Ilova ichidagi bildirishnoma (qo'ng'iroqcha) sifatida keladi" />
      <div className="bg-card rounded-xl border border-border p-5 space-y-3">
        <Select label="Kimga" value={audience} onChange={v => setAudience(v as typeof audience)} options={[
          { value: 'all', label: 'Hammaga' }, { value: 'users', label: 'Faqat mijozlarga' }, { value: 'admins', label: 'Faqat restoran egalariga' },
        ]} />
        <Input value={title} onChange={e => setTitle(e.target.value)} placeholder="Sarlavha" maxLength={200} className="bg-secondary" />
        <Textarea value={body} onChange={e => setBody(e.target.value)} placeholder="Matn" rows={4} className="bg-secondary" />
        <Input value={link} onChange={e => setLink(e.target.value)} placeholder="Havola (ixtiyoriy), masalan /offers" className="bg-secondary" />
        <Button variant="hero" onClick={send} disabled={sending || !title.trim()}>
          {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <><Send className="w-4 h-4 mr-2" /> Yuborish</>}
        </Button>
      </div>
    </div>
  );
};
