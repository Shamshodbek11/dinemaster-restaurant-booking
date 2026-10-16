import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { restaurantsApi, ApiError, type Restaurant } from '@/lib/api';
import { toast } from 'sonner';
import { Store, ChevronRight, ChevronLeft, CheckCircle2 } from 'lucide-react';

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: (r: Restaurant) => void;
}

const CUISINES = ["O'zbek", 'Italyan', 'Yapon', 'Turk', 'Koreya', 'Fransuz', 'BBQ / Shashlik', 'Steakhouse', 'Yevropa', 'Boshqa'];
const PRICES = ['$', '$$', '$$$', '$$$$'];

export const RestaurantOnboardingDialog = ({ open, onOpenChange, onCreated }: Props) => {
  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);

  // Step 1: basic
  const [name, setName] = useState('');
  const [cuisine, setCuisine] = useState("O'zbek");
  const [priceRange, setPriceRange] = useState('$$');
  const [description, setDescription] = useState('');

  // Step 2: contact
  const [address, setAddress] = useState('');
  const [city, setCity] = useState('Toshkent');
  const [phone, setPhone] = useState('');
  const [totalSlots, setTotalSlots] = useState<number | ''>(20);

  // Step 3: location
  const [latitude, setLatitude] = useState<number | ''>('');
  const [longitude, setLongitude] = useState<number | ''>('');

  const reset = () => {
    setStep(1);
    setName(''); setDescription(''); setCuisine("O'zbek"); setPriceRange('$$');
    setAddress(''); setCity('Toshkent'); setPhone(''); setTotalSlots(20);
    setLatitude(''); setLongitude('');
    onOpenChange(false);
  };

  const canNextFromStep1 = name.trim().length >= 3 && description.trim().length >= 10;
  const canNextFromStep2 = address.trim().length >= 5 && phone.trim().length >= 9 && Number(totalSlots) > 0;

  const handleSubmit = async () => {
    setLoading(true);
    try {
      const created = await restaurantsApi.create({
        name: name.trim(),
        description: description.trim(),
        cuisineType: cuisine,
        priceRange,
        address: address.trim(),
        city: city.trim(),
        phone: phone.trim(),
        totalSlots: Number(totalSlots) || 20,
        latitude: typeof latitude === 'number' ? latitude : undefined,
        longitude: typeof longitude === 'number' ? longitude : undefined,
        amenities: [],
        policies: [],
        imagesUrl: [],
        openingHours: '{"mon":"10:00-23:00","tue":"10:00-23:00","wed":"10:00-23:00","thu":"10:00-23:00","fri":"10:00-23:00","sat":"10:00-23:00","sun":"11:00-23:00"}',
      });
      toast.success('Restoran yaratildi! Tasdiqlash uchun superadminga yuborildi.');
      onCreated(created);
      reset();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Restoran yaratib bo\'lmadi';
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  };

  const tryGetGeolocation = () => {
    if (!navigator.geolocation) {
      toast.error('Brauzer geolokatsiyani qo\'llab-quvvatlamaydi');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLatitude(Number(pos.coords.latitude.toFixed(6)));
        setLongitude(Number(pos.coords.longitude.toFixed(6)));
        toast.success('Joylashuv aniqlandi');
      },
      () => toast.error('Joylashuvni aniqlab bo\'lmadi')
    );
  };

  return (
    <Dialog open={open} onOpenChange={(v) => !loading && (v ? onOpenChange(true) : reset())}>
      <DialogContent className="sm:max-w-lg bg-card border-border">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center">
              <Store className="w-5 h-5 text-primary" />
            </div>
            <div>
              <DialogTitle>Restoraningizni yarating</DialogTitle>
              <DialogDescription>{step}/3 qadam</DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="flex gap-1 mt-2">
          {[1, 2, 3].map((s) => (
            <div key={s} className={`h-1 flex-1 rounded-full ${s <= step ? 'bg-primary' : 'bg-secondary'}`} />
          ))}
        </div>

        {step === 1 && (
          <div className="space-y-4">
            <div>
              <Label>Restoran nomi</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Masalan: La Bella Italia" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Oshxona turi</Label>
                <select
                  className="w-full mt-2 bg-secondary border border-border rounded-md h-10 px-3 text-foreground"
                  value={cuisine}
                  onChange={(e) => setCuisine(e.target.value)}
                >
                  {CUISINES.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
              <div>
                <Label>Narx darajasi</Label>
                <div className="flex gap-1 mt-2">
                  {PRICES.map((p) => (
                    <button
                      key={p}
                      type="button"
                      onClick={() => setPriceRange(p)}
                      className={`flex-1 h-10 rounded-md text-sm font-bold border transition ${priceRange === p ? 'bg-primary text-primary-foreground border-primary' : 'bg-secondary border-border'}`}
                    >
                      {p}
                    </button>
                  ))}
                </div>
              </div>
            </div>
            <div>
              <Label>Tavsif</Label>
              <Textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={3}
                placeholder="Restoraningiz haqida qisqacha — kamida 10 belgi"
              />
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            <div>
              <Label>Manzil</Label>
              <Input value={address} onChange={(e) => setAddress(e.target.value)} placeholder="Ko'cha, uy raqami" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Shahar</Label>
                <Input value={city} onChange={(e) => setCity(e.target.value)} />
              </div>
              <div>
                <Label>Telefon</Label>
                <Input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+998 71 ..." />
              </div>
            </div>
            <div>
              <Label>Bir kunda mehmon sig'imi</Label>
              <Input
                type="number"
                min={1}
                step={5}
                value={totalSlots === '' ? '' : totalSlots}
                onChange={(e) => {
                  const v = e.target.value;
                  if (v === '') setTotalSlots('');
                  else setTotalSlots(Math.max(1, Number(v)));
                }}
                placeholder="20"
              />
              <p className="text-xs text-muted-foreground mt-1">Kunlik maksimal qabul qilinadigan mehmonlar soni</p>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-4">
            <div className="bg-secondary rounded-lg p-4 text-sm text-muted-foreground">
              Geo-koordinatalar — restoraningizni xaritada ko'rsatish uchun. Brauzer orqali avtomatik aniqlash yoki qo'lda kiritish mumkin.
            </div>
            <Button variant="outline" className="w-full" onClick={tryGetGeolocation}>
              📍 Joriy joylashuvimni olish
            </Button>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Latitude</Label>
                <Input
                  type="number"
                  step="0.000001"
                  value={latitude === '' ? '' : latitude}
                  onChange={(e) => setLatitude(e.target.value === '' ? '' : Number(e.target.value))}
                  placeholder="41.2825"
                />
              </div>
              <div>
                <Label>Longitude</Label>
                <Input
                  type="number"
                  step="0.000001"
                  value={longitude === '' ? '' : longitude}
                  onChange={(e) => setLongitude(e.target.value === '' ? '' : Number(e.target.value))}
                  placeholder="69.2036"
                />
              </div>
            </div>
            <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-3 text-xs text-amber-500 flex items-start gap-2">
              <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
              <span>Yaratgandan so'ng restoraningiz <b>tasdiqlanmagan</b> holatda turadi. Superadmin tasdiqlagandan keyin u foydalanuvchilarga ko'rinadi.</span>
            </div>
          </div>
        )}

        <div className="flex gap-3 pt-2">
          {step > 1 && (
            <Button variant="outline" className="flex-1" onClick={() => setStep(step - 1)} disabled={loading}>
              <ChevronLeft className="w-4 h-4" /> Ortga
            </Button>
          )}
          {step < 3 && (
            <Button
              variant="hero"
              className="flex-1"
              disabled={(step === 1 && !canNextFromStep1) || (step === 2 && !canNextFromStep2)}
              onClick={() => setStep(step + 1)}
            >
              Davom ettirish <ChevronRight className="w-4 h-4" />
            </Button>
          )}
          {step === 3 && (
            <Button variant="hero" className="flex-1" onClick={handleSubmit} disabled={loading}>
              {loading ? '...' : 'Yaratish'}
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
};
