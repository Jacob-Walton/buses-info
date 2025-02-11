var site = (function (exports) {
    'use strict';

    function initializeAuth() {
        const logoutButton = document.getElementById("logoutButton");
        if (logoutButton) {
            logoutButton.addEventListener("click", handleLogout);
        }
    }

    async function handleLogout() {
        try {
            const response = await fetch("/logout", { method: "POST" });
            if (response.ok) {
                window.location.href = "/";
            } else {
                console.error("Failed to log out");
            }
        } catch (error) {
            console.error(error);
        }
    }

    function initializeMobileNav() {
        const elements = {
            trigger: document.getElementById("mobileMenuTrigger"),
            close: document.getElementById("mobileMenuClose"),
            navbar: document.getElementById("mobileNavbar"),
            overlay: document.getElementById("mobileOverlay"),
            body: document.body
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
            document.querySelectorAll(".mobile-nav-item").forEach(item => {
                item.classList.remove("animate");
            });
        }

        // Event listeners
        elements.trigger?.addEventListener("click", openMobileMenu);
        elements.close?.addEventListener("click", closeMobileMenu);
        elements.overlay?.addEventListener("click", closeMobileMenu);

        document.querySelectorAll(".mobile-nav-link a").forEach(link => {
            link.addEventListener("click", closeMobileMenu);
        });

        window.addEventListener("resize", () => {
            if (window.innerWidth > 768) {
                closeMobileMenu();
            }
        });
    }

    function initializeProfileDropdown() {
        const profileTrigger = document.getElementById("profileTrigger");
        const profileDropdown = document.getElementById("profileDropdown");

        if (!profileTrigger || !profileDropdown) return;

        profileTrigger.addEventListener("click", (e) => {
            e.stopPropagation();
            profileDropdown.classList.toggle("active");
        });

        document.addEventListener("click", (event) => {
            if (!profileDropdown.contains(event.target)) {
                profileDropdown.classList.remove("active");
            }
        });
    }

    function initializeCustomSelects() {
        const customSelects = document.querySelectorAll(".custom-select");

        customSelects.forEach(initializeSelect);

        document.addEventListener("click", closeAllSelect);
    }

    function initializeSelect(customSelect) {
        const selectSelected = customSelect.querySelector(".select-selected");
        const selectItems = customSelect.querySelector(".select-items");

        if (!selectSelected || !selectItems) return;

        selectSelected.addEventListener("click", (e) => {
            e.stopPropagation();
            closeAllSelect(selectSelected);
            selectItems.classList.toggle("select-hide");
            selectSelected.classList.toggle("select-arrow-active");
        });

        initializeOptions(selectItems, selectSelected, customSelect);
    }

    function initializeOptions(selectItems, selectSelected, customSelect) {
        selectItems.querySelectorAll("div").forEach(option => {
            option.addEventListener("click", function(e) {
                e.stopPropagation();
                updateSelection(this, selectSelected, customSelect);
            });
        });
    }

    function updateSelection(option, selectSelected, customSelect) {
        selectSelected.textContent = option.textContent;
        const hiddenInput = customSelect.querySelector('input[type="hidden"]');
        if (hiddenInput) {
            hiddenInput.value = option.dataset.value;
            hiddenInput.dispatchEvent(new Event("change", { bubbles: true }));
        }
        closeSelect(selectSelected);
    }

    function closeSelect(selectSelected) {
        const selectItems = selectSelected.nextElementSibling;
        selectItems.classList.add("select-hide");
        selectSelected.classList.remove("select-arrow-active");
    }

    function closeAllSelect(element) {
        document.querySelectorAll(".custom-select").forEach(select => {
            const selected = select.querySelector(".select-selected");
            const items = select.querySelector(".select-items");
            if (selected && items && selected !== element) {
                items.classList.add("select-hide");
                selected.classList.remove("select-arrow-active");
            }
        });
    }

    function initializeLegalNotices() {
        initializeLegalBanner();
        initializeLegalModal();
        initializeLegalNoticeHandlers();
    }

    function initializeLegalBanner() {
        const legalBanner = document.getElementById("legalBanner");
        const dismissButton = document.getElementById("dismissLegalBanner");

        if (!legalBanner || !dismissButton) return;

        if (!localStorage.getItem("legalBannerDismissed")) {
            legalBanner.style.display = "block";
            document.body.classList.add("has-banner");
        }

        dismissButton.addEventListener("click", () => {
            legalBanner.style.display = "none";
            document.body.classList.remove("has-banner");
            localStorage.setItem("legalBannerDismissed", "true");
        });
    }

    function initializeLegalModal() {
        const legalModal = document.getElementById("legalModal");
        const acknowledgeButton = document.getElementById("acknowledgeLegal");

        if (!legalModal || !acknowledgeButton) return;

        if (!localStorage.getItem("legalNoticeAccepted")) {
            showLegalModal();
        }
    }

    function initializeLegalNoticeHandlers() {
        document.getElementById('showLegalNotice')?.addEventListener('click', (e) => {
            e.preventDefault();
            showLegalModal();
        });

        document.getElementById('acknowledgeLegal')?.addEventListener('click', hideLegalModal);
        document.getElementById('legalModal')?.addEventListener('click', (e) => {
            if (e.target === e.currentTarget) {
                hideLegalModal();
            }
        });
    }

    function showLegalModal() {
        const legalModal = document.getElementById('legalModal');
        if (legalModal) {
            legalModal.style.display = 'flex';
            document.body.style.overflow = 'hidden';
        }
    }

    function hideLegalModal() {
        const legalModal = document.getElementById('legalModal');
        if (legalModal) {
            legalModal.style.display = 'none';
            document.body.style.overflow = '';
            localStorage.setItem("legalNoticeAccepted", "true");
        }
    }

    class ToastManager {
        constructor() {
            this.createToastContainer();
            this.toasts = [];
            this.maxToasts = 5;
            this.height = 64; // Height of each toast + margin
        }

        createToastContainer() {
            const container = document.createElement('div');
            container.id = 'toast-container';
            document.body.appendChild(container);
        }

        show(message, type = 'success', options = {}) {
            const id = Math.random().toString(36).substr(2, 9);
            const toast = this.createToastElement(message, type, options);
            toast.dataset.id = id;
            
            const container = document.getElementById('toast-container');
            container.appendChild(toast);
            
            this.toasts.push({ element: toast, id });

            toast.style.transform = `translateY(${this.height}px) scale(0.9)`;
            toast.style.opacity = '0';

            requestAnimationFrame(() => {
                this.updateToastStack();
            });

            if (!options.persistent) {
                setTimeout(() => {
                    this.dismiss(id);
                }, options.duration || 4000);
            }

            return id;
        }

        updateToastStack() {
            const maxVisible = Math.min(this.toasts.length, this.maxToasts);
            
            this.toasts.forEach((toast, index) => {
                if (index < maxVisible) {
                    const scale = 1 - (index * 0.05);
                    const y = index * (this.height / 2); // Compressed spacing
                    const opacity = 1 - (index * 0.15);
                    
                    toast.element.style.transition = 'all 0.3s cubic-bezier(0.16, 1, 0.3, 1)';
                    toast.element.style.transform = `translateY(-${y}px) scale(${scale})`;
                    toast.element.style.opacity = opacity.toString();
                    toast.element.style.zIndex = (1000 - index).toString();
                } else {
                    // Hide extra toasts
                    toast.element.style.opacity = '0';
                    toast.element.style.transform = 'translateY(0) scale(0.9)';
                }
            });
        }

        dismiss(id) {
            const toast = this.toasts.find(t => t.id === id);
            if (!toast) return;

            const { element } = toast;
            element.style.transition = 'all 0.2s cubic-bezier(0.16, 1, 0.3, 1)';
            element.style.transform = `translateX(calc(100% + 20px))`;
            element.style.opacity = '0';

            setTimeout(() => {
                element.remove();
                this.toasts = this.toasts.filter(t => t.id !== id);
                this.updateToastStack();
            }, 200);
        }

        updateToastPositions() {
            this.toasts.forEach((toast, index) => {
                const offset = index * 64; // Height of toast + gap
                toast.element.style.transform = `translateY(-${offset}px)`;
            });
        }

        createToastElement(message, type, options) {
            const toast = document.createElement('div');
            toast.className = `toast toast-${type}`;
            
            if (options.loading) {
                const spinner = document.createElement('div');
                spinner.className = 'toast-spinner';
                toast.appendChild(spinner);
            } else {
                const icon = document.createElement('i');
                icon.className = this.getIconClass(type);
                toast.appendChild(icon);
            }
            
            const content = document.createElement('div');
            content.className = 'toast-content';
            
            const title = document.createElement('p');
            title.className = 'toast-title';
            title.textContent = options.title || message;
            
            content.appendChild(title);
            
            if (options.title) {
                const description = document.createElement('p');
                description.className = 'toast-description';
                description.textContent = message;
                content.appendChild(description);
            }
            
            toast.appendChild(content);
            
            if (options.action) {
                const button = document.createElement('button');
                button.className = 'toast-action';
                button.textContent = options.action.label;
                button.onclick = options.action.onClick;
                toast.appendChild(button);
            }

            const closeButton = document.createElement('button');
            closeButton.className = 'toast-close';
            closeButton.innerHTML = '×';
            closeButton.onclick = () => this.dismiss(toast.dataset.id);
            toast.appendChild(closeButton);

            return toast;
        }

        getIconClass(type) {
            switch (type) {
                case 'success': return 'fas fa-check-circle';
                case 'error': return 'fas fa-exclamation-circle';
                case 'warning': return 'fas fa-exclamation-triangle';
                default: return 'fas fa-info-circle';
            }
        }
    }

    const toast = new ToastManager();

    window.toast = toast;

    document.addEventListener("DOMContentLoaded", function () {
        initializeAuth();
        initializeMobileNav();
        initializeProfileDropdown();
        initializeCustomSelects();
        initializeLegalNotices();
    });

    exports.toast = toast;

    Object.defineProperty(exports, '__esModule', { value: true });

    return exports;

})({});
//# sourceMappingURL=site.js.map
