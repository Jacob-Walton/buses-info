export function initializeMobileNav() {
	const elements = {
		trigger: document.getElementById("mobileMenuTrigger"),
		navbar: document.getElementById("mobileNavbar"),
		overlay: document.getElementById("mobileNavbarOverlay"),
		body: document.body,
		navItems: document.querySelectorAll(".mobile-nav-item"),
		subMenuTriggers: document.querySelectorAll(".has-submenu"),
	};

	// Touch variables
	let touchStartX = 0;
	let touchStartTime = 0;
	let isTouchDevice = false;
	const SWIPE_THRESHOLD = 70; // min distance to count as swipe
	const SWIPE_TIME_THRESHOLD = 300; // max time for a swipe in ms

	function openMobileMenu() {
		elements.navbar.classList.add("active");
		elements.overlay.classList.add("active");
		elements.body.classList.add("menu-open");
		elements.trigger.setAttribute("aria-expanded", "true");
		elements.trigger.classList.add("active");

		// Reset any inline styles that might have been set during swiping
		elements.navbar.style.transform = "";
		elements.navbar.style.transition = "";

		// Focus trap inside the mobile menu
		setTimeout(() => {
			document.querySelector(".mobile-nav-item a")?.focus();
		}, 300);

		// Staggered animation for menu items
		animateNavItems();
	}

	function closeMobileMenu() {
		elements.navbar.classList.remove("active");
		elements.overlay.classList.remove("active");
		elements.body.classList.remove("menu-open");
		elements.trigger.setAttribute("aria-expanded", "false");
		elements.trigger.classList.remove("active");

		// Reset any inline styles that might have been set during swiping
		elements.navbar.style.transform = "";
		elements.navbar.style.transition = "";
		elements.overlay.style.opacity = "";

		resetNavItems();

		// Close any open submenus
		document.querySelectorAll(".submenu.open").forEach((submenu) => {
			submenu.classList.remove("open");
		});

		document.querySelectorAll(".has-submenu").forEach((item) => {
			item.setAttribute("aria-expanded", "false");
		});
	}

	function animateNavItems() {
		elements.navItems.forEach((item, index) => {
			item.style.transitionDelay = `${index * 0.05}s`;
			item.classList.add("animate");
		});
	}

	function resetNavItems() {
		elements.navItems.forEach((item) => {
			item.classList.remove("animate");
			setTimeout(() => {
				item.style.transitionDelay = "0s";
			}, 300);
		});
	}

	function toggleSubmenu(e) {
		const parentItem = e.currentTarget.closest(".has-submenu");
		const submenu = parentItem.querySelector(".submenu");
		const isOpen = submenu.classList.contains("open");

		// Close other open submenus
		document.querySelectorAll(".submenu.open").forEach((menu) => {
			if (menu !== submenu) {
				menu.classList.remove("open");
				menu.closest(".has-submenu").setAttribute("aria-expanded", "false");
			}
		});

		// Toggle current submenu
		submenu.classList.toggle("open");
		parentItem.setAttribute("aria-expanded", isOpen ? "false" : "true");

		e.preventDefault();
	}

	// Separate touch handling for mobile devices only
	function initTouchHandling() {
		if (!("ontouchstart" in window)) return;

		isTouchDevice = true;
		const navbarWidth = elements.navbar.offsetWidth || 300;

		// Only attach touch events if we're on a touch device
		elements.navbar.addEventListener(
			"touchstart",
			(e) => {
				touchStartX = e.touches[0].clientX;
				touchStartTime = Date.now();
			},
			{ passive: true },
		);

		elements.navbar.addEventListener(
			"touchmove",
			(e) => {
				// Only handle swipes when menu is open
				if (!elements.navbar.classList.contains("active")) return;

				const currentX = e.touches[0].clientX;
				const diff = currentX - touchStartX;

				// Only allow swipes to the right (for closing)
				if (diff > 0) {
					// Prevent scroll while swiping
					e.preventDefault();

					elements.navbar.style.transition = "none";
					elements.navbar.style.transform = `translateX(${diff}px)`;

					// Fade overlay based on swipe progress
					const progress = Math.min(diff / navbarWidth, 1);
					elements.overlay.style.opacity = 1 - progress;
				}
			},
			{ passive: false },
		);

		elements.navbar.addEventListener(
			"touchend",
			(e) => {
				if (!elements.navbar.classList.contains("active")) return;

				const touchEndX = e.changedTouches[0].clientX;
				const touchEndTime = Date.now();
				const timeDiff = touchEndTime - touchStartTime;
				const distanceDiff = touchEndX - touchStartX;

				// Return to normal transitions
				elements.navbar.style.transition = "";
				elements.overlay.style.transition = "";

				// If swipe was fast enough or far enough, close the menu
				if (
					(distanceDiff > SWIPE_THRESHOLD && timeDiff < SWIPE_TIME_THRESHOLD) ||
					distanceDiff > navbarWidth / 2
				) {
					closeMobileMenu();
				} else {
					// Otherwise snap back
					elements.navbar.style.transform = "";
					elements.overlay.style.opacity = "";
				}
			},
			{ passive: true },
		);

		// Edge-swipe to open (simpler implementation)
		document.addEventListener(
			"touchstart",
			(e) => {
				// Only handle this when menu is closed
				if (elements.navbar.classList.contains("active")) return;

				const touchX = e.touches[0].clientX;
				const windowWidth = window.innerWidth;

				// Check if touch starts near the right edge
				if (touchX > windowWidth - 20) {
					touchStartX = touchX;
					touchStartTime = Date.now();
				}
			},
			{ passive: true },
		);

		document.addEventListener(
			"touchend",
			(e) => {
				// Only handle this when menu is closed
				if (elements.navbar.classList.contains("active") || touchStartX === 0)
					return;

				const touchEndX = e.changedTouches[0].clientX;
				const touchEndTime = Date.now();
				const timeDiff = touchEndTime - touchStartTime;
				const distanceDiff = touchStartX - touchEndX;

				// If it was a left swipe starting from the right edge
				if (distanceDiff > SWIPE_THRESHOLD && timeDiff < SWIPE_TIME_THRESHOLD) {
					openMobileMenu();
				}

				// Reset
				touchStartX = 0;
			},
			{ passive: true },
		);
	}

	// Event listeners - separate from touch events
	elements.trigger?.addEventListener("click", () => {
		if (elements.navbar.classList.contains("active")) {
			closeMobileMenu();
		} else {
			openMobileMenu();
		}
	});

	elements.overlay?.addEventListener("click", closeMobileMenu);

	// Handle links click
	document
		.querySelectorAll(
			".mobile-nav-link, .mobile-profile-item a, .mobile-profile-item button",
		)
		.forEach((link) => {
			link.addEventListener("click", () => {
				// Small delay to see the active state before closing
				setTimeout(closeMobileMenu, 150);
			});
		});

	// Handle submenu toggles
	elements.subMenuTriggers.forEach((trigger) => {
		trigger.addEventListener("click", toggleSubmenu);
	});

	// Handle keyboard accessibility
	elements.navbar?.addEventListener("keydown", (e) => {
		if (e.key === "Escape") {
			closeMobileMenu();
		}
	});

	// Handle window resize
	window.addEventListener("resize", () => {
		if (
			window.innerWidth > 768 &&
			elements.navbar.classList.contains("active")
		) {
			closeMobileMenu();
		}
	});

	// Initialize the touch handling
	initTouchHandling();
}
