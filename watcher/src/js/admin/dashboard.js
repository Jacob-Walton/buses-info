/**
 * Admin Dashboard Module
 * Handles functionality for the admin dashboard
 */
const AdminDashboard = (function() {
    'use strict';
    
    // Configuration
    const CONFIG = {
        REFRESH_INTERVAL: 30000, // 30 seconds
        SELECTORS: {
            TOTAL_USERS: '#totalUsers',
            ACTIVE_API_KEYS: '#activeApiKeys',
            API_REQUESTS: '#apiRequests',
            AVERAGE_RESPONSE_TIME: '#avgResponseTime',
            REFRESH_BUTTON: '#refreshMetricsButton',
            SERVICE_GRID: '.service-grid'
        }
    };
    
    // Private state
    const state = {
        refreshInterval: null,
        lastRefreshTime: null,
        isRefreshing: false,
        initialized: false
    };
    
    // DOM elements
    const elements = {};
    
    /**
     * Initialize elements and event handlers
     */
    function initializeElements() {
        for (const [key, selector] of Object.entries(CONFIG.SELECTORS)) {
            elements[key] = document.querySelector(selector);
            if (!elements[key]) {
                console.warn(`Element not found: ${selector}`);
            }
        }
        
        // Setup event listeners
        if (elements.REFRESH_BUTTON) {
            elements.REFRESH_BUTTON.addEventListener('click', refreshData);
        }
        
        // Setup sidebar toggle
        const sidebarToggle = document.querySelector('.admin-sidebar-toggle');
        const sidebar = document.querySelector('.admin-sidebar');
        
        if (sidebarToggle && sidebar) {
            sidebarToggle.addEventListener('click', () => {
                sidebar.classList.toggle('open');
            });
            
            // Close sidebar when clicking outside
            document.addEventListener('click', (event) => {
                if (sidebar.classList.contains('open') && 
                    !sidebar.contains(event.target) && 
                    !sidebarToggle.contains(event.target)) {
                    sidebar.classList.remove('open');
                }
            });
        }
    }
    
    /**
     * Refresh metrics data
     */
    async function refreshMetrics() {
        try {
            const response = await fetch('/api/admin/metrics');
            if (!response.ok) {
                throw new Error(`Failed to fetch metrics: ${response.status}`);
            }
            
            const data = await response.json();
            updateMetricsDisplay(data);
            return data;
        } catch (error) {
            console.error('Error refreshing metrics:', error);
            if (window.toast) {
                window.toast.show('Failed to refresh metrics', 'error');
            }
            throw error;
        }
    }
    
    /**
     * Refresh health status data
     */
    async function refreshHealth() {
        try {
            const response = await fetch('/api/admin/health');
            if (!response.ok) {
                throw new Error(`Failed to fetch health data: ${response.status}`);
            }
            
            const data = await response.json();
            updateHealthDisplay(data);
            return data;
        } catch (error) {
            console.error('Error refreshing health status:', error);
            throw error;
        }
    }
    
    /**
     * Update metrics display in the UI
     */
    function updateMetricsDisplay(metrics) {
        if (!metrics) return;
        
        if (elements.TOTAL_USERS) {
            elements.TOTAL_USERS.textContent = metrics.totalUsers;
        }
        
        if (elements.ACTIVE_API_KEYS) {
            elements.ACTIVE_API_KEYS.textContent = metrics.activeApiKeys;
        }
        
        if (elements.API_REQUESTS) {
            elements.API_REQUESTS.textContent = metrics.apiRequests24Hours.toLocaleString();
        }
        
        if (elements.AVERAGE_RESPONSE_TIME) {
            elements.AVERAGE_RESPONSE_TIME.textContent = `${metrics.averageResponseTime.toFixed(1)} ms`;
        }
    }
    
    /**
     * Update health status display in the UI
     */
    function updateHealthDisplay(health) {
        if (!health || !health.services || !elements.SERVICE_GRID) return;
        
        // Update service status indicators
        for (const [service, status] of Object.entries(health.services)) {
            const statusElement = elements.SERVICE_GRID.querySelector(`[data-service="${service}"]`);
            if (statusElement) {
                // Remove all status classes and add the current one
                statusElement.className = `service-status ${status.toLowerCase()}`;
            }
        }
    }
    
    /**
     * Refresh all dashboard data
     */
    async function refreshData() {
        if (state.isRefreshing) return;
        
        state.isRefreshing = true;
        
        if (elements.REFRESH_BUTTON) {
            elements.REFRESH_BUTTON.disabled = true;
            elements.REFRESH_BUTTON.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Refreshing...';
        }
        
        try {
            // Refresh metrics and health data in parallel
            await Promise.all([
                refreshMetrics(),
                refreshHealth()
            ]);
            
            state.lastRefreshTime = new Date();
            
            if (window.toast) {
                window.toast.show('Dashboard refreshed successfully', 'success');
            }
        } catch (error) {
            console.error('Error refreshing dashboard data:', error);
        } finally {
            state.isRefreshing = false;
            
            if (elements.REFRESH_BUTTON) {
                elements.REFRESH_BUTTON.disabled = false;
                elements.REFRESH_BUTTON.innerHTML = '<i class="fas fa-sync-alt"></i> Refresh';
            }
        }
    }
    
    /**
     * Initialize the dashboard
     */
    function init() {
        if (state.initialized) return;
        
        console.log('Initializing Admin Dashboard');
        
        initializeElements();
        refreshData();
        
        // Set up auto refresh interval
        state.refreshInterval = setInterval(refreshData, CONFIG.REFRESH_INTERVAL);
        
        state.initialized = true;
    }
    
    /**
     * Clean up resources
     */
    function cleanup() {
        if (state.refreshInterval) {
            clearInterval(state.refreshInterval);
            state.refreshInterval = null;
        }
        
        state.initialized = false;
    }
    
    // Public API
    return {
        init,
        cleanup,
        refresh: refreshData
    };
})();

// Initialize when the DOM is loaded
document.addEventListener('DOMContentLoaded', AdminDashboard.init);

// Clean up when the page is unloaded
window.addEventListener('unload', AdminDashboard.cleanup);