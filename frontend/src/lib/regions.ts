export interface RegionOption {
  value: string;
  labelKey: string;
  aliases: string[];
}

export const uzbekistanRegions: RegionOption[] = [
  { value: '', labelKey: 'venues.region.all', aliases: [] },
  { value: 'andijon', labelKey: 'regions.andijon', aliases: ['andijon', 'andijan'] },
  { value: 'buxoro', labelKey: 'regions.buxoro', aliases: ['buxoro', 'bukhara'] },
  { value: 'fargona', labelKey: 'regions.fargona', aliases: ["farg'ona", 'fargona', 'fergana'] },
  { value: 'jizzax', labelKey: 'regions.jizzax', aliases: ['jizzax', 'jizzakh'] },
  { value: 'xorazm', labelKey: 'regions.xorazm', aliases: ['xorazm', 'khorezm', 'xiva', 'khiva'] },
  { value: 'namangan', labelKey: 'regions.namangan', aliases: ['namangan'] },
  { value: 'navoiy', labelKey: 'regions.navoiy', aliases: ['navoiy', 'navoi'] },
  { value: 'qashqadaryo', labelKey: 'regions.qashqadaryo', aliases: ['qashqadaryo', 'kashkadarya', 'qarshi'] },
  { value: 'samarqand', labelKey: 'regions.samarqand', aliases: ['samarqand', 'samarkand'] },
  { value: 'sirdaryo', labelKey: 'regions.sirdaryo', aliases: ['sirdaryo', 'syrdarya', 'guliston'] },
  { value: 'surxondaryo', labelKey: 'regions.surxondaryo', aliases: ['surxondaryo', 'surkhandarya', 'termiz'] },
  { value: 'toshkent_region', labelKey: 'regions.toshkentRegion', aliases: ['toshkent viloyati', 'tashkent region'] },
  { value: 'toshkent_city', labelKey: 'regions.toshkentCity', aliases: ['toshkent', 'tashkent'] },
];

export function matchesRegion(regionValue: string | undefined, fields: Array<string | undefined | null>): boolean {
  if (!regionValue) return true;
  const region = uzbekistanRegions.find(r => r.value === regionValue);
  if (!region) return true;
  const haystack = fields.filter(Boolean).join(' ').toLowerCase();
  return region.aliases.some(alias => haystack.includes(alias));
}
