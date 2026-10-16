import { MapPin, Navigation } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface Props {
  latitude: number;
  longitude: number;
  name: string;
  address?: string;
  height?: number;
}

/**
 * OpenStreetMap iframe orqali xarita ko'rsatish (kalit kerak emas).
 * TheFork.com uslubidagi pin va "Yo'l olish" tugmasi bilan.
 */
export const RestaurantMap = ({ latitude, longitude, name, address, height = 320 }: Props) => {
  // OSM bbox uchun kichik radius (~300m)
  const delta = 0.003;
  const bbox = [
    longitude - delta,
    latitude - delta,
    longitude + delta,
    latitude + delta,
  ].join(',');

  const iframeSrc = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${latitude},${longitude}`;

  // Yo'l olish (multiple options)
  const googleMapsUrl = `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}`;
  const yandexMapsUrl = `https://yandex.com/maps/?rtext=~${latitude},${longitude}&rtt=auto`;

  return (
    <div className="bg-card rounded-xl border border-border overflow-hidden">
      <div className="relative">
        <iframe
          title={`${name} xaritada`}
          src={iframeSrc}
          style={{ width: '100%', height, border: 0 }}
          loading="lazy"
        />
        {/* Pin overlay (visual emphasis) */}
        <div className="pointer-events-none absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-full">
          <div className="bg-primary text-primary-foreground rounded-full p-2 shadow-lg ring-4 ring-primary/30 animate-pulse">
            <MapPin className="w-5 h-5" />
          </div>
        </div>
      </div>
      <div className="p-4 flex items-center justify-between gap-3 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 text-sm text-foreground font-medium">
            <MapPin className="w-4 h-4 text-primary shrink-0" />
            <span className="truncate">{name}</span>
          </div>
          {address && (
            <p className="text-xs text-muted-foreground mt-1 truncate">{address}</p>
          )}
          <p className="text-xs text-muted-foreground mt-0.5">
            {latitude.toFixed(5)}, {longitude.toFixed(5)}
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" asChild>
            <a href={googleMapsUrl} target="_blank" rel="noreferrer">
              <Navigation className="w-3.5 h-3.5 mr-1" /> Google Maps
            </a>
          </Button>
          <Button variant="outline" size="sm" asChild>
            <a href={yandexMapsUrl} target="_blank" rel="noreferrer">
              Yandex
            </a>
          </Button>
        </div>
      </div>
    </div>
  );
};
