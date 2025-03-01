import Logger from '../modules/logging';

const AdminDashboard = {
    CONFIG: Object.freeze({
        SELECTORS: {
            METRICS: {
                TOTAL_USERS: '#totalUsers',
                ACTIVE_API_KEYS: '#activeApiKeys',
                API_REQUESTS: '#apiRequests',
                AVERAGE_RESPONSE_TIME: '#avgResponseTime',
                SERVICE_GRID: '.service-grid',
                REFRESH_BUTTON: '#refreshMetricsButton'
            }
        }
    }),
    state: {
        refreshInterval: null,
        metrics: null,
        initialized: false
    },
    elements: {},
    initializeElements() {
        Logger.info("Initializing elements");
        const selectors = this.CONFIG.SELECTORS;
        
        // Handle METRICS selectors
        this.elements.METRICS = {};
        for (const [key, selector] of Object.entries(selectors.METRICS)) {
            const element = document.querySelector(selector);
            if (!element) {
                Logger.error(`Element not found for metrics key: ${key}`);
                throw new Error(`Element not found for metrics key: ${key}`);
            }
            this.elements.METRICS[key] = element;
        }
    },
    async refreshMetrics() {
        try {
            const response = await fetch('/api/admin/metrics');
            if (!response.ok) throw new Error('Failed to fetch metrics');

            const metrics = await response.json();
            this.updateMetricsDisplay(metrics);
        } catch (error) {
            Logger.error('Error refreshing metrics:', error);
            window.toast.show('Failed to refresh metrics', 'error');
        }
    },
    updateMetricsDisplay(metrics) {
        this.elements.METRICS.TOTAL_USERS.textContent = metrics.totalUsers;
        this.elements.METRICS.ACTIVE_API_KEYS.textContent = metrics.activeApiKeys;
        this.elements.METRICS.API_REQUESTS.textContent = metrics.apiRequests24Hours
        this.elements.METRICS.AVERAGE_RESPONSE_TIME.textContent = `${metrics.averageResponseTime || 0}ms`;
    },
    async refreshHealth() {
        try {
            const response = await fetch('/api/admin/health');
            if (!response.ok) throw new Error('Failed to fetch health');

            const health = await response.json();
            this.updateHealthDisplay(health);
        } catch (error) {
            Logger.error('Error refreshing health:', error);
            window.toast.show('Failed to refresh health', 'error');
        }
    },
    updateHealthDisplay(health) {
        const serviceGrid = document.querySelector(this.CONFIG.SELECTORS.METRICS.SERVICE_GRID);
        if (!serviceGrid) return;

        Object.entries(health.services).forEach(([service, status]) => {
            const statusElement = serviceGrid.querySelector(`[data-service="${service}"]`);
            if (statusElement) {
                statusElement.className = `service-status ${status.toLowerCase()}`;
            }
        });
    },
    init() {
        if (this.state.initialized) return;

        this.initializeElements();
        this.refreshMetrics();
        this.refreshHealth();
        
        this.state.refreshInterval = setInterval(() => {
            this.refreshMetrics();
            this.refreshHealth();
        }, 30000);

        this.state.initialized = true

        this.elements.METRICS.REFRESH_BUTTON.addEventListener('click', () => {
            this.refreshMetrics();
            this.refreshHealth();
        });
    },
    cleanup() {
        if (this.state.refreshInterval) {
            clearInterval(this.state.refreshInterval);
        }
    }
};

document.addEventListener('DOMContentLoaded', () => {
    AdminDashboard.init();
});

window.addEventListener('unload', () => {
    AdminDashboard.cleanup();
});