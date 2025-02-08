import { initializeAuth } from './modules/auth.js';
import { initializeMobileNav } from './modules/mobileNav.js';
import { initializeProfileDropdown } from './modules/profileDropdown.js';
import { initializeCustomSelects } from './modules/customSelect.js';
import { initializeLegalNotices } from './modules/legalNotices.js';

document.addEventListener("DOMContentLoaded", function () {
    initializeAuth();
    initializeMobileNav();
    initializeProfileDropdown();
    initializeCustomSelects();
    initializeLegalNotices();
});
