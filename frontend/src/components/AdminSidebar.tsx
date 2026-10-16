import type { LucideIcon } from 'lucide-react';
import { useNavigate, Link } from 'react-router-dom';
import { useApp } from '@/contexts/AppContext';
import { NotificationBell } from '@/components/NotificationBell';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { BRAND } from '@/lib/brand';
import {
  BarChart3,
  CalendarDays,
  Users,
  Store,
  Percent,
  Settings,
  LogOut,
  UtensilsCrossed,
  MessageSquare,
  Table2,
  Clock,
  CalendarRange,
  FileBarChart,
  UserCog,
  Building2,
  Home,
} from 'lucide-react';

export type AdminTab =
  | 'dashboard'
  | 'reservations'
  | 'guests'
  | 'calendar'
  | 'menu'
  | 'tables'
  | 'reviews'
  | 'waitlist'
  | 'restaurant'
  | 'discounts'
  | 'reports'
  | 'staff'
  | 'venues'
  | 'settings';

const ADMIN_MENU: { key: AdminTab; icon: LucideIcon; label: string }[] = [
  { key: 'dashboard', icon: BarChart3, label: 'Bosh sahifa' },
  { key: 'reservations', icon: CalendarDays, label: 'Bronlar' },
  { key: 'calendar', icon: CalendarRange, label: 'Kalendar' },
  { key: 'guests', icon: Users, label: 'Mehmonlar' },
  { key: 'waitlist', icon: Clock, label: 'Kutish ro\'yxati' },
  { key: 'menu', icon: UtensilsCrossed, label: 'Menyu' },
  { key: 'tables', icon: Table2, label: 'Stollar' },
  { key: 'reviews', icon: MessageSquare, label: 'Sharhlar' },
  { key: 'restaurant', icon: Store, label: 'Restoran' },
  { key: 'discounts', icon: Percent, label: 'Chegirmalar' },
  { key: 'reports', icon: FileBarChart, label: 'Hisobotlar' },
  { key: 'staff', icon: UserCog, label: 'Xodimlar' },
  { key: 'venues', icon: Building2, label: 'Tadbir zallari' },
  { key: 'settings', icon: Settings, label: 'Sozlamalar' },
];

interface Props {
  activeTab: AdminTab;
  onTabChange: (tab: AdminTab) => void;
  /** Ko'rsatiladigan bo'limlar (huquqqa qarab); berilmasa — hammasi */
  tabs?: AdminTab[];
  restaurantName?: string;
  userName?: string;
  roleLabel?: string;
  /** Telefonda: yon menyu ochiqmi */
  mobileOpen: boolean;
  onMobileOpenChange: (open: boolean) => void;
}

export const AdminSidebar = (props: Props) => (
  <>
    <aside className="hidden md:flex fixed left-0 top-0 bottom-0 w-64 bg-[hsl(222,47%,8%)] border-r border-border flex-col z-40">
      <SidebarBody {...props} />
    </aside>
    <Sheet open={props.mobileOpen} onOpenChange={props.onMobileOpenChange}>
      <SheetContent side="left" className="md:hidden w-72 p-0 bg-[hsl(222,47%,8%)] border-border flex flex-col">
        <SheetTitle className="sr-only">Menyu</SheetTitle>
        <SidebarBody {...props} inSheet />
      </SheetContent>
    </Sheet>
  </>
);

const SidebarBody = ({ activeTab, onTabChange, tabs, restaurantName, userName, roleLabel, onMobileOpenChange, inSheet }: Props & { inSheet?: boolean }) => {
  const navigate = useNavigate();
  const { logout } = useApp();
  const items = tabs ? ADMIN_MENU.filter(i => tabs.includes(i.key)) : ADMIN_MENU;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <>
      {/* Telefonda yopish tugmasi (X) o'ng burchakda — joy qoldiramiz */}
      <div className={`p-5 border-b border-white/10 ${inSheet ? 'pr-12' : ''}`}>
        <div className="flex items-center gap-2">
          <Link to="/" className="w-9 h-9 rounded-lg bg-gradient-amber flex items-center justify-center shrink-0" aria-label="Bosh sahifa">
            <UtensilsCrossed className="w-5 h-5 text-primary-foreground" />
          </Link>
          <div className="flex-1 min-w-0">
            <span className="font-display text-sm font-bold text-white truncate block">{BRAND}</span>
            <p className="text-xs text-white/50">Boshqaruv paneli</p>
          </div>
          <NotificationBell variant="sidebar" />
        </div>
      </div>

      <nav className="flex-1 py-4 space-y-1 px-3 overflow-y-auto">
        {items.map(item => (
          <button
            key={item.key}
            onClick={() => { onTabChange(item.key); onMobileOpenChange(false); }}
            className={`w-full flex items-center gap-3 px-4 py-2.5 rounded-xl text-sm font-medium transition-all ${
              activeTab === item.key
                ? 'bg-primary/15 text-primary'
                : 'text-white/60 hover:text-white hover:bg-white/5'
            }`}
          >
            <item.icon className="w-4 h-4 shrink-0" />
            <span>{item.label}</span>
          </button>
        ))}
      </nav>

      <div className="p-4 border-t border-white/10">
        {restaurantName && <p className="text-xs text-white/70 mb-1 truncate">{restaurantName}</p>}
        {userName && <p className="text-xs text-white/40 truncate">{userName}</p>}
        {roleLabel && <p className="text-[11px] text-primary/80 mb-3">{roleLabel}</p>}
        <Link to="/" className="w-full flex items-center gap-2 px-4 py-2 rounded-xl text-sm text-white/60 hover:text-white hover:bg-white/5">
          <Home className="w-4 h-4" /> Saytga o'tish
        </Link>
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm text-red-400 hover:bg-red-500/10 transition-all"
        >
          <LogOut className="w-4 h-4" /> Chiqish
        </button>
      </div>
    </>
  );
};
