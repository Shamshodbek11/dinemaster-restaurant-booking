import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { authApi, tokenStore, ApiError } from '@/lib/api';
import { toast } from 'sonner';
import { Shield, Lock, CheckCircle2, AlertTriangle } from 'lucide-react';

/**
 * Bu sahifa faqat tizimda hech qanday superadmin yo'q bo'lganida ishlaydi.
 * Backend `/api/auth/setup-superadmin` endpoint'i avtomatik tekshiradi —
 * agar superadmin allaqachon mavjud bo'lsa, so'rov rad etiladi.
 */

const SetupPage = () => {
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [secretKey, setSecretKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const [alreadySetup, setAlreadySetup] = useState(false);

  // Frontend tomonida oddiy "qo'shimcha himoya" uchun maxfiy kalit.
  // Backend esa superadmin mavjudligini o'zi tekshiradi.
  const SETUP_SECRET = 'dinemaster-setup-2026';

  const handleSetup = async (e: React.FormEvent) => {
    e.preventDefault();

    if (secretKey !== SETUP_SECRET) {
      toast.error("Maxfiy kalit noto'g'ri!");
      return;
    }

    if (password.length < 8) {
      toast.error("Parol kamida 8 ta belgidan iborat bo'lishi kerak");
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.setupSuperadmin({ email, password, fullName });
      // Avtomatik login qilmaymiz — login sahifasiga yo'naltiramiz
      // Lekin tokenni saqlab qo'yamiz, kerak bo'lsa
      tokenStore.set(res.accessToken, res.expiresAt);
      setDone(true);
      toast.success('Superadmin muvaffaqiyatli yaratildi!');
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 400 && err.message.toLowerCase().includes('allaqachon')) {
          setAlreadySetup(true);
          return;
        }
        toast.error(err.message);
      } else {
        toast.error('Xatolik yuz berdi');
      }
    } finally {
      setLoading(false);
    }
  };

  if (alreadySetup) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center px-4">
        <div className="text-center max-w-sm">
          <div className="w-16 h-16 rounded-full bg-amber-500/20 flex items-center justify-center mx-auto mb-4">
            <AlertTriangle className="w-8 h-8 text-amber-400" />
          </div>
          <h1 className="font-display text-2xl font-bold text-foreground mb-2">
            Setup allaqachon bajarilgan
          </h1>
          <p className="text-muted-foreground mb-6">
            Tizimda superadmin mavjud. Bu sahifaga kirish mumkin emas.
          </p>
          <Button variant="hero" onClick={() => navigate('/login')}>
            Login sahifasiga o'tish
          </Button>
        </div>
      </div>
    );
  }

  if (done) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center px-4">
        <div className="text-center max-w-sm">
          <div className="w-16 h-16 rounded-full bg-green-500/20 flex items-center justify-center mx-auto mb-4">
            <CheckCircle2 className="w-8 h-8 text-green-400" />
          </div>
          <h1 className="font-display text-2xl font-bold text-foreground mb-2">
            Superadmin yaratildi!
          </h1>
          <p className="text-muted-foreground mb-2">Email: <strong className="text-foreground">{email}</strong></p>
          <p className="text-muted-foreground mb-6 text-sm">
            Endi login sahifasiga o'tib, yaratilgan email va parol bilan kiring.
          </p>
          <Button variant="hero" onClick={() => { tokenStore.clear(); navigate('/login'); }}>
            Login sahifasiga o'tish
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-purple-600 to-purple-400 flex items-center justify-center mx-auto mb-4 shadow-lg">
            <Shield className="w-7 h-7 text-white" />
          </div>
          <h1 className="font-display text-3xl font-bold text-foreground">
            Tizim sozlamasi
          </h1>
          <p className="text-muted-foreground mt-2 text-sm">
            Birinchi superadmin hisobini yarating
          </p>
        </div>

        <div className="flex items-start gap-3 bg-amber-500/10 border border-amber-500/30 rounded-xl p-4 mb-6">
          <Lock className="w-4 h-4 text-amber-400 mt-0.5 shrink-0" />
          <p className="text-amber-400 text-sm">
            Bu sahifa faqat bitta marta ishlatiladi. Superadmin yaratilgandan so'ng bu sahifa avtomatik bloklanadi.
          </p>
        </div>

        <form onSubmit={handleSetup} className="space-y-4 bg-card rounded-2xl p-8 border border-border">
          <div className="space-y-2">
            <Label>To'liq ism</Label>
            <Input
              value={fullName}
              onChange={e => setFullName(e.target.value)}
              placeholder="Super Admin"
              required
              className="bg-secondary"
            />
          </div>

          <div className="space-y-2">
            <Label>Email</Label>
            <Input
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="superadmin@dinemaster.uz"
              required
              className="bg-secondary"
            />
          </div>

          <div className="space-y-2">
            <Label>Parol <span className="text-muted-foreground text-xs">(kamida 8 belgi)</span></Label>
            <Input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="••••••••"
              required
              className="bg-secondary"
            />
          </div>

          <div className="space-y-2">
            <Label>
              Maxfiy kalit
              <span className="text-muted-foreground text-xs ml-2">(setup uchun)</span>
            </Label>
            <Input
              type="password"
              value={secretKey}
              onChange={e => setSecretKey(e.target.value)}
              placeholder="••••••••••••••"
              required
              className="bg-secondary"
            />
            <p className="text-xs text-muted-foreground">
              Maxfiy kalit: <code className="bg-secondary px-1 rounded">dinemaster-setup-2026</code>
            </p>
          </div>

          <Button
            type="submit"
            className="w-full bg-purple-600 hover:bg-purple-500 text-white font-bold"
            disabled={loading}
          >
            {loading ? 'Yaratilmoqda...' : 'Superadmin yaratish'}
          </Button>
        </form>

        <p className="text-center text-muted-foreground text-xs mt-4">
          Allaqachon hisobingiz bormi?{' '}
          <button onClick={() => navigate('/login')} className="text-primary hover:underline">
            Login
          </button>
        </p>
      </div>
    </div>
  );
};

export default SetupPage;
