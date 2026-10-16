import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { hasTranslation, type Language } from "@/lib/i18n";

const SRC = join(__dirname, "..");

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap(name => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) return name === "test" || name === "ui" ? [] : sourceFiles(full);
    return /\.(ts|tsx)$/.test(name) ? [full] : [];
  });
}

/** Koddagi t('kalit') / tf('kalit') va labelKey: 'kalit' */
function usedKeys(): Set<string> {
  const keys = new Set<string>();
  const re = /\b(?:t|tf)\(\s*['"]([a-zA-Z0-9_.]+)['"]|labelKey:\s*['"]([a-zA-Z0-9_.]+)['"]/g;
  for (const file of sourceFiles(SRC)) {
    if (file.endsWith("i18n.ts")) continue;
    for (const m of readFileSync(file, "utf8").matchAll(re)) keys.add(m[1] ?? m[2]);
  }
  // Dinamik kalitlar: t(`status.${...}`) va h.k.
  const dynamic: Record<string, string[]> = {
    status: ["pending", "confirmed", "completed", "cancelled", "no_show"],
    deposit: ["refund_requested", "refunded", "forfeited"],
    "booking.slot": ["past", "day_full", "hour_full", "too_far"],
    "table.location": ["hall", "terrace", "vip", "garden", "family"],
  };
  for (const [prefix, list] of Object.entries(dynamic)) list.forEach(k => keys.add(`${prefix}.${k}`));
  return keys;
}

describe("i18n", () => {
  const keys = [...usedKeys()].sort();

  it("finds keys in source", () => {
    expect(keys.length).toBeGreaterThan(100);
  });

  for (const lang of ["uz", "ru", "en"] as Language[]) {
    it(`every used key is translated to ${lang}`, () => {
      const missing = keys.filter(k => !hasTranslation(k, lang));
      expect(missing, `${lang}: ${missing.join(", ")}`).toEqual([]);
    });
  }
});
