import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { MoneyInput } from '@/components/ui/money-input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Switch } from '@/components/ui/switch';
import {
  menuApi,
  ApiError,
  type MenuCategory,
  type MenuItem,
} from '@/lib/api';
import { toast } from 'sonner';
import {
  Plus,
  Pencil,
  Trash2,
  Flame,
  UtensilsCrossed,
  Loader2,
  Folder,
} from 'lucide-react';

interface Props {
  restaurantId: string;
}

/**
 * Admin menu builder — categories and items full CRUD.
 * Uses menuApi.all() to include inactive items.
 */
export const AdminMenuBuilder = ({ restaurantId }: Props) => {
  const [categories, setCategories] = useState<MenuCategory[]>([]);
  const [loading, setLoading] = useState(true);

  // Category dialog state
  const [catOpen, setCatOpen] = useState(false);
  const [editingCat, setEditingCat] = useState<MenuCategory | null>(null);
  const [catName, setCatName] = useState('');
  const [catSort, setCatSort] = useState(0);

  // Item dialog state
  const [itemOpen, setItemOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<MenuItem | null>(null);
  const [itemCategoryId, setItemCategoryId] = useState('');
  const [itemName, setItemName] = useState('');
  const [itemDesc, setItemDesc] = useState('');
  const [itemPrice, setItemPrice] = useState(0);
  const [itemImage, setItemImage] = useState('');
  const [itemAvailable, setItemAvailable] = useState(true);
  const [itemPopular, setItemPopular] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const list = await menuApi.all(restaurantId);
      setCategories(list);
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : "Yuklab bo'lmadi";
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, [restaurantId]);

  useEffect(() => {
    if (restaurantId) load();
  }, [restaurantId, load]);

  // -------- Category handlers --------
  const openNewCategory = () => {
    setEditingCat(null);
    setCatName('');
    setCatSort(categories.length);
    setCatOpen(true);
  };

  const openEditCategory = (c: MenuCategory) => {
    setEditingCat(c);
    setCatName(c.name);
    setCatSort(c.sortOrder);
    setCatOpen(true);
  };

  const saveCategory = async () => {
    if (!catName.trim()) {
      toast.error('Kategoriya nomini kiriting');
      return;
    }
    setSaving(true);
    try {
      if (editingCat) {
        await menuApi.updateCategory(restaurantId, editingCat.id, {
          name: catName.trim(),
          sortOrder: catSort,
        });
      } else {
        await menuApi.createCategory(restaurantId, {
          name: catName.trim(),
          sortOrder: catSort,
        });
      }
      toast.success('Saqlandi');
      setCatOpen(false);
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setSaving(false);
    }
  };

  const deleteCategory = async (c: MenuCategory) => {
    if (!confirm(`"${c.name}" kategoriyasini o'chirasizmi?`)) return;
    try {
      await menuApi.deleteCategory(restaurantId, c.id);
      toast.success("O'chirildi");
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

  // -------- Item handlers --------
  const openNewItem = (categoryId: string) => {
    setEditingItem(null);
    setItemCategoryId(categoryId);
    setItemName('');
    setItemDesc('');
    setItemPrice(0);
    setItemImage('');
    setItemAvailable(true);
    setItemPopular(false);
    setItemOpen(true);
  };

  const openEditItem = (item: MenuItem) => {
    setEditingItem(item);
    setItemCategoryId(item.categoryId);
    setItemName(item.name);
    setItemDesc(item.description);
    setItemPrice(item.price);
    setItemImage(item.imageUrl ?? '');
    setItemAvailable(item.isAvailable);
    setItemPopular(item.isPopular);
    setItemOpen(true);
  };

  const saveItem = async () => {
    if (!itemName.trim() || !itemCategoryId) {
      toast.error("Nom va kategoriya to'ldirilishi kerak");
      return;
    }
    setSaving(true);
    try {
      if (editingItem) {
        await menuApi.updateItem(restaurantId, editingItem.id, {
          categoryId: itemCategoryId,
          name: itemName.trim(),
          description: itemDesc.trim(),
          price: itemPrice,
          imageUrl: itemImage.trim(),
          isAvailable: itemAvailable,
          isPopular: itemPopular,
        });
      } else {
        await menuApi.createItem(restaurantId, {
          categoryId: itemCategoryId,
          name: itemName.trim(),
          description: itemDesc.trim() || undefined,
          price: itemPrice,
          imageUrl: itemImage.trim() || undefined,
          isAvailable: itemAvailable,
          isPopular: itemPopular,
        });
      }
      toast.success('Saqlandi');
      setItemOpen(false);
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setSaving(false);
    }
  };

  const deleteItem = async (item: MenuItem) => {
    if (!confirm(`"${item.name}" taomini o'chirasizmi?`)) return;
    try {
      const res = await menuApi.deleteItem(restaurantId, item.id);
      // Buyurtmalarda ishlatilgan taom o'chirilmaydi, faqat yashiriladi — backend xabarini ko'rsatamiz
      toast.success(res?.message ?? "O'chirildi");
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    }
  };

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
          <h3 className="font-display text-lg font-bold text-foreground">Menyu boshqaruvi</h3>
          <p className="text-sm text-muted-foreground">Kategoriya va taomlarni qo'shing, tahrirlang.</p>
        </div>
        <Button variant="hero" onClick={openNewCategory}>
          <Plus className="w-4 h-4 mr-1" /> Kategoriya
        </Button>
      </div>

      {categories.length === 0 ? (
        <div className="bg-card border border-border rounded-xl p-12 text-center">
          <UtensilsCrossed className="w-12 h-12 text-muted-foreground mx-auto mb-3 opacity-50" />
          <p className="text-muted-foreground mb-4">Menyu hali bo'sh</p>
          <Button onClick={openNewCategory}>Birinchi kategoriyani qo'shing</Button>
        </div>
      ) : (
        <div className="space-y-6">
          {categories.map(cat => (
            <div key={cat.id} className="bg-card border border-border rounded-xl overflow-hidden">
              <div className="flex items-center justify-between px-5 py-3 border-b border-border bg-secondary/30">
                <div className="flex items-center gap-2">
                  <Folder className="w-4 h-4 text-primary" />
                  <span className="font-medium text-foreground">{cat.name}</span>
                  <span className="text-xs text-muted-foreground">({cat.items?.length ?? 0})</span>
                </div>
                <div className="flex gap-1">
                  <Button size="sm" variant="ghost" onClick={() => openNewItem(cat.id)}>
                    <Plus className="w-3.5 h-3.5 mr-1" /> Taom
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => openEditCategory(cat)}>
                    <Pencil className="w-3.5 h-3.5" />
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-destructive hover:text-destructive"
                    onClick={() => deleteCategory(cat)}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </Button>
                </div>
              </div>
              <div className="divide-y divide-border">
                {(cat.items ?? []).map(item => (
                  <div key={item.id} className="px-5 py-3 flex items-center gap-4">
                    {item.imageUrl ? (
                      <img
                        src={item.imageUrl}
                        alt={item.name}
                        className="w-12 h-12 rounded-lg object-cover bg-secondary"
                        onError={(e) => {
                          const img = e.currentTarget;
                          img.style.display = 'none';
                          const next = img.nextElementSibling as HTMLElement | null;
                          if (next) next.style.display = 'flex';
                        }}
                      />
                    ) : (
                      <div className="w-12 h-12 rounded-lg bg-secondary flex items-center justify-center">
                        <UtensilsCrossed className="w-5 h-5 text-muted-foreground" />
                      </div>
                    )}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="text-foreground font-medium truncate">{item.name}</p>
                        {item.isPopular && (
                          <span className="flex items-center gap-0.5 text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-primary/15 text-primary">
                            <Flame className="w-3 h-3" /> Top
                          </span>
                        )}
                        {!item.isAvailable && (
                          <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-muted text-muted-foreground">
                            Yashirin
                          </span>
                        )}
                      </div>
                      {item.description && (
                        <p className="text-xs text-muted-foreground truncate">{item.description}</p>
                      )}
                    </div>
                    <div className="text-foreground font-bold shrink-0">
                      {item.price.toLocaleString('uz-UZ')} so'm
                    </div>
                    <div className="flex gap-1 shrink-0">
                      <Button size="sm" variant="ghost" onClick={() => openEditItem(item)}>
                        <Pencil className="w-3.5 h-3.5" />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-destructive hover:text-destructive"
                        onClick={() => deleteItem(item)}
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </Button>
                    </div>
                  </div>
                ))}
                {(cat.items ?? []).length === 0 && (
                  <div className="px-5 py-6 text-center text-sm text-muted-foreground">Taom qo'shing</div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Category dialog */}
      <Dialog open={catOpen} onOpenChange={setCatOpen}>
        <DialogContent className="bg-card border-border">
          <DialogHeader>
            <DialogTitle className="font-display text-foreground">
              {editingCat ? 'Kategoriyani tahrirlash' : 'Yangi kategoriya'}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Nomi</Label>
              <Input value={catName} onChange={e => setCatName(e.target.value)} className="bg-secondary" />
            </div>
            <div>
              <Label>Tartib raqami (kichik birinchi ko'rinadi)</Label>
              <MoneyInput
                value={catSort}
                onChange={setCatSort}
                step={1}
                min={0}
                placeholder="0"
                formatThousands={false}
                className="bg-secondary"
              />
            </div>
            <Button variant="hero" onClick={saveCategory} disabled={saving} className="w-full">
              {saving ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : null}
              Saqlash
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Item dialog */}
      <Dialog open={itemOpen} onOpenChange={setItemOpen}>
        <DialogContent className="bg-card border-border max-w-lg">
          <DialogHeader>
            <DialogTitle className="font-display text-foreground">
              {editingItem ? 'Taomni tahrirlash' : 'Yangi taom'}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Kategoriya</Label>
              <select
                value={itemCategoryId}
                onChange={e => setItemCategoryId(e.target.value)}
                className="h-10 w-full px-3 rounded-md border border-input bg-secondary text-foreground text-sm"
              >
                {categories.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <Label>Nomi</Label>
              <Input value={itemName} onChange={e => setItemName(e.target.value)} className="bg-secondary" />
            </div>
            <div>
              <Label>Tavsif</Label>
              <Textarea
                value={itemDesc}
                onChange={e => setItemDesc(e.target.value)}
                className="bg-secondary"
                rows={2}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Narx (so'm)</Label>
                <MoneyInput
                  value={itemPrice}
                  onChange={setItemPrice}
                  step={1000}
                  min={0}
                  placeholder="Masalan: 45 000"
                  suffix="so'm"
                  className="bg-secondary"
                />
              </div>
              <div>
                <Label>Rasm URL</Label>
                <Input
                  value={itemImage}
                  onChange={e => setItemImage(e.target.value)}
                  className="bg-secondary"
                  placeholder="https://..."
                />
              </div>
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="avail">Mavjud</Label>
              <Switch id="avail" checked={itemAvailable} onCheckedChange={setItemAvailable} />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="pop">Mashhur</Label>
              <Switch id="pop" checked={itemPopular} onCheckedChange={setItemPopular} />
            </div>
            <Button variant="hero" onClick={saveItem} disabled={saving} className="w-full">
              {saving ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : null}
              Saqlash
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
};
