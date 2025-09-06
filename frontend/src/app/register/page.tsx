'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/hooks/useAuth';
import { LegalModal } from '@/components/common';
import Image from 'next/image';
import { GoogleLogin, CredentialResponse } from '@react-oauth/google';
import styles from '../login/page.module.scss';

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

export default function RegisterPage() {
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: '',
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [showLegalModal, setShowLegalModal] = useState(false);
  const { register, refreshUser } = useAuth();
  const router = useRouter();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData((prev) => ({
      ...prev,
      [e.target.name]: e.target.value,
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (formData.password !== formData.confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    if (formData.password.length < 8) {
      setError('Password must be at least 8 characters long.');
      return;
    }

    setShowLegalModal(true);
  };

  const handleLegalAccept = async () => {
    setShowLegalModal(false);
    setIsLoading(true);

    try {
      await register({
        email: formData.email,
        password: formData.password,
        firstName: formData.firstName,
        lastName: formData.lastName,
        termsAccepted: true,
      });
      router.push('/buses');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleLegalClose = () => {
    setShowLegalModal(false);
  };

  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    if (!credentialResponse.credential) {
      setError('Google registration failed: No credential received');
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
        credentials: 'include',
        body: JSON.stringify({
          idToken: credentialResponse.credential,
        }),
      });

      if (res.ok) {
        await refreshUser();
        router.push('/');
      } else {
        const errorData = await res.json();
        setError(errorData.error || 'Google registration failed');
      }
    } catch (error) {
      console.error('Google registration error:', error);
      setError('Google registration failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleError = () => {
    setError('Google registration failed. Please try again.');
  };

  const handleAppleLogin = async () => {
    setIsLoading(true);
    setError('');

    try {
      if (!window.AppleID) {
        await loadAppleScript();
      }

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
        setError(errorData.error || 'Apple registration failed');
      }
    } catch (error: unknown) {
      console.error('Apple registration error:', error);
      if (
        typeof error === 'object' &&
        error !== null &&
        'error' in error &&
        (error as { error: string }).error === 'popup_closed_by_user'
      )
        return;
      setError('Apple registration failed. Please try again.');
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
          <p>Create your account to access bus information, predictions and more.</p>
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

          <h1 className={styles.loginTitle}>Create Account</h1>

          {error && (
            <div className={styles.errorSummary} role="alert">
              <ul>
                <li>{error}</li>
              </ul>
            </div>
          )}

          <form onSubmit={handleSubmit} className={styles.loginForm}>
            <div className={styles.formGroup}>
              <label htmlFor="firstName">First Name</label>
              <input
                id="firstName"
                name="firstName"
                type="text"
                value={formData.firstName}
                onChange={handleChange}
                placeholder="Enter your first name"
                required
                disabled={isLoading}
                autoComplete="given-name"
                autoFocus
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="lastName">Last Name</label>
              <input
                id="lastName"
                name="lastName"
                type="text"
                value={formData.lastName}
                onChange={handleChange}
                placeholder="Enter your last name"
                required
                disabled={isLoading}
                autoComplete="family-name"
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="email">Email</label>
              <input
                id="email"
                name="email"
                type="email"
                value={formData.email}
                onChange={handleChange}
                placeholder="Enter your email"
                required
                disabled={isLoading}
                autoComplete="username"
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="password">Password</label>
              <input
                id="password"
                name="password"
                type="password"
                value={formData.password}
                onChange={handleChange}
                placeholder="Enter your password"
                required
                disabled={isLoading}
                autoComplete="new-password"
                minLength={8}
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="confirmPassword">Confirm Password</label>
              <input
                id="confirmPassword"
                name="confirmPassword"
                type="password"
                value={formData.confirmPassword}
                onChange={handleChange}
                placeholder="Confirm your password"
                required
                disabled={isLoading}
                autoComplete="new-password"
                minLength={8}
              />
            </div>

            <button type="submit" className={styles.btnPrimary} disabled={isLoading}>
              <i className="fas fa-user-plus"></i>
              {isLoading ? 'Creating Account...' : 'Create Account'}
            </button>
          </form>

          <div className={styles.divider}>
            <div className={styles.dividerLine}></div>
            <span>or continue with</span>
            <div className={styles.dividerLine}></div>
          </div>

          <div className={styles.socialLogin}>
            <div className={styles.googleButtonContainer}>
              <button className={styles.btnGoogle} type="button" aria-label="Sign up with Google">
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
            <Link href="/login" className={styles.authTransitionLink}>
              <i className="fas fa-sign-in-alt"></i>
              Already have an account? Sign in
            </Link>
          </div>
        </div>
      </div>

      <LegalModal isOpen={showLegalModal} onAccept={handleLegalAccept} onClose={handleLegalClose} />
    </div>
  );
}
