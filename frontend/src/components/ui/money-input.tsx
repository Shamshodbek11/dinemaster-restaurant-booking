import { forwardRef, useState, useEffect } from 'react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface MoneyInputProps {
  value: number | '' | null | undefined;
  onChange: (val: number) => void;
  step?: number;
  min?: number;
  max?: number;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  /** Pul birligi (so'm, $, etc) — input ichida sufix sifatida ko'rsatiladi */
  suffix?: string;
  /** Ming joylariga vergul qo'yish (1 000 000) */
  formatThousands?: boolean;
}

/**
 * Pul/raqam kiritish maydoni:
 * - Bo'sh bo'lsa "0" ko'rsatmaydi (placeholder ishlatadi)
 * - Foydalanuvchi raqam yozayotganda boshidagi 0 yo'qoladi (1000 yozish — "1000", "0" emas)
 * - step (default 100) tugmalar yoki keyboard arrow uchun ishlaydi
 * - formatThousands: ko'rsatishda ming joylari ajratiladi, lekin onChange'ga raqam keladi
 */
export const MoneyInput = forwardRef<HTMLInputElement, MoneyInputProps>((props, ref) => {
  const {
    value,
    onChange,
    step = 100,
    min = 0,
    max,
    placeholder = '0',
    disabled,
    className,
    suffix,
    formatThousands = true,
  } = props;

  // Ichki text-state: bo'sh string yoki raqam string'i
  const [text, setText] = useState<string>(() => {
    if (value === null || value === undefined || value === '' || value === 0) return '';
    return formatThousands ? Number(value).toLocaleString('en-US').replace(/,/g, ' ') : String(value);
  });

  // Tashqaridan value o'zgarsa sync qilish
  useEffect(() => {
    if (value === null || value === undefined || value === '') {
      setText('');
      return;
    }
    const numeric = Number(value);
    if (Number.isNaN(numeric)) return;
    const display = formatThousands ? numeric.toLocaleString('en-US').replace(/,/g, ' ') : String(numeric);
    if (display !== text) setText(display);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  const commit = (rawText: string) => {
    if (rawText === '') {
      onChange(0);
      return;
    }
    const cleaned = rawText.replace(/[^0-9]/g, '');
    if (cleaned === '') {
      onChange(0);
      return;
    }
    let num = parseInt(cleaned, 10);
    if (Number.isNaN(num)) num = 0;
    if (typeof min === 'number' && num < min) num = min;
    if (typeof max === 'number' && num > max) num = max;
    onChange(num);
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value;
    // faqat raqam, bo'shliq va vergul qabul qilamiz
    const digitsOnly = raw.replace(/[^0-9]/g, '');
    if (digitsOnly === '') {
      setText('');
      onChange(0);
      return;
    }
    // Boshidagi ortiqcha 0'larni olib tashlaymiz
    const trimmed = digitsOnly.replace(/^0+(?=\d)/, '');
    const num = parseInt(trimmed, 10);
    const display = formatThousands ? num.toLocaleString('en-US').replace(/,/g, ' ') : String(num);
    setText(display);
    onChange(num);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      const cur = Number(String(text).replace(/\s/g, '')) || 0;
      const next = cur + step;
      const display = formatThousands ? next.toLocaleString('en-US').replace(/,/g, ' ') : String(next);
      setText(display);
      onChange(next);
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      const cur = Number(String(text).replace(/\s/g, '')) || 0;
      const next = Math.max(min ?? 0, cur - step);
      const display = formatThousands ? next.toLocaleString('en-US').replace(/,/g, ' ') : String(next);
      setText(display);
      onChange(next);
    }
  };

  return (
    <div className="relative">
      <Input
        ref={ref}
        type="text"
        inputMode="numeric"
        value={text}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        onBlur={(e) => commit(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        className={cn(suffix && 'pr-14', className)}
      />
      {suffix && (
        <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
          {suffix}
        </span>
      )}
    </div>
  );
});
MoneyInput.displayName = 'MoneyInput';
