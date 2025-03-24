document.addEventListener("DOMContentLoaded", () => {
	// Elements
	const navbar = document.getElementById("mainNavbar");
	const navbarToggle = document.getElementById("navbarToggle");
	const navbarContent = document.getElementById("navbarContent");
	const navbarBackdrop = document.getElementById("navbarBackdrop");
	const userMenuButton = document.getElementById("userMenuButton");
	const userDropdown = document.getElementById("userDropdown");
	const body = document.body;

	// Toggle mobile menu
	if (navbarToggle && navbarContent) {
		navbarToggle.addEventListener("click", function () {
			const expanded = this.getAttribute("aria-expanded") === "true";
			this.setAttribute("aria-expanded", !expanded);
			navbarContent.classList.toggle("active");
			navbarBackdrop.classList.toggle("active");
			body.classList.toggle("menu-open");

			// Animate nav items when menu opens
			if (!expanded) {
				setTimeout(() => {
					document.querySelectorAll(".navbar-nav .nav-item").forEach((item) => {
						item.classList.add("animate");
					});
				}, 100);
			} else {
				document.querySelectorAll(".navbar-nav .nav-item").forEach((item) => {
					item.classList.remove("animate");
				});
			}
		});

		// Close menu when backdrop is clicked
		if (navbarBackdrop) {
			navbarBackdrop.addEventListener("click", () => {
				navbarToggle.setAttribute("aria-expanded", "false");
				navbarContent.classList.remove("active");
				navbarBackdrop.classList.remove("active");
				body.classList.remove("menu-open");

				document.querySelectorAll(".navbar-nav .nav-item").forEach((item) => {
					item.classList.remove("animate");
				});
			});
		}
	}

	// User dropdown toggle
	if (userMenuButton && userDropdown) {
		userMenuButton.addEventListener("click", function (e) {
			e.stopPropagation();
			const expanded = this.getAttribute("aria-expanded") === "true";
			this.setAttribute("aria-expanded", !expanded);
			userDropdown.classList.toggle("active");
		});

		// Close dropdown when clicking outside
		document.addEventListener("click", (e) => {
			if (
				userDropdown.classList.contains("active") &&
				!userDropdown.contains(e.target) &&
				e.target !== userMenuButton
			) {
				userMenuButton.setAttribute("aria-expanded", "false");
				userDropdown.classList.remove("active");
			}
		});
	}

	// Hide navbar on scroll down, show on scroll up
	let lastScrollTop = 0;
	const scrollThreshold = 70; // Navbar height

	window.addEventListener("scroll", () => {
		const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

		// Add shadow on scroll
		if (scrollTop > 0) {
			navbar.classList.add("scrolled");
		} else {
			navbar.classList.remove("scrolled");
		}

		// Hide/show based on scroll direction
		if (scrollTop > scrollThreshold) {
			if (scrollTop > lastScrollTop) {
				// Scrolling down
				navbar.classList.add("nav-hidden");
			} else {
				// Scrolling up
				navbar.classList.remove("nav-hidden");
			}
		}

		lastScrollTop = scrollTop;
	});

	// Close mobile menu on window resize
	window.addEventListener("resize", () => {
		if (window.innerWidth > 768 && navbarContent.classList.contains("active")) {
			navbarToggle.setAttribute("aria-expanded", "false");
			navbarContent.classList.remove("active");
			navbarBackdrop.classList.remove("active");
			body.classList.remove("menu-open");

			document.querySelectorAll(".navbar-nav .nav-item").forEach((item) => {
				item.classList.remove("animate");
			});
		}
	});
});
