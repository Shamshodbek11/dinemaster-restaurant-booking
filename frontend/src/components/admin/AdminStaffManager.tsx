import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { staffApi, ApiError, type StaffMember } from '@/lib/api';
import { BRAND } from '@/lib/brand';
import { toast } from 'sonner';
import { Loader2, Trash2, UserPlus, Users } from 'lucide-react';

const ROLE_INFO: Record<StaffMember['role'], { label: string; desc: string }> = {
  manager: {
    label: 'Menejer',
    desc: "Bronlar, menyu, stollar, chegirmalar, sharhlarga javob, restoran sozlamalari va hisobotlar. Xodim qo'shish va restoranni o'chirish — faqat egasida.",
  },
  host: {
    label: 'Host (administrator)',
    desc: "Bronlarni ko'rish, mehmonni kutib olish (Keldi / Kelmadi), stol biriktirish, bekor qilish va kutish ro'yxati. Sozlama va hisobotlar yopiq.",
  },
};

export const AdminStaffManager = ({ restaurantId }: { restaurantId: string }) => {
  const [staff, setStaff] = useState<StaffMember[] | null>(null);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<StaffMember['role']>('host');
  const [saving, setSaving] = useState(false);

  const load = useCallback(() => {
    staffApi.list(restaurantId).then(setStaff).catch(err => {
      setStaff([]);
      toast.error(err instanceof ApiError ? err.message : "Xodimlarni yuklab bo'lmadi");
    });
  }, [restaurantId]);

  useEffect(() => { load(); }, [load]);

  const add = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await staffApi.add(restaurantId, { email: email.trim(), role });
      toast.success("Xodim qo'shildi — u endi boshqaruv paneliga kira oladi");
      setEmail('');
      load();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Qo'shib bo'lmadi");
    } finally {
      setSaving(false);
    }
  };

  const changeRole = async (s: StaffMember, newRole: StaffMember['role']) => {
    try {
      await staffApi.add(restaurantId, { email: s.email, role: newRole });
      setStaff(prev => prev?.map(x => (x.id === s.id ? { ...x, role: newRole } : x)) ?? null);
      toast.success('Rol o\'zgartirildi');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Xatolik');
    }
  };

  const remove = async (s: StaffMember) => {
    if (!confirm(`${s.fullName || s.email} xodimlar ro'yxatidan olib tashlansinmi?`)) return;
    try {
      await staffApi.remove(restaurantId, s.id);
      setStaff(prev => prev?.filter(x => x.id !== s.id) ?? null);
      toast.success('Xodim olib tashlandi');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Xatolik');
    }
  };

  return (
    <div className="max-w-3xl space-y-6">
      <div>
        <h3 className="font-display text-2xl font-bold text-foreground">Xodimlar</h3>
        <p className="text-sm text-muted-foreground">
          Menejer va hostlarga o'z hisobi bilan kirish huquqini bering — parolingizni bo'lishish shart emas.
        </p>
      </div>

      <form onSubmit={add} className="bg-card rounded-xl border border-border p-5 space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-3 items-end">
          <div className="space-y-2">
            <Label htmlFor="staff-email">Xodimning emaili</Label>
            <Input id="staff-email" type="email" required value={email} onChange={e => setEmail(e.target.value)}
              placeholder="xodim@example.com" className="bg-secondary" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="staff-role">Rol</Label>
            <select id="staff-role" value={role} onChange={e => setRole(e.target.value as StaffMember['role'])}
              className="h-10 rounded-md border border-border bg-secondary px-3 text-sm text-foreground w-full sm:w-52">
              <option value="host">{ROLE_INFO.host.label}</option>
              <option value="manager">{ROLE_INFO.manager.label}</option>
            </select>
          </div>
        </div>
        <p className="text-xs text-muted-foreground">{ROLE_INFO[role].desc}</p>
        <p className="text-xs text-muted-foreground">Xodim avval {BRAND}'da shu email bilan ro'yxatdan o'tgan bo'lishi kerak.</p>
        <Button type="submit" variant="hero" disabled={saving}>
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <><UserPlus className="w-4 h-4 mr-1" /> Qo'shish</>}
        </Button>
      </form>

      {staff === null ? (
        <div className="h-24 rounded-xl bg-card border border-border animate-pulse" />
      ) : staff.length === 0 ? (
        <div className="text-center py-10 text-muted-foreground">
          <Users className="w-10 h-10 mx-auto mb-2 opacity-40" />
          <p>Hozircha xodim qo'shilmagan</p>
        </div>
      ) : (
        <div className="space-y-2">
          {staff.map(s => (
            <div key={s.id} className="bg-card rounded-xl border border-border p-4 flex flex-wrap items-center gap-3">
              <div className="flex-1 min-w-[12rem]">
                <p className="font-medium text-foreground">{s.fullName || s.email}</p>
                <p className="text-sm text-muted-foreground">{s.email}</p>
              </div>
              <select value={s.role} onChange={e => changeRole(s, e.target.value as StaffMember['role'])} aria-label="Rol"
                className="h-9 rounded-md border border-border bg-secondary px-2 text-sm text-foreground">
                <option value="host">{ROLE_INFO.host.label}</option>
                <option value="manager">{ROLE_INFO.manager.label}</option>
              </select>
              <Button variant="ghost" size="icon" onClick={() => remove(s)} aria-label="Olib tashlash">
                <Trash2 className="w-4 h-4 text-destructive" />
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
