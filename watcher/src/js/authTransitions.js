const AuthTransitions = {
    isTransitioning: false,
    currentPage: null,
    
    /**
     * Initialize the transition system
     */
    init: function() {
        this.currentPage = window.location.pathname;
        this.removeExistingListeners();
        this.setupListeners();
    },
    
    /**
     * Remove existing event listeners to prevent duplicates
     */
    removeExistingListeners: function() {
        document.querySelectorAll('.auth-transition-link').forEach(link => {
            link.removeEventListener('click', this.handleLinkClick);
        });
    },
    
    /**
     * Handle transition link click events
     */
    handleLinkClick: function(event) {
        event.preventDefault();
        const link = event.currentTarget;
        const url = link.getAttribute('href');
        AuthTransitions.navigateTo(url);
    },
    
    /**
     * Set up all required event listeners
     */
    setupListeners: function() {
        document.querySelectorAll('.auth-transition-link').forEach(link => {
            link.addEventListener('click', this.handleLinkClick);
        });
        this.setupFormFocusEffects();
    },
    
    /**
     * Determine the transition direction based on current and destination pages
     */
    determineDirection: function(newUrl) {
        const currentPath = this.currentPage.replace(/https?:\/\/[^\/]+/, '');
        const newPath = newUrl.replace(/https?:\/\/[^\/]+/, '');
        
        if (currentPath.includes('login') && newPath.includes('register')) {
            return 'right'; 
        } else if (currentPath.includes('register') && newPath.includes('login')) {
            return 'left';
        } else {
            return newPath.length > currentPath.length ? 'right' : 'left';
        }
    },
    
    /**
     * Navigate to a new page with transition animation
     */
    navigateTo: function(url) {
        if (this.isTransitioning) return;
        
        const container = document.querySelector('.auth-form-container');
        const brandContent = document.querySelector('.auth-background .brand-content');
        
        if (!container) {
            window.location.href = url;
            return;
        }
        
        this.isTransitioning = true;
        const direction = this.determineDirection(url);
        
        // Prepare for outgoing animation
        container.classList.remove('slide-in-left', 'slide-in-right', 'fade-in-left', 'fade-in-right', 'fade-in');
        container.classList.add(direction === 'left' ? 'fade-out-left' : 'fade-out-right');
        
        // Also fade out brand content
        if (brandContent) {
            brandContent.style.transition = 'opacity 0.25s ease';
            brandContent.style.opacity = '0';
        }
        
        setTimeout(() => {
            this.fetchAndReplaceContent(url, container, brandContent, direction);
        }, 250);
    },
    
    /**
     * Fetch new content and replace the current page
     */
    fetchAndReplaceContent: function(url, container, brandContent, direction) {
        const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const headers = new Headers({ 'X-Requested-With': 'XMLHttpRequest' });
        
        if (csrfToken) {
            headers.append('RequestVerificationToken', csrfToken);
        }
        
        fetch(url, {
            headers: headers,
            credentials: 'same-origin',
            redirect: 'error'
        })
        .then(response => {
            if (!response.ok) throw new Error(`HTTP error! Status: ${response.status}`);
            return response.text();
        })
        .then(html => {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');
            const newContent = doc.querySelector('.auth-form-container');
            
            if (!newContent) {
                throw new Error('Content not found in fetched page');
            }
            
            // Update form container content
            container.style.opacity = '0';
            container.innerHTML = newContent.innerHTML;
            
            // Update background brand content if available
            if (brandContent) {
                const newBrandContent = doc.querySelector('.auth-background .brand-content');
                if (newBrandContent) {
                    brandContent.innerHTML = newBrandContent.innerHTML;
                }
            }
            
            // Update page state
            const title = doc.querySelector('title');
            if (title) document.title = title.textContent;
            window.history.pushState({}, document.title, url);
            this.currentPage = url;
            
            // Initialize new content
            this.setupFormFocusEffects();
            this.initializeValidation();
            
            // Prepare for incoming animation
            container.classList.remove('fade-out-left', 'fade-out-right');
            
            setTimeout(() => {
                // Animate form content
                container.style.opacity = '';
                container.classList.add(direction === 'left' ? 'slide-in-right' : 'slide-in-left');
                
                // Animate brand content
                if (brandContent) {
                    brandContent.style.opacity = '1';
                }
                
                setTimeout(() => {
                    this.isTransitioning = false;
                    this.setupListeners();
                }, 400);
            }, 30);
        })
        .catch(error => {
            console.error('Error during page transition:', error);
            window.location.href = url;
        });
    },
    
    /**
     * Initialize form validation if jQuery validation is available
     */
    initializeValidation: function() {
        if (window.jQuery && window.jQuery.validator) {
            window.jQuery("form").each(function() {
                window.jQuery(this).data("validator", null);
                window.jQuery.validator.unobtrusive.parse(window.jQuery(this));
            });
        }
    },
    
    /**
     * Add focus effects to form input fields
     */
    setupFormFocusEffects: function() {
        document.querySelectorAll('.form-group input').forEach(input => {
            input.removeEventListener('focus', this.handleInputFocus);
            input.removeEventListener('blur', this.handleInputBlur);
            
            input.addEventListener('focus', this.handleInputFocus);
            input.addEventListener('blur', this.handleInputBlur);
        });
    },
    
    /**
     * Handle input focus event
     */
    handleInputFocus: function() {
        this.parentElement.classList.add('focused');
    },
    
    /**
     * Handle input blur event
     */
    handleInputBlur: function() {
        this.parentElement.classList.remove('focused');
    }
};

// Initialize transitions when the DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    AuthTransitions.init();
});

// Handle browser back/forward navigation
window.addEventListener('popstate', function() {
    window.location.reload();
});
