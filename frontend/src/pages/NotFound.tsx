import { Link } from "react-router-dom";
import { Navbar } from "@/components/Navbar";
import { Button } from "@/components/ui/button";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";

const NotFound = () => {
  const { language } = useApp();
  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="flex min-h-screen items-center justify-center px-4">
        <div className="text-center space-y-4">
          <h1 className="font-display text-6xl font-bold text-primary">404</h1>
          <p className="text-xl text-muted-foreground">{t('notFound.title', language)}</p>
          <Button variant="hero" asChild><Link to="/">{t('notFound.home', language)}</Link></Button>
        </div>
      </div>
    </div>
  );
};

export default NotFound;
