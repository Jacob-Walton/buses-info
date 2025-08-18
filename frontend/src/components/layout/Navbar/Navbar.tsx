'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useAuth } from '@/hooks/useAuth';
import styles from './Navbar.module.scss';

export function Navbar() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isProfileDropdownOpen, setIsProfileDropdownOpen] = useState(false);
  const pathname = usePathname();
  const { user, isAuthenticated, logout } = useAuth();
  const profileMenuRef = useRef<HTMLDivElement>(null);

  const isActive = (path: string) => pathname === path;

  useEffect(() => {
    // Manage body class for mobile menu
    if (isMobileMenuOpen) {
      document.body.classList.add('menu-open');
    } else {
      document.body.classList.remove('menu-open');
    }

    // Cleanup on unmount
    return () => {
      document.body.classList.remove('menu-open');
    };
  }, [isMobileMenuOpen]);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (profileMenuRef.current && !profileMenuRef.current.contains(event.target as Node)) {
        setIsProfileDropdownOpen(false);
      }
    };

    if (isProfileDropdownOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isProfileDropdownOpen]);

  // Close dropdown when pathname changes (navigation)
  useEffect(() => {
    setIsProfileDropdownOpen(false);
  }, [pathname]);

  const handleMobileMenuToggle = () => {
    setIsMobileMenuOpen(!isMobileMenuOpen);
  };

  const handleLogout = async () => {
    await logout();
    setIsProfileDropdownOpen(false);
    setIsMobileMenuOpen(false);
  };

  const handleNavClick = () => {
    setIsMobileMenuOpen(false);
    setIsProfileDropdownOpen(false);
  };

  return (
    <>
      <nav className={styles.navbar}>
        <div className={styles.container}>
          <div className={styles.navbarBrandContainer}>
            <Link 
              href="/" 
              className={`${styles.navbarBrand} ${styles.simple}`}
              aria-label="Bus Info Home"
              title="Bus Info Home"
              onClick={handleNavClick}
            />
          </div>

          <button
            className={`${styles.mobileMenuTrigger} ${isMobileMenuOpen ? styles.active : ''}`}
            onClick={handleMobileMenuToggle}
            aria-label="Toggle navigation menu"
            aria-expanded={isMobileMenuOpen}
          >
            <span></span>
            <span></span>
            <span></span>
          </button>

          <div className={`${styles.navbarMenu} ${isMobileMenuOpen ? styles.active : ''}`}>
            <ul className={styles.navbarNav}>
              <li className={`${styles.navItem} ${isMobileMenuOpen ? styles.animate : ''}`}>
                <Link 
                  href="/" 
                  className={`${styles.navLink} ${isActive('/') ? styles.active : ''}`}
                  onClick={handleNavClick}
                >
                  Home
                </Link>
              </li>

              {!isAuthenticated ? (
                <li className={`${styles.navItem} ${isMobileMenuOpen ? styles.animate : ''}`}>
                  <Link 
                    href="/login" 
                    className={`${styles.navLink} ${styles.navLinkBold}`}
                    onClick={handleNavClick}
                  >
                    Sign In
                  </Link>
                </li>
              ) : (
                <>
                  <div className={`${styles.profileMenu} ${styles.desktopOnly}`} ref={profileMenuRef}>
                    <button 
                      className={styles.profileTrigger}
                      onClick={() => setIsProfileDropdownOpen(!isProfileDropdownOpen)}
                    >
                      <i className="fas fa-user"></i>
                      <i className="fas fa-chevron-down"></i>
                    </button>
                    <div className={`${styles.profileDropdown} ${isProfileDropdownOpen ? styles.active : ''}`}>
                      {user?.role === 'Admin' && (
                        <Link 
                          href="/admin" 
                          className={styles.dropdownItem}
                          onClick={handleNavClick}
                        >
                          <i className="fas fa-shield-alt"></i>
                          Admin
                        </Link>
                      )}
                      <Link 
                        href="/settings" 
                        className={styles.dropdownItem}
                        onClick={handleNavClick}
                      >
                        <i className="fas fa-cog"></i>
                        Settings
                      </Link>
                      <button 
                        onClick={handleLogout} 
                        className={`${styles.dropdownItem} ${styles.danger}`}
                      >
                        <i className="fas fa-sign-out-alt"></i>
                        Sign Out
                      </button>
                    </div>
                  </div>
                  
                  {/* Mobile profile items */}
                  <div className={styles.mobileProfileItems}>
                    {user?.role === 'Admin' && (
                      <li className={`${styles.navItem} ${isMobileMenuOpen ? styles.animate : ''}`}>
                        <Link 
                          href="/admin" 
                          className={`${styles.navLink} ${isActive('/admin') ? styles.active : ''}`}
                          onClick={handleNavClick}
                        >
                          <i className="fas fa-shield-alt"></i>
                          Admin
                        </Link>
                      </li>
                    )}
                    <li className={`${styles.navItem} ${isMobileMenuOpen ? styles.animate : ''}`}>
                      <Link 
                        href="/settings" 
                        className={`${styles.navLink} ${isActive('/settings') ? styles.active : ''}`}
                        onClick={handleNavClick}
                      >
                        <i className="fas fa-cog"></i>
                        Settings
                      </Link>
                    </li>
                    <li className={`${styles.navItem} ${isMobileMenuOpen ? styles.animate : ''}`}>
                      <button 
                        onClick={handleLogout} 
                        className={`${styles.navLink} ${styles.danger}`}
                      >
                        <i className="fas fa-sign-out-alt"></i>
                        Sign Out
                      </button>
                    </li>
                  </div>
                </>
              )}
            </ul>
          </div>
        </div>
      </nav>

      {/* Mobile overlay */}
      {isMobileMenuOpen && (
        <div 
          className={`${styles.mobileOverlay} ${styles.active}`}
          onClick={handleMobileMenuToggle}
        />
      )}
    </>
  );
}