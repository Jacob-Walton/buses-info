'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/hooks/useAuth';
import Image from 'next/image';
import { GoogleLogin, CredentialResponse } from '@react-oauth/google';
import styles from './page.module.scss';

// Declare Apple Sign In global
declare global {
  interface Window {
    AppleID: {
      auth: {
        init: (options: {
          clientId: string;
          scope: string;
          redirectURI: string;
          usePopup: boolean;
        }) => Promise<void>;
        signIn: () => Promise<{
          authorization: {
            id_token: string;
          };
        }>;
      };
    };
  }
}

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const { login, refreshUser } = useAuth();
  const router = useRouter();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      await login(email, password);
      router.push('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Invalid username or password.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    if (!credentialResponse.credential) {
      setError('Google login failed: No credential received');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const res = await fetch('/api/auth/google', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include', // Include cookies
        body: JSON.stringify({
          idToken: credentialResponse.credential,
        }),
      });

      if (res.ok) {
        await refreshUser();
        router.push('/');
      } else {
        const errorData = await res.json();
        setError(errorData.error || 'Google login failed');
      }
    } catch (error) {
      console.error('Google login error:', error);
      setError('Google login failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleError = () => {
    setError('Google login failed. Please try again.');
  };

  const handleAppleLogin = async () => {
    setIsLoading(true);
    setError('');

    try {
      // Load Apple script if not already loaded
      if (!window.AppleID) {
        await loadAppleScript();
      }

      // Initialize Apple Sign In
      await window.AppleID.auth.init({
        clientId: process.env.NEXT_PUBLIC_APPLE_CLIENT_ID!,
        scope: 'name email',
        redirectURI: window.location.origin,
        usePopup: true,
      });

      const response = await window.AppleID.auth.signIn();

      const res = await fetch('/api/auth/apple', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          id_token: response.authorization.id_token,
        }),
      });

      if (res.ok) {
        await refreshUser();
        router.push('/');
      } else {
        const errorData = await res.json();
        setError(errorData.error || 'Apple login failed');
      }
    } catch (error: unknown) {
      console.error('Apple login error:', error);
      if (
        typeof error === 'object' &&
        error !== null &&
        'error' in error &&
        (error as { error: string }).error === 'popup_closed_by_user'
      )
        return;
      setError('Apple login failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const loadAppleScript = (): Promise<void> => {
    return new Promise((resolve, reject) => {
      if (document.getElementById('apple-signin-script')) {
        resolve();
        return;
      }

      const script = document.createElement('script');
      script.id = 'apple-signin-script';
      script.src =
        'https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js';
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Failed to load Apple Sign In script'));
      document.head.appendChild(script);
    });
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
            <Image
              src="https://d1tl6qv7xwsvxx.cloudfront.net/assets/logo-full.png"
              alt="Bus Info Logo"
              width={200}
              height={60}
              priority
            />
          </div>

          <h1 className={styles.loginTitle}>Welcome Back</h1>

          {process.env.NODE_ENV === 'development' && (
            <div className={styles.devNotice}>
              <h3>Development Mode</h3>
              <p>Use these test credentials:</p>
              <ul>
                <li>
                  <strong>User:</strong> test@example.com / password
                </li>
                <li>
                  <strong>Admin:</strong> admin@example.com / admin
                </li>
              </ul>
            </div>
          )}

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

            <button type="submit" className={styles.btnPrimary} disabled={isLoading}>
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
            <div className={styles.googleButtonContainer}>
              <button className={styles.btnGoogle}>
                <Image
                  src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/google/google-original.svg"
                  alt="Google"
                  width={18}
                  height={18}
                />
                Google
              </button>
              <div className={styles.googleOverlay}>
                <GoogleLogin
                  onSuccess={handleGoogleSuccess}
                  onError={handleGoogleError}
                  useOneTap={false}
                />
              </div>
            </div>

            <button
              type="button"
              onClick={handleAppleLogin}
              className={styles.btnApple}
              disabled={isLoading}
            >
              <Image
                src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/apple/apple-original.svg"
                alt="Apple"
                width={18}
                height={18}
              />
              Apple
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
