'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/hooks/useAuth';
import { Button } from '@/components/ui';
import styles from './page.module.scss';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const { login } = useAuth();
  const router = useRouter();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      await login(email, password);
      router.push('/buses');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Invalid username or password.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleLogin = () => {
    window.location.href = '/api/auth/google';
  };

  const handleMicrosoftLogin = () => {
    window.location.href = '/api/auth/microsoft';
  };

  return (
    <div className={styles.loginPage}>
      <div className={styles.authBackground}>
        <div className={styles.brandContent}>
          <h2>Bus Info</h2>
          <p>Access bus information, predictions and more.</p>
        </div>
      </div>
      
      <div className={styles.authFormContainer}>
        <div className={styles.loginContainer}>
          <div className={styles.loginLogo}>
            <img src="https://d1tl6qv7xwsvxx.cloudfront.net/assets/logo-full.png" alt="Bus Info Logo" />
          </div>
          
          <h1 className={styles.loginTitle}>Welcome Back</h1>

          {error && (
            <div className={styles.errorSummary} role="alert">
              <ul>
                <li>{error}</li>
              </ul>
            </div>
          )}

          <form onSubmit={handleSubmit} className={styles.loginForm}>
            <div className={styles.formGroup}>
              <label htmlFor="email">Email</label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter your email"
                required
                disabled={isLoading}
                autoComplete="username"
                autoFocus
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="password">Password</label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                required
                disabled={isLoading}
                autoComplete="current-password"
              />
            </div>

            <div className={styles.formOptions}>
              <label className={styles.option}>
                <div className={styles.checkboxWrapper}>
                  <input
                    type="checkbox"
                    checked={rememberMe}
                    onChange={(e) => setRememberMe(e.target.checked)}
                    disabled={isLoading}
                  />
                  <div className={styles.checkboxCustom}></div>
                </div>
                <span>Remember me</span>
              </label>
              <Link href="/forgot-password" className={styles.forgotLink}>
                Forgot password?
              </Link>
            </div>

            <button
              type="submit"
              className={styles.btnPrimary}
              disabled={isLoading}
            >
              <i className="fas fa-sign-in-alt"></i>
              {isLoading ? 'Signing In...' : 'Sign In'}
            </button>
          </form>

          <div className={styles.divider}>
            <div className={styles.dividerLine}></div>
            <span>or continue with</span>
            <div className={styles.dividerLine}></div>
          </div>
          
          <div className={styles.socialLogin}>
            <button
              type="button"
              onClick={handleGoogleLogin}
              className={styles.btnGoogle}
              disabled={isLoading}
            >
              <img 
                src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/google/google-original.svg" 
                alt="Google" 
                width="18" 
                height="18" 
              />
              Google
            </button>

            <button
              type="button"
              onClick={handleMicrosoftLogin}
              className={styles.btnMicrosoft}
              disabled={isLoading}
            >
              <img 
                src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/windows11/windows11-original.svg" 
                alt="Microsoft" 
                width="18" 
                height="18" 
              />
              Microsoft
            </button>
          </div>

          <div className={styles.loginLinks}>
            <Link href="/register" className={styles.authTransitionLink}>
              <i className="fas fa-user-plus"></i>
              Create new account
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}