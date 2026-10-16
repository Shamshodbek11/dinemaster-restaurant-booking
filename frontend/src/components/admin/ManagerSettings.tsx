import { useApp, type ThemeMode } from '@/contexts/AppContext';
import { TelegramCard } from '@/components/profile/TelegramCard';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { profileApi, ApiError } from '@/lib/api';
import { toast } from 'sonner';
import { useState } from 'react';
import { Sun, Moon, Monitor, User, Bell, ShieldCheck, Languages } from 'lucide-react';
import type { Language } from '@/lib/i18n';

const LANGS: { value: Language; label: string; flag: string }[] = [
  { value: 'uz', label: "O'zbekcha", flag: '🇺🇿' },
  { value: 'ru', label: 'Русский', flag: '🇷🇺' },
  { value: 'en', label: 'English', flag: '🇬🇧' },
];

export const ManagerSettings = () => {
  const { language, setLanguage, darkMode, theme, setTheme, profile, refreshProfile } = useApp();
  const themes: { mode: ThemeMode; label: string; icon: typeof Sun }[] = [
    { mode: 'system', label: 'Qurilma bo\'yicha', icon: Monitor },
    { mode: 'light', label: 'Yorug\'', icon: Sun },
    { mode: 'dark', label: 'Qorong\'i', icon: Moon },
  ];

  const [fullName, setFullName] = useState(profile?.fullName ?? '');
  const [phone, setPhone] = useState(profile?.phone ?? '');
  const [savingProfile, setSavingProfile] = useState(false);

  // Notifikatsiyalar (localStorage'da saqlanadi)
  const [browserNotif, setBrowserNotif] = useState(() => localStorage.getItem('dm-notif-browser') === 'true');

  const toggleNotif = (kind: 'browser', val: boolean) => {
    localStorage.setItem(`dm-notif-${kind}`, String(val));
    if (kind === 'browser') {
      setBrowserNotif(val);
      if (val && 'Notification' in window && Notification.permission !== 'granted') {
        Notification.requestPermission();
      }
    }
  };

  const saveProfile = async () => {
    setSavingProfile(true);
    try {
      await profileApi.update({
        fullName: fullName.trim() || profile?.fullName || '',
        phone: phone.trim() || undefined,
        preferredLanguage: language,
      });
      await refreshProfile();
      toast.success('Profil saqlandi');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Saqlab bo\'lmadi');
    } finally {
      setSavingProfile(false);
    }
  };

  return (
    <div className="max-w-3xl space-y-6">
      <div>
        <h3 className="font-display text-2xl font-bold text-foreground">Sozlamalar</h3>
        <p className="text-muted-foreground text-sm">Profil, ko'rinish, til va bildirishnomalar</p>
      </div>

      {/* APPEARANCE */}
      <section className="bg-card rounded-xl border border-border p-6 space-y-5">
        <div className="flex items-center gap-2">
          {darkMode ? <Moon className="w-5 h-5 text-primary" /> : <Sun className="w-5 h-5 text-primary" />}
          <h4 className="font-display text-lg font-bold text-foreground">Ko'rinish</h4>
        </div>
        <div className="grid grid-cols-3 gap-2">
          {themes.map(o => (
            <button key={o.mode} onClick={() => setTheme(o.mode)}
              className={`flex flex-col items-center gap-1 py-3 px-2 rounded-xl border transition-all ${
                theme === o.mode ? 'border-primary bg-primary/10' : 'border-border bg-secondary hover:border-primary/30'}`}>
              <o.icon className="w-5 h-5 text-foreground" />
              <span className="text-xs font-medium text-foreground text-center">{o.label}</span>
            </button>
          ))}
        </div>
      </section>

      {/* LANGUAGE */}
      <section className="bg-card rounded-xl border border-border p-6 space-y-4">
        <div className="flex items-center gap-2">
          <Languages className="w-5 h-5 text-primary" />
          <h4 className="font-display text-lg font-bold text-foreground">Til</h4>
        </div>
        <div className="grid grid-cols-3 gap-2">
          {LANGS.map(l => (
            <button
              key={l.value}
              onClick={() => setLanguage(l.value)}
              className={`flex flex-col items-center gap-1 py-3 px-2 rounded-xl border transition-all ${
                language === l.value
                  ? 'border-primary bg-primary/10'
                  : 'border-border bg-secondary hover:border-primary/30'
              }`}
            >
              <span className="text-2xl">{l.flag}</span>
              <span className="text-xs font-medium text-foreground">{l.label}</span>
            </button>
          ))}
        </div>
      </section>

      {/* PROFILE */}
      <section className="bg-card rounded-xl border border-border p-6 space-y-4">
        <div className="flex items-center gap-2">
          <User className="w-5 h-5 text-primary" />
          <h4 className="font-display text-lg font-bold text-foreground">Profil ma'lumotlari</h4>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <Label>To'liq ism</Label>
            <Input value={fullName} onChange={e => setFullName(e.target.value)} className="bg-secondary" />
          </div>
          <div>
            <Label>Telefon</Label>
            <Input value={phone} onChange={e => setPhone(e.target.value)} className="bg-secondary" placeholder="+998..." />
          </div>
        </div>
        <Button variant="hero" onClick={saveProfile} disabled={savingProfile}>
          {savingProfile ? 'Saqlanmoqda...' : 'Profilni saqlash'}
        </Button>
      </section>

      {/* NOTIFICATIONS */}
      <section className="bg-card rounded-xl border border-border p-6 space-y-4">
        <div className="flex items-center gap-2">
          <Bell className="w-5 h-5 text-primary" />
          <h4 className="font-display text-lg font-bold text-foreground">Bildirishnomalar</h4>
        </div>
        <NotifRow
          label="Brauzer notifikatsiyalari"
          desc="Yangi xabar kelganda brauzer oynasida ko'rsatiladi (sayt ochiq bo'lsa)"
          value={browserNotif}
          onChange={v => toggleNotif('browser', v)}
        />
        <TelegramCard />
      </section>

      {/* ACCOUNT */}
      <section className="bg-card rounded-xl border border-border p-6 space-y-4">
        <div className="flex items-center gap-2">
          <ShieldCheck className="w-5 h-5 text-primary" />
          <h4 className="font-display text-lg font-bold text-foreground">Hisobingiz</h4>
        </div>
        <div className="text-sm text-muted-foreground space-y-1">
          <div>Email: <span className="text-foreground">{profile?.email}</span></div>
          <div>Rol: <span className="text-foreground">{(profile?.roles ?? []).map(r => ROLE_LABELS[r.toLowerCase()] ?? r).join(', ')}</span></div>
          <div>Yums balansi: <span className="text-foreground">{profile?.yumsBalance ?? 0}</span></div>
        </div>
      </section>
    </div>
  );
};

const ROLE_LABELS: Record<string, string> = {
  user: 'Mehmon', admin: 'Restoran egasi', staff: 'Restoran xodimi', superadmin: 'Superadmin',
};

const NotifRow = ({ label, desc, value, onChange }: { label: string; desc: string; value: boolean; onChange: (v: boolean) => void }) => (
  <div className="flex items-center justify-between">
    <div>
      <Label className="text-foreground">{label}</Label>
      <p className="text-xs text-muted-foreground">{desc}</p>
    </div>
    <Switch checked={value} onCheckedChange={onChange} />
  </div>
);
