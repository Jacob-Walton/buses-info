document.addEventListener('DOMContentLoaded', function() {
    // Initialize content page enhancements
    initContentPageAnimations();
    initStickyTOC();
    initScrollToTop();
    enhanceNoticeBoxes();
    
    // Add icon to last updated timestamps
    document.querySelectorAll('.last-updated').forEach(el => {
        if (!el.querySelector('i')) {
            el.innerHTML = `<i class="fas fa-history"></i> ${el.innerHTML}`;
        }
    });
});

/**
 * Animates content sections as they enter the viewport
 */
function initContentPageAnimations() {
    const animatableElements = document.querySelectorAll('.content-section, .notice-box, .card, .feature-item, .github-box, .contact-method');
    
    // If no elements to animate, exit early
    if (animatableElements.length === 0) return;
    
    // Add animation classes with a staggered delay
    document.querySelectorAll('.content-section').forEach((section, index) => {
        section.classList.add(`animated-delay-${index + 1}`);
    });
    
    // Create intersection observer for animation on scroll
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animated');
                observer.unobserve(entry.target); // Only animate once
            }
        });
    }, {
        root: null,
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });
    
    // Start observing all animatable elements
    animatableElements.forEach(element => {
        observer.observe(element);
    });
}

/**
 * Creates a table of contents for longer content pages
 */
function initStickyTOC() {
    const toc = document.querySelector('.page-toc');
    if (!toc) return;
    
    // Generate TOC from headings
    const headings = document.querySelectorAll('.content-section h2');
    if (headings.length < 3) {
        toc.style.display = 'none'; // Hide TOC if page is short
        return;
    }
    
    const tocList = document.createElement('ul');
    
    headings.forEach((heading, index) => {
        // Add ID to heading if not present
        if (!heading.id) {
            heading.id = `section-${index}`;
        }
        
        const listItem = document.createElement('li');
        const link = document.createElement('a');
        link.href = `#${heading.id}`;
        link.textContent = heading.textContent;
        link.classList.add('toc-link');
        
        link.addEventListener('click', function(e) {
            e.preventDefault();
            document.querySelector(`#${heading.id}`).scrollIntoView({ 
                behavior: 'smooth' 
            });
        });
        
        listItem.appendChild(link);
        tocList.appendChild(listItem);
    });
    
    toc.appendChild(tocList);
    
    // Remove scroll event listener - we don't want the TOC to be sticky anymore
    // const tocContainer = toc.parentElement;
    // const tocOffset = tocContainer.offsetTop;
    
    // window.addEventListener('scroll', function() {
    //     if (window.pageYOffset > tocOffset - 90) {
    //         toc.classList.add('sticky');
    //     } else {
    //         toc.classList.remove('sticky');
    //     }
    // });
}

/**
 * Initialize scroll to top functionality
 */
function initScrollToTop() {
    const scrollTopButton = document.querySelector('.btn-scroll-top');
    if (!scrollTopButton) return;
    
    scrollTopButton.addEventListener('click', function(e) {
        e.preventDefault();
        window.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    });
    
    // Show/hide button based on scroll position
    window.addEventListener('scroll', function() {
        if (window.pageYOffset > 300) {
            scrollTopButton.classList.add('visible');
        } else {
            scrollTopButton.classList.remove('visible');
        }
    });
}

/**
 * Add interactive features to notice boxes
 */
function enhanceNoticeBoxes() {
    document.querySelectorAll('.notice-box').forEach(box => {
        // Add hover effect
        box.addEventListener('mouseenter', function() {
            this.classList.add('notice-hover');
        });
        
        box.addEventListener('mouseleave', function() {
            this.classList.remove('notice-hover');
        });
        
        // Ensure notice boxes have appropriate ARIA roles
        if (!box.getAttribute('role')) {
            box.setAttribute('role', 'region');
            box.setAttribute('aria-label', 'Notice');
        }
    });
}

// Utility functions
function debounce(func, wait = 20) {
    let timeout;
    return function(...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}
