import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Switch } from '@/components/ui/switch';
import { tablesApi, ApiError, type RestaurantTable } from '@/lib/api';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, Table2, Loader2, Users } from 'lucide-react';

interface Props {
  restaurantId: string;
}

const LOCATIONS = [
  { value: 'hall', label: 'Zal' },
  { value: 'terrace', label: 'Terassa' },
  { value: 'vip', label: 'VIP' },
  { value: 'garden', label: "Bog'" },
  { value: 'family', label: 'Oilaviy xona' },
];

const CAPACITIES = [2, 4, 6, 8];

/**
 * CRUD for restaurant tables. Allows creating 2/4/6/8-seat tables
 * in different zones (hall/terrace/vip/garden).
 */
export const AdminTablesManager = ({ restaurantId }: Props) => {
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RestaurantTable | null>(null);
  const [label, setLabel] = useState('');
  const [capacity, setCapacity] = useState(4);
  const [location, setLocation] = useState('hall');
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const list = await tablesApi.list(restaurantId);
      setTables(list);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setLoading(false);
    }
  }, [restaurantId]);

  useEffect(() => {
    if (restaurantId) load();
  }, [restaurantId, load]);

  const openNew = () => {
    setEditing(null);
    setLabel('');
    setCapacity(4);
    setLocation('hall');
    setIsActive(true);
    setDialogOpen(true);
  };

  const openEdit = (t: RestaurantTable) => {
    setEditing(t);
    setLabel(t.label);
    setCapacity(t.capacity);
    setLocation(t.location);
    setIsActive(t.isActive);
    setDialogOpen(true);
  };

  const save = async () => {
    if (!label.trim()) {
      toast.error('Stol nomini kiriting (masalan T-1)');
      return;
    }
    setSaving(true);
    try {
      if (editing) {
        await tablesApi.update(restaurantId, editing.id, {
          label: label.trim(),
          capacity,
          location,
          isActive,
        });
      } else {
        await tablesApi.create(restaurantId, {
          label: label.trim(),
          capacity,
          location,
        });
      }
      toast.success('Saqlandi');
      setDialogOpen(false);
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (t: RestaurantTable) => {
    if (!confirm(`${t.label} stolini o'chirasizmi?`)) return;
    try {
      await tablesApi.delete(restaurantId, t.id);
      toast.success("O'chirildi");
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  const byLocation = LOCATIONS.map(loc => ({
    ...loc,
    tables: tables.filter(t => t.location === loc.value),
  }));

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="w-6 h-6 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-display text-lg font-bold text-foreground">Stollar</h3>
          <p className="text-sm text-muted-foreground">
            Zal, terrasa, VIP va bog stollarini boshqaring. Jami: {tables.length}
          </p>
        </div>
        <Button variant="hero" onClick={openNew}>
          <Plus className="w-4 h-4 mr-1" /> Stol
        </Button>
      </div>

      {byLocation.map(group => (
        <div key={group.value} className="space-y-3">
          <h4 className="font-display text-md font-semibold text-foreground">
            {group.label}{' '}
            <span className="text-sm font-normal text-muted-foreground">({group.tables.length})</span>
          </h4>
          {group.tables.length === 0 ? (
            <div className="bg-card border border-border rounded-xl p-6 text-center text-sm text-muted-foreground">
              Bu zonada stol yo'q
            </div>
          ) : (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              {group.tables.map(t => (
                <div
                  key={t.id}
                  className={`bg-card border rounded-xl p-4 relative transition-colors ${
                    t.isActive ? 'border-border' : 'border-dashed border-muted opacity-60'
                  }`}
                >
                  <div className="flex items-center gap-2 mb-2">
                    <Table2 className="w-4 h-4 text-primary" />
                    <span className="font-display font-bold text-foreground">{t.label}</span>
                  </div>
                  <div className="flex items-center gap-1 text-muted-foreground text-sm mb-3">
                    <Users className="w-3.5 h-3.5" /> {t.capacity} mehmon
                  </div>
                  <div className="flex gap-1">
                    <Button size="sm" variant="ghost" className="flex-1" onClick={() => openEdit(t)}>
                      <Pencil className="w-3 h-3" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="flex-1 text-destructive hover:text-destructive"
                      onClick={() => remove(t)}
                    >
                      <Trash2 className="w-3 h-3" />
                    </Button>
                  </div>
                  {!t.isActive && (
                    <span className="absolute top-2 right-2 text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-muted text-muted-foreground">
                      Nofaol
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      ))}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="bg-card border-border">
          <DialogHeader>
            <DialogTitle className="font-display text-foreground">
              {editing ? 'Stolni tahrirlash' : 'Yangi stol'}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Nomi (masalan T-1)</Label>
              <Input value={label} onChange={e => setLabel(e.target.value)} className="bg-secondary" />
            </div>
            <div>
              <Label>Sigʻimi</Label>
              <div className="grid grid-cols-4 gap-2">
                {CAPACITIES.map(c => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setCapacity(c)}
                    className={`py-2 rounded-md border text-sm font-medium transition-all ${
                      capacity === c
                        ? 'border-primary bg-primary/15 text-primary'
                        : 'border-border bg-secondary text-muted-foreground'
                    }`}
                  >
                    {c}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <Label>Zona</Label>
              <div className="grid grid-cols-2 gap-2">
                {LOCATIONS.map(l => (
                  <button
                    key={l.value}
                    type="button"
                    onClick={() => setLocation(l.value)}
                    className={`py-2 rounded-md border text-sm font-medium transition-all ${
                      location === l.value
                        ? 'border-primary bg-primary/15 text-primary'
                        : 'border-border bg-secondary text-muted-foreground'
                    }`}
                  >
                    {l.label}
                  </button>
                ))}
              </div>
            </div>
            {editing && (
              <div className="flex items-center justify-between">
                <Label htmlFor="active">Faol</Label>
                <Switch id="active" checked={isActive} onCheckedChange={setIsActive} />
              </div>
            )}
            <Button variant="hero" onClick={save} disabled={saving} className="w-full">
              {saving ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : null}
              Saqlash
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
};
