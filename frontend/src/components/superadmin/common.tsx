import type { ReactNode } from 'react';
import { PAGE_SIZE, STATUS_LABELS } from './utils';
import { Button } from '@/components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';


export const PanelHeader = ({ title, subtitle, children }: { title: string; subtitle?: string; children?: ReactNode }) => (
  <div className="flex flex-wrap items-end justify-between gap-3 mb-4">
    <div>
      <h3 className="font-display text-2xl font-bold text-foreground">{title}</h3>
      {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
    </div>
    {children}
  </div>
);

export const Pager = ({ page, total, onPage }: { page: number; total: number; onPage: (p: number) => void }) => {
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  if (pages <= 1) return <p className="text-xs text-muted-foreground mt-3">Jami: {total}</p>;
  return (
    <div className="flex items-center gap-2 mt-3 text-sm text-muted-foreground">
      <Button variant="outline" size="icon" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="oldingi"><ChevronLeft className="w-4 h-4" /></Button>
      <span>{page} / {pages} · jami {total}</span>
      <Button variant="outline" size="icon" disabled={page >= pages} onClick={() => onPage(page + 1)} aria-label="keyingi"><ChevronRight className="w-4 h-4" /></Button>
    </div>
  );
};

const STATUS_STYLES: Record<string, string> = {
  confirmed: 'bg-success/15 text-success',
  pending: 'bg-primary/15 text-primary',
  completed: 'bg-blue-500/15 text-blue-400',
  cancelled: 'bg-destructive/15 text-destructive',
  no_show: 'bg-orange-500/15 text-orange-400',
  approved: 'bg-success/15 text-success',
  rejected: 'bg-destructive/15 text-destructive',
};


export const StatusBadge = ({ status }: { status: string }) => (
  <span className={cn('text-xs font-medium px-2 py-0.5 rounded-full whitespace-nowrap', STATUS_STYLES[status] ?? 'bg-muted text-muted-foreground')}>
    {STATUS_LABELS[status] ?? status}
  </span>
);

export const Select = ({ value, onChange, options, label }: {
  value: string; onChange: (v: string) => void; options: { value: string; label: string }[]; label: string;
}) => (
  <select
    value={value}
    onChange={e => onChange(e.target.value)}
    aria-label={label}
    className="h-10 rounded-md border border-border bg-secondary px-3 text-sm text-foreground"
  >
    {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
  </select>
);


export const Table = ({ head, children, empty }: { head: string[]; children: ReactNode; empty?: boolean }) => (
  <div className="bg-card rounded-xl border border-border overflow-x-auto">
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-muted-foreground border-b border-border">
          {head.map(h => <th key={h} className="p-3 font-medium whitespace-nowrap">{h}</th>)}
        </tr>
      </thead>
      <tbody>{children}</tbody>
    </table>
    {empty && <p className="text-center text-muted-foreground py-10">Hech narsa topilmadi.</p>}
  </div>
);

