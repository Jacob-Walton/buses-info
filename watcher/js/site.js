import { initializeAuth } from './modules/auth.js';
import { initializeMobileNav } from './modules/mobileNav.js';
import { initializeProfileDropdown } from './modules/profileDropdown.js';
import { initializeCustomSelects } from './modules/customSelect.js';
import { initializeLegalNotices } from './modules/legalNotices.js';
import { toast } from './modules/toast.js';

export { toast };

window.toast = toast;

document.addEventListener("DOMContentLoaded", function () {
    initializeAuth();
    initializeMobileNav();
    initializeProfileDropdown();
    initializeCustomSelects();
    initializeLegalNotices();
});
