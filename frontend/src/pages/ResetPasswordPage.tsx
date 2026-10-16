import { Navigate } from 'react-router-dom';

/**
 * Eski "?token=" havolalari uchun. Parol endi emailga kelgan 6 xonali kod bilan tiklanadi
 * (ForgotPasswordPage), shuning uchun bu sahifa o'sha yerga yo'naltiradi.
 */
const ResetPasswordPage = () => <Navigate to="/forgot-password" replace />;

export default ResetPasswordPage;
