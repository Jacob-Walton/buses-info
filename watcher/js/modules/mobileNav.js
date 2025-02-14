export function initializeMobileNav() {
	const elements = {
		trigger: document.getElementById("mobileMenuTrigger"),
		close: document.getElementById("mobileMenuClose"),
		navbar: document.getElementById("mobileNavbar"),
		overlay: document.getElementById("mobileNavbarOverlay"),
		body: document.body,
	};

	function openMobileMenu() {
		elements.navbar.classList.add("active");
		elements.overlay.classList.add("active");
		elements.body.style.overflow = "hidden";
		animateNavItems();
	}

	function closeMobileMenu() {
		elements.navbar.classList.remove("active");
		elements.overlay.classList.remove("active");
		elements.body.style.overflow = "";
		resetNavItems();
	}

	function animateNavItems() {
		document.querySelectorAll(".mobile-nav-item").forEach((item, index) => {
			setTimeout(() => item.classList.add("animate"), 100 * (index + 1));
		});
	}

	function resetNavItems() {
		document.querySelectorAll(".mobile-nav-item").forEach((item) => {
			item.classList.remove("animate");
		});
	}

	// Event listeners
	elements.trigger?.addEventListener("click", openMobileMenu);
	elements.close?.addEventListener("click", closeMobileMenu);
	elements.overlay?.addEventListener("click", closeMobileMenu);

	document.querySelectorAll(".mobile-nav-link a").forEach((link) => {
		link.addEventListener("click", closeMobileMenu);
	});

	window.addEventListener("resize", () => {
		if (window.innerWidth > 768) {
			closeMobileMenu();
		}
	});
}
