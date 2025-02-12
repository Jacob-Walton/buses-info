import { initializeAuth } from "./modules/auth.js";
import { initializeCustomSelects } from "./modules/customSelect.js";
import { initializeLegalNotices } from "./modules/legalNotices.js";
import { initializeMobileNav } from "./modules/mobileNav.js";
import { initializeProfileDropdown } from "./modules/profileDropdown.js";
import { toast } from "./modules/toast.js";

export { toast };

window.toast = toast;

document.addEventListener("DOMContentLoaded", () => {
	initializeAuth();
	initializeMobileNav();
	initializeProfileDropdown();
	initializeCustomSelects();
	initializeLegalNotices();
});
