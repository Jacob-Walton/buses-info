import Link from 'next/link';
import { forwardRef } from 'react';
import styles from './Button.module.scss';

export interface ButtonProps {
  children: React.ReactNode;
  variant?: 'primary' | 'secondary' | 'danger';
  size?: 'small' | 'medium' | 'large';
  href?: string;
  onClick?: () => void;
  disabled?: boolean;
  type?: 'button' | 'submit' | 'reset';
  className?: string;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      children,
      variant = 'primary',
      size = 'medium',
      href,
      onClick,
      disabled,
      type = 'button',
      className = '',
      ...props
    },
    ref,
  ) => {
    const baseClasses = `${styles.button} ${styles[variant]} ${styles[size]} ${className}`;

    if (href) {
      return (
        <Link href={href} className={baseClasses}>
          {children}
        </Link>
      );
    }

    return (
      <button
        ref={ref}
        type={type}
        className={baseClasses}
        onClick={onClick}
        disabled={disabled}
        {...props}
      >
        {children}
      </button>
    );
  },
);

Button.displayName = 'Button';
