import { lazy, Suspense } from "react";
import { Toaster } from "@/components/ui/toaster";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AppProvider } from "@/contexts/AppContext";
import { ProtectedRoute } from "@/components/ProtectedRoute";
import Index from "./pages/Index";
import RestaurantsPage from "./pages/RestaurantsPage";
import LoginPage from "./pages/LoginPage";
import SignupPage from "./pages/SignupPage";
import NotFound from "./pages/NotFound";

// Kamroq ishlatiladigan sahifalar alohida bo'laklarda yuklanadi (asosiy bundle kichrayadi)
const RestaurantDetailPage = lazy(() => import("./pages/RestaurantDetailPage"));
const ForgotPasswordPage = lazy(() => import("./pages/ForgotPasswordPage"));
const ResetPasswordPage = lazy(() => import("./pages/ResetPasswordPage"));
const ProfilePage = lazy(() => import("./pages/ProfilePage"));
const YumsPage = lazy(() => import("./pages/YumsPage"));
const OffersPage = lazy(() => import("./pages/OffersPage"));
const VenuesPage = lazy(() => import("./pages/VenuesPage"));
const VenueDetailPage = lazy(() => import("./pages/VenueDetailPage"));
const AdminDashboardPage = lazy(() => import("./pages/AdminDashboardPage"));
const SuperadminDashboardPage = lazy(() => import("./pages/SuperadminDashboardPage"));
const SetupPage = lazy(() => import("./pages/SetupPage"));
const VerifyEmailPage = lazy(() => import("./pages/VerifyEmailPage"));
const PartnerPage = lazy(() => import("./pages/PartnerPage"));
const InfoPage = lazy(() => import("./pages/InfoPage"));

const PageFallback = () => (
  <div className="min-h-screen flex items-center justify-center">
    <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
  </div>
);

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <AppProvider>
      <TooltipProvider>
        <Toaster />
        <Sonner />
        <BrowserRouter>
          <Suspense fallback={<PageFallback />}>
          <Routes>
            <Route path="/" element={<Index />} />
            <Route path="/restaurants" element={<RestaurantsPage />} />
            <Route path="/restaurant/:id" element={<RestaurantDetailPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignupPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/verify-email" element={<VerifyEmailPage />} />
            <Route path="/partner" element={<PartnerPage />} />
            <Route path="/terms" element={<InfoPage kind="terms" />} />
            <Route path="/privacy" element={<InfoPage kind="privacy" />} />
            <Route path="/faq" element={<InfoPage kind="faq" />} />
            <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
            <Route path="/yums" element={<YumsPage />} />
            <Route path="/offers" element={<OffersPage />} />
            <Route path="/venues" element={<VenuesPage />} />
            <Route path="/venue/:id" element={<VenueDetailPage />} />
            {/* Boshqaruv paneli — restoran egalari (admin) va xodimlari (staff) */}
            <Route path="/manager" element={<ProtectedRoute requiredRole="admin"><AdminDashboardPage /></ProtectedRoute>} />
            <Route path="/admin-dashboard" element={<ProtectedRoute requiredRole="admin"><AdminDashboardPage /></ProtectedRoute>} />
            <Route path="/superadmin-dashboard" element={<ProtectedRoute requiredRole="superadmin"><SuperadminDashboardPage /></ProtectedRoute>} />
            <Route path="/setup" element={<SetupPage />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
          </Suspense>
        </BrowserRouter>
      </TooltipProvider>
    </AppProvider>
  </QueryClientProvider>
);

export default App;
