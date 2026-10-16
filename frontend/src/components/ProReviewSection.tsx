import { useMemo, useState } from 'react';
import { Star, MessageSquare, Reply, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { motion } from 'framer-motion';
import { reviewsApi, ApiError, type Review } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  restaurantId: string;
  reviews: Review[];
  restaurantRating: number;
  foodRating: number;
  serviceRating: number;
  interiorRating: number;
  reviewCount: number;
  canRespond?: boolean; // true for restaurant owner
  onReviewsChanged?: () => void;
}

/**
 * 3-part rating review section that matches the live API.
 * Users can submit a review with food/service/interior sub-ratings,
 * owners can post a public response.
 */
export const ProReviewSection = ({
  restaurantId,
  reviews,
  restaurantRating,
  foodRating,
  serviceRating,
  interiorRating,
  reviewCount,
  canRespond,
  onReviewsChanged,
}: Props) => {
  const { language, user } = useApp();
  const [addOpen, setAddOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [filter, setFilter] = useState<'newest' | 'withComments'>('newest');

  // Form state
  const [rFood, setRFood] = useState(8);
  const [rService, setRService] = useState(8);
  const [rInterior, setRInterior] = useState(8);
  const [rComment, setRComment] = useState('');

  // Reply form state (per-review)
  const [replyFor, setReplyFor] = useState<string | null>(null);
  const [replyText, setReplyText] = useState('');
  const [replying, setReplying] = useState(false);

  const filtered = useMemo(
    () =>
      [...(filter === 'withComments' ? reviews.filter(r => r.comment) : reviews)].sort(
        (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      ),
    [reviews, filter],
  );

  const ratingLabel =
    restaurantRating >= 9 ? 'Excellent' : restaurantRating >= 8 ? 'Very Good' : restaurantRating >= 7 ? 'Good' : 'Average';
  const ratingColor =
    restaurantRating >= 9 ? 'text-success' : restaurantRating >= 8 ? 'text-primary' : 'text-muted-foreground';

  const submitReview = async () => {
    if (!user) return;
    const overall = Math.round(((rFood + rService + rInterior) / 3) * 10) / 10;
    setSaving(true);
    try {
      await reviewsApi.create({
        restaurantId,
        rating: overall,
        foodRating: rFood,
        serviceRating: rService,
        interiorRating: rInterior,
        comment: rComment.trim() || undefined,
      });
      toast.success(t('reviews.submitted', language));
      setAddOpen(false);
      setRComment('');
      setRFood(8);
      setRService(8);
      setRInterior(8);
      onReviewsChanged?.();
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : 'Xatolik';
      toast.error(msg);
    } finally {
      setSaving(false);
    }
  };

  const submitReply = async (reviewId: string) => {
    if (!replyText.trim()) return;
    setReplying(true);
    try {
      await reviewsApi.respond(reviewId, { ownerResponse: replyText.trim() });
      toast.success(t('reviews.replySaved', language));
      setReplyFor(null);
      setReplyText('');
      onReviewsChanged?.();
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : 'Xatolik';
      toast.error(msg);
    } finally {
      setReplying(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Rating overview */}
      <div className="bg-card rounded-2xl border border-border p-6">
        <div className="flex flex-col md:flex-row gap-8">
          {/* Left: overall score */}
          <div className="flex items-center gap-4">
            <div className="w-20 h-20 rounded-2xl bg-primary/10 flex flex-col items-center justify-center">
              <span className="font-display text-3xl font-bold text-foreground">
                {restaurantRating.toFixed(1)}
              </span>
              <span className="text-xs text-muted-foreground">/10</span>
            </div>
            <div>
              <span className={`text-sm font-bold px-3 py-1 rounded-full ${ratingColor} bg-primary/10`}>
                {ratingLabel}
              </span>
              <p className="text-muted-foreground text-sm mt-1">
                {reviewCount} {t('card.reviews', language)}
              </p>
            </div>
          </div>

          {/* Right: bars */}
          <div className="flex-1 space-y-3">
            <RatingBar label={t('reviews.food', language)} value={foodRating} />
            <RatingBar label={t('reviews.service', language)} value={serviceRating} />
            <RatingBar label={t('reviews.ambience', language)} value={interiorRating} />
          </div>
        </div>
      </div>

      {/* Info + Add review */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-2 text-muted-foreground text-sm">
          <MessageSquare className="w-4 h-4" />
          <span>{t('reviews.realExperiences', language)}</span>
        </div>
        {user && !canRespond && (
          <Button variant="hero" size="sm" onClick={() => setAddOpen(true)}>
            <Star className="w-4 h-4 mr-1" /> {t('detail.addReview', language)}
          </Button>
        )}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2">
        <Button variant={filter === 'newest' ? 'default' : 'outline'} size="sm" onClick={() => setFilter('newest')}>
          {t('reviews.newest', language)}
        </Button>
        <Button
          variant={filter === 'withComments' ? 'default' : 'outline'}
          size="sm"
          onClick={() => setFilter('withComments')}
        >
          {t('reviews.withComments', language)}
        </Button>
      </div>

      {/* List */}
      <div className="space-y-4">
        {filtered.map((review, i) => (
          <motion.div
            key={review.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.03 }}
            className="bg-card rounded-xl border border-border p-5"
          >
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-secondary flex items-center justify-center text-foreground font-bold">
                  {(review.userFullName ?? 'U')[0].toUpperCase()}
                </div>
                <div>
                  <p className="text-foreground font-medium text-sm">
                    {review.userFullName ?? 'Foydalanuvchi'}
                  </p>
                  <p className="text-muted-foreground text-xs">
                    {new Date(review.createdAt).toLocaleDateString()}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-1">
                <span className="font-display text-xl font-bold text-foreground">
                  {review.rating.toFixed(1)}
                </span>
                <span className="text-muted-foreground text-sm">/10</span>
              </div>
            </div>

            {/* Sub-ratings */}
            <div className="grid grid-cols-3 gap-2 text-xs mb-3">
              <SubBadge label={t('reviews.food', language)} value={review.foodRating} />
              <SubBadge label={t('reviews.service', language)} value={review.serviceRating} />
              <SubBadge label={t('reviews.ambience', language)} value={review.interiorRating} />
            </div>

            {review.comment && (
              <p className="text-foreground/80 text-sm mb-3 whitespace-pre-wrap">{review.comment}</p>
            )}

            {/* Owner response */}
            {review.ownerResponse && (
              <div className="mt-3 bg-primary/5 border-l-2 border-primary pl-3 py-2 rounded-r">
                <p className="text-xs font-medium text-primary mb-1">
                  {t('reviews.ownerResponse', language)}
                </p>
                <p className="text-foreground/80 text-sm">{review.ownerResponse}</p>
                {review.ownerRespondedAt && (
                  <p className="text-[11px] text-muted-foreground mt-1">
                    {new Date(review.ownerRespondedAt).toLocaleDateString()}
                  </p>
                )}
              </div>
            )}

            {/* Owner reply control */}
            {canRespond && !review.ownerResponse && (
              <div className="mt-3">
                {replyFor === review.id ? (
                  <div className="space-y-2">
                    <Textarea
                      value={replyText}
                      onChange={e => setReplyText(e.target.value)}
                      placeholder={t('reviews.replyPlaceholder', language)}
                      rows={2}
                      className="bg-secondary border-border text-sm"
                    />
                    <div className="flex gap-2">
                      <Button size="sm" onClick={() => submitReply(review.id)} disabled={replying || !replyText.trim()}>
                        {replying ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : 'Yuborish'}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setReplyFor(null);
                          setReplyText('');
                        }}
                      >
                        Bekor
                      </Button>
                    </div>
                  </div>
                ) : (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setReplyFor(review.id);
                      setReplyText('');
                    }}
                  >
                    <Reply className="w-3.5 h-3.5 mr-1" /> Javob yozish
                  </Button>
                )}
              </div>
            )}
          </motion.div>
        ))}
      </div>

      {filtered.length === 0 && (
        <p className="text-muted-foreground text-center py-8">{t('profile.noReviews', language)}</p>
      )}

      {/* Add Review Dialog — 3 sub-ratings */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent className="bg-card border-border max-w-lg">
          <DialogHeader>
            <DialogTitle className="font-display text-foreground">
              {t('detail.addReview', language)}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-5">
            <RatingPicker label={t('reviews.food', language)} value={rFood} onChange={setRFood} />
            <RatingPicker label={t('reviews.service', language)} value={rService} onChange={setRService} />
            <RatingPicker label={t('reviews.ambience', language)} value={rInterior} onChange={setRInterior} />

            <div className="bg-secondary/50 rounded-xl p-3 flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                {t('reviews.overallRating', language)}
              </span>
              <span className="font-display text-xl font-bold text-foreground">
                {((rFood + rService + rInterior) / 3).toFixed(1)}/10
              </span>
            </div>

            <div>
              <p className="text-sm text-muted-foreground mb-2">
                {t('reviews.yourComment', language)}
              </p>
              <Textarea
                value={rComment}
                onChange={e => setRComment(e.target.value)}
                placeholder={t('reviews.commentPlaceholder', language)}
                rows={4}
                className="bg-secondary border-border"
              />
            </div>

            <Button variant="hero" className="w-full" onClick={submitReview} disabled={saving}>
              {saving ? (
                <Loader2 className="w-4 h-4 animate-spin mr-2" />
              ) : null}
              {t('reviews.submit', language)}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
};

const RatingBar = ({ label, value }: { label: string; value: number }) => (
  <div className="flex items-center gap-3">
    <span className="text-sm text-muted-foreground w-24 shrink-0">{label}</span>
    <div className="flex-1 h-2 rounded-full bg-secondary overflow-hidden">
      <div
        className="h-full rounded-full bg-primary transition-all"
        style={{ width: `${Math.max(0, Math.min(100, (value / 10) * 100))}%` }}
      />
    </div>
    <span className="text-sm font-medium text-foreground w-8 text-right">{value.toFixed(1)}</span>
  </div>
);

const SubBadge = ({ label, value }: { label: string; value: number }) => (
  <div className="bg-secondary/50 rounded-lg px-2 py-1 flex items-center justify-between">
    <span className="text-muted-foreground truncate">{label}</span>
    <span className="font-bold text-foreground ml-1">{value}/10</span>
  </div>
);

const RatingPicker = ({
  label,
  value,
  onChange,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
}) => (
  <div>
    <div className="flex items-center justify-between mb-2">
      <p className="text-sm text-muted-foreground">{label}</p>
      <span className="text-sm font-bold text-foreground">{value}/10</span>
    </div>
    <div className="flex gap-1">
      {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => (
        <button
          key={n}
          type="button"
          onClick={() => onChange(n)}
          className={`w-8 h-8 rounded-md text-xs font-bold transition-all ${
            n <= value
              ? 'bg-primary text-primary-foreground'
              : 'bg-secondary text-muted-foreground hover:bg-secondary/80'
          }`}
        >
          {n}
        </button>
      ))}
    </div>
  </div>
);
