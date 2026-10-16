import { useCallback, useEffect, useState } from 'react';
import { reviewsApi, ApiError, type Review } from '@/lib/api';
import { ProReviewSection } from '@/components/ProReviewSection';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

interface Props {
  restaurantId: string;
  restaurantRating: number;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  reviewCount: number;
}

/**
 * Admin-side review panel — reuses ProReviewSection with canRespond=true,
 * which lets the owner post a public reply to each review.
 */
export const AdminReviewsManager = (p: Props) => {
  const [reviews, setReviews] = useState<Review[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const list = await reviewsApi.byRestaurant(p.restaurantId);
      setReviews(list);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Xatolik');
    } finally {
      setLoading(false);
    }
  }, [p.restaurantId]);

  useEffect(() => {
    if (p.restaurantId) load();
  }, [p.restaurantId, load]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="w-6 h-6 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <ProReviewSection
      restaurantId={p.restaurantId}
      reviews={reviews}
      restaurantRating={p.restaurantRating}
      foodRating={p.foodRating}
      serviceRating={p.serviceRating}
      interiorRating={p.interiorRating}
      reviewCount={p.reviewCount}
      canRespond
      onReviewsChanged={load}
    />
  );
};
