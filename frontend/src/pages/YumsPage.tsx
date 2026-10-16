import { useState, useEffect } from "react";
import { reservationsApi } from "@/lib/api";
import { Navbar } from "@/components/Navbar";
import { Footer } from "@/components/Footer";
import { Button } from "@/components/ui/button";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";
import { motion } from "framer-motion";
import { Sparkles, Star, ArrowRight, Calendar, TrendingUp, Info, History } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";

interface YumsHistoryItem {
  id: string;
  amount: number;
  reason: string;
  createdAt: string;
}

const localeByLanguage = {
  uz: "uz-UZ",
  en: "en-US",
  ru: "ru-RU",
} as const;

const YumsPage = () => {
  const { language, user, profile } = useApp();
  const navigate = useNavigate();
  const balance = profile?.yumsBalance ?? 0;

  const [history, setHistory] = useState<YumsHistoryItem[]>([]);
  // Hali tashrif bo'lmagan faol bronlar — ular yakunlangach yums tushadi
  const [pending, setPending] = useState({ yums: 0, bookings: 0 });
  const [loadingHistory, setLoadingHistory] = useState(false);

  useEffect(() => {
    if (user) {
      setLoadingHistory(true);
      api.get<{ items: YumsHistoryItem[] }>('/profiles/yums/history')
        .then((res) => setHistory(res.items ?? []))
        .catch(() => {})
        .finally(() => setLoadingHistory(false));
      reservationsApi.mine()
        .then(list => {
          const active = list.filter(r => (r.status === 'confirmed' || r.status === 'pending') && (r.yumsEarned ?? 0) > 0);
          setPending({ yums: active.reduce((s, r) => s + (r.yumsEarned ?? 0), 0), bookings: active.length });
        })
        .catch(() => {});
    }
  }, [user]);

  const discountTier = {
    yums: 1000,
    label: t('yums.rewardDiscountLabel', language),
    desc: t('yums.rewardDiscountDesc', language),
    unlocked: balance >= 1000,
  };

  const howItWorks = [
    {
      icon: Calendar,
      title: t('yums.step1Title', language),
      desc: t('yums.step1Desc', language),
    },
    {
      icon: TrendingUp,
      title: t('yums.step2Title', language),
      desc: t('yums.step2Desc', language),
    },
    {
      icon: Sparkles,
      title: t('yums.step3Title', language),
      desc: t('yums.step3Desc', language),
    },
  ];

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="pt-24 pb-16 container mx-auto px-4">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="text-center mb-16">
          <div className="inline-flex items-center gap-2 bg-primary/10 text-primary text-sm font-medium px-4 py-2 rounded-full mb-6 border border-primary/20">
            <Sparkles className="w-4 h-4" />
            {t('yums.program', language)}
          </div>
          <h1 className="font-display text-4xl md:text-6xl font-bold text-foreground mb-4">
            {t('yums.title', language)}
          </h1>
          <p className="text-muted-foreground text-lg max-w-2xl mx-auto mb-8">
            {t('yums.subtitle', language)}
          </p>

          {user ? (
            <div className="inline-flex items-center gap-3 bg-card rounded-2xl border border-border px-8 py-5">
              <Sparkles className="w-8 h-8 text-primary" />
              <div className="text-left">
                <p className="text-muted-foreground text-sm">{t('yums.balance', language)}</p>
                <p className="font-display text-3xl font-bold text-foreground">
                  {balance} <span className="text-primary text-lg">Yums</span>
                </p>
                {pending.yums > 0 && (
                  <p className="text-xs text-muted-foreground mt-1">
                    {t('yums.pendingTotal', language).replace('{n}', String(pending.yums)).replace('{b}', String(pending.bookings))}
                  </p>
                )}
              </div>
            </div>
          ) : (
            <Button variant="hero" size="lg" onClick={() => navigate('/signup')}>
              {t('yums.getStarted', language)} <ArrowRight className="w-4 h-4 ml-2" />
            </Button>
          )}
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="mb-12 bg-blue-500/10 border border-blue-500/30 rounded-2xl p-6 flex gap-4"
        >
          <Info className="w-5 h-5 text-blue-400 shrink-0 mt-0.5" />
          <div className="text-sm text-blue-300 space-y-1">
            <p className="font-semibold text-blue-200">{t('yums.infoTitle', language)}</p>
            <p>{t('yums.infoEarn', language)}</p>
            <p>{t('yums.infoRedeem', language)}</p>
            <p className="text-blue-400">{t('yums.infoLimit', language)}</p>
          </div>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} whileInView={{ opacity: 1, y: 0 }} viewport={{ once: true }} className="mb-16">
          <h2 className="font-display text-2xl md:text-3xl font-bold text-foreground text-center mb-10">
            {t('yums.howItWorks', language)}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {howItWorks.map((step, i) => (
              <div key={step.title} className="bg-card rounded-2xl border border-border p-8 text-center relative">
                <div className="w-14 h-14 rounded-full bg-primary/10 flex items-center justify-center mx-auto mb-4">
                  <step.icon className="w-7 h-7 text-primary" />
                </div>
                <div className="absolute top-4 right-4 w-8 h-8 rounded-full bg-secondary flex items-center justify-center text-sm font-bold text-muted-foreground">
                  {i + 1}
                </div>
                <h3 className="font-display text-lg font-bold text-foreground mb-2">{step.title}</h3>
                <p className="text-muted-foreground text-sm">{step.desc}</p>
              </div>
            ))}
          </div>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} whileInView={{ opacity: 1, y: 0 }} viewport={{ once: true }} className="mb-16">
          <h2 className="font-display text-2xl md:text-3xl font-bold text-foreground text-center mb-10">
            {t('yums.discountTiers', language)}
          </h2>
          <div className="grid grid-cols-1 gap-6 max-w-sm mx-auto">
            <div
              className={`bg-card rounded-2xl border p-8 text-center transition-all ${
                discountTier.unlocked ? 'border-primary shadow-glow' : 'border-border'
              }`}
            >
              <div className="flex items-center justify-center gap-2 mb-3">
                <Star className={`w-6 h-6 ${discountTier.unlocked ? 'text-primary' : 'text-muted-foreground'}`} />
                <span className="font-display text-2xl font-bold text-foreground">{discountTier.yums} Yums</span>
              </div>
              <div className="text-3xl font-display font-bold text-primary mb-2">{discountTier.label}</div>
              <p className="text-muted-foreground text-sm mb-4">{discountTier.desc}</p>
              {discountTier.unlocked ? (
                <div className="bg-primary/10 text-primary text-sm font-medium px-4 py-2 rounded-full inline-flex items-center gap-2">
                  <Sparkles className="w-4 h-4" /> {t('yums.autoApplied', language)}
                </div>
              ) : (
                <div>
                  <p className="text-xs text-muted-foreground mb-3">
                    {t('yums.needMore', language)} <strong>{discountTier.yums - balance} Yums</strong> ({Math.ceil((discountTier.yums - balance) / 100)} {t('yums.bookingsNeeded', language)})
                  </p>
                  <div className="w-full bg-secondary rounded-full h-2">
                    <div
                      className="bg-primary h-2 rounded-full transition-all"
                      style={{ width: `${Math.min(100, (balance / discountTier.yums) * 100)}%` }}
                    />
                  </div>
                  <p className="text-xs text-muted-foreground mt-1">{balance} / {discountTier.yums}</p>
                </div>
              )}
            </div>
          </div>

          {user ? (
            <div className="text-center mt-8">
              <Button variant="hero" size="lg" onClick={() => navigate('/restaurants')}>
                {t('yums.bookNow', language)} <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
            </div>
          ) : (
            <div className="text-center mt-8">
              <p className="text-muted-foreground mb-3">{t('yums.signupPrompt', language)}</p>
              <Button variant="hero" size="lg" onClick={() => navigate('/signup')}>
                {t('auth.signup', language)} <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
            </div>
          )}
        </motion.div>

        {user && (
          <motion.div initial={{ opacity: 0, y: 20 }} whileInView={{ opacity: 1, y: 0 }} viewport={{ once: true }}>
            <h2 className="font-display text-2xl font-bold text-foreground mb-6 flex items-center gap-2">
              <History className="w-6 h-6 text-primary" /> {t('yums.history', language)}
            </h2>
            {loadingHistory ? (
              <div className="text-center text-muted-foreground py-8">{t('common.loading', language)}</div>
            ) : history.length === 0 ? (
              <div className="bg-card rounded-2xl border border-border p-8 text-center text-muted-foreground">
                {t('yums.historyEmpty', language)}
              </div>
            ) : (
              <div className="bg-card rounded-2xl border border-border divide-y divide-border">
                {history.map((item) => (
                  <div key={item.id} className="flex items-center justify-between px-6 py-4">
                    <div>
                      <p className="text-sm font-medium text-foreground">{item.reason}</p>
                      <p className="text-xs text-muted-foreground">
                        {new Date(item.createdAt).toLocaleDateString(localeByLanguage[language])}
                      </p>
                    </div>
                    <span className={`font-bold text-sm ${item.amount >= 0 ? 'text-green-500' : 'text-destructive'}`}>
                      {item.amount >= 0 ? '+' : ''}{item.amount} Yums
                    </span>
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        )}
      </div>
      <Footer />
    </div>
  );
};

export default YumsPage;
