'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useLocalStorage } from '@/hooks/useLocalStorage';
import { Modal } from '@/components/ui';
import styles from './LegalModal.module.scss';

interface LegalModalProps {
  isOpen?: boolean;
  onAccept?: () => void;
  onClose?: () => void;
}

export function LegalModal({ isOpen, onAccept, onClose }: LegalModalProps) {
  const [hasSeenLegal, setHasSeenLegal] = useLocalStorage('hasSeenLegal', false);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    if (isOpen !== undefined) {
      setShowModal(isOpen);
    } else if (!hasSeenLegal) {
      setShowModal(true);
    }
  }, [isOpen, hasSeenLegal]);

  const handleAccept = () => {
    setHasSeenLegal(true);
    setShowModal(false);
    onAccept?.();
  };

  const handleClose = () => {
    setShowModal(false);
    onClose?.();
  };

  if (!showModal) return null;

  return (
    <Modal isOpen={showModal} onClose={handleClose} className={styles.legalModal}>
      <div className={styles.legalModalContent}>
        <div className={styles.legalModalHeader}>
          <i className="fas fa-balance-scale"></i>
          <h2>Legal Notice & Terms of Use</h2>
        </div>
        <div className={styles.legalModalBody}>
          <p>PLEASE READ THIS LEGAL NOTICE CAREFULLY BEFORE USING THIS SITE</p>
          <div className={styles.legalTerms}>
            <p>By accessing and using this website (&quot;Site&quot;), you hereby acknowledge, understand, and expressly agree to the following terms and conditions:</p>
            <ol>
              <li>This Site is an independent, personal project and maintains no affiliation, endorsement, or connection with Runshaw College;</li>
              <li>All information provided herein is unofficial, non-binding, and provided &quot;as-is&quot; without any warranties, express or implied;</li>
              <li>For authoritative bus service information, users must refer to <a href="https://www.runshaw.ac.uk" target="_blank" rel="noopener noreferrer">Runshaw College&apos;s official website</a>;</li>
              <li>The operator of this Site expressly disclaims all liability for any direct, indirect, consequential, incidental, or special damages arising out of or in any way connected with the use of this Site;</li>
              <li>Any reliance you place on the information contained herein is strictly at your own risk.</li>
            </ol>
            <div className={styles.legalLinks}>
              <p>For more information, please review our:</p>
              <div className={styles.linkContainer}>
                <Link href="/privacy" className={styles.legalLink} target="_blank" rel="noopener noreferrer">
                  <i className="fas fa-shield-alt"></i>
                  Privacy Policy
                </Link>
                <Link href="/terms" className={styles.legalLink} target="_blank" rel="noopener noreferrer">
                  <i className="fas fa-file-contract"></i>
                  Terms of Use
                </Link>
              </div>
            </div>
          </div>
        </div>
        <div className={styles.legalModalFooter}>
          <button onClick={handleAccept} className={styles.acceptButton}>
            <i className="fas fa-check"></i>
            I Acknowledge and Accept
          </button>
        </div>
      </div>
    </Modal>
  );
}