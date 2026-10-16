import { Star, Clock, Percent, MapPin } from "lucide-react";
import { Button } from "@/components/ui/button";
import { motion } from "framer-motion";
import { useNavigate } from "react-router-dom";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";
import type { Restaurant } from "@/lib/restaurantApi";

export type { Restaurant } from "@/lib/restaurantApi";

interface RestaurantCardProps {
  restaurant: Restaurant;
  index: number;
  onBook?: (id: string) => void;
}

export const RestaurantCard = ({ restaurant, index }: RestaurantCardProps) => {
  const navigate = useNavigate();
  const { language } = useApp();

  return (
    <motion.div
      initial={{ opacity: 0, y: 30 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.08, duration: 0.5 }}
      className="group bg-gradient-card rounded-2xl overflow-hidden border border-border shadow-card hover:shadow-elevated hover:border-primary/30 transition-all duration-300 cursor-pointer"
      onClick={() => navigate(`/restaurant/${restaurant.id}`)}
    >
      <div className="relative h-48 overflow-hidden">
        <img src={restaurant.image} alt={restaurant.name} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500" />
        {!!restaurant.discount && (
          <div className="absolute top-3 left-3 flex items-center gap-1 bg-gradient-amber text-primary-foreground text-xs font-bold px-3 py-1.5 rounded-full shadow-glow">
            <Percent className="w-3 h-3" />
            {restaurant.discount}% {t('card.discount', language)}
          </div>
        )}
        <div className="absolute top-3 right-3">
          <span className="glass text-xs font-bold px-2.5 py-1 rounded-full text-foreground">
            {restaurant.cuisine_type.split("|")[1]?.trim() ?? restaurant.cuisine_type}
          </span>
        </div>
      </div>

      <div className="p-5">
        <div className="flex items-start justify-between mb-2">
          <h3 className="font-display text-lg font-semibold text-foreground group-hover:text-primary transition-colors">
            {restaurant.name}
          </h3>
          <div className="flex items-center gap-1 bg-primary/10 text-primary text-sm font-bold px-2 py-0.5 rounded-md">
            <Star className="w-3.5 h-3.5 fill-primary" />
            {restaurant.rating.toFixed(1)}
          </div>
        </div>

        <div className="flex items-center gap-1 text-muted-foreground text-xs mb-3">
          <MapPin className="w-3 h-3" />
          <span>{restaurant.address}</span>
          <span className="mx-1">•</span>
          <span>{restaurant.price_range}</span>
          <span className="mx-1">•</span>
          <span>{restaurant.reviewCount} {t('card.reviews', language)}</span>
        </div>

        <p className="text-muted-foreground text-sm mb-4 line-clamp-2">{restaurant.description}</p>

        <div className="flex items-center justify-between">
          {!!restaurant.discount && restaurant.nextAvailable && (
            <div className="flex items-center gap-1 text-success text-xs font-medium">
              <Clock className="w-3 h-3" />
              {t('card.discountHours', language)} {restaurant.nextAvailable}
            </div>
          )}
          <Button variant="hero" size="sm" className="ml-auto" onClick={(e) => { e.stopPropagation(); navigate(`/restaurant/${restaurant.id}`); }}>
            {t('card.book', language)}
          </Button>
        </div>
      </div>
    </motion.div>
  );
};
