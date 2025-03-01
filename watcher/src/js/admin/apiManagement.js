/**
 * API Management Module
 * A modular, maintainable approach to managing the API administration interface
 */
const ApiManagement = (function() {
    'use strict';
    
    // Private state object
    const state = {
        isLoading: false,
        lastFetchTime: null,
        pendingAction: null
    };
    
    // Cache for DOM elements
    const elements = {};
    
    /**
     * Initialize the module
     */
    function init() {
        cacheElements();
        bindEvents();
        loadApiAnalytics();
        
        // Setup periodic refresh of analytics data (every 5 minutes)
        setInterval(refreshApiAnalytics, 300000);
    }
    
    /**
     * Cache DOM elements for better performance
     */
    function cacheElements() {
        elements.refreshButton = document.getElementById('refreshApiDataButton');
        elements.rejectModal = document.getElementById('rejectModal');
        elements.usageModal = document.getElementById('usageModal');
        elements.usageDetails = document.getElementById('usageDetails');
        elements.rejectForm = document.getElementById('rejectForm');
        elements.rejectRequestId = document.getElementById('rejectRequestId');
        elements.rejectReason = document.getElementById('rejectReason');
        elements.apiAnalytics = document.getElementById('apiAnalytics');
        elements.modals = document.querySelectorAll('.admin-modal');
        elements.confirmTemplate = document.getElementById('confirmModalTemplate');
    }
    
    /**
     * Bind event listeners to DOM elements
     */
    function bindEvents() {
        // Refresh button click
        elements.refreshButton?.addEventListener('click', refreshApiAnalytics);
        
        // Modal close buttons
        document.querySelectorAll('.js-close-modal').forEach(btn => {
            btn.addEventListener('click', closeAllModals);
        });
        
        // Close modals when clicking outside the modal content
        elements.modals.forEach(modal => {
            modal.addEventListener('click', event => {
                if (event.target === modal) {
                    closeAllModals();
                }
            });
        });
        
        // Key press events for accessibility
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                closeAllModals();
            }
        });
        
        // Reject form submission
        elements.rejectForm?.addEventListener('submit', handleRejectFormSubmit);
        
        // View key usage buttons
        document.querySelectorAll('.js-view-usage').forEach(btn => {
            btn.addEventListener('click', () => {
                const key = btn.dataset.key;
                viewKeyDetails(key);
            });
        });
        
        // Revoke key buttons
        document.querySelectorAll('.js-revoke-key').forEach(btn => {
            btn.addEventListener('click', () => {
                const key = btn.dataset.key;
                confirmAction({
                    title: 'Revoke API Key',
                    message: 'Are you sure you want to revoke this API key? This action cannot be undone.',
                    confirmText: 'Revoke Key',
                    confirmClass: 'admin-btn--danger',
                    iconClass: 'fa-ban',
                    onConfirm: () => revokeApiKey(key, btn)
                });
            });
        });
        
        // Approve request buttons
        document.querySelectorAll('.js-approve-request').forEach(btn => {
            btn.addEventListener('click', () => {
                const requestId = btn.dataset.requestId;
                approveRequest(requestId, btn);
            });
        });
        
        // Reject request buttons
        document.querySelectorAll('.js-reject-request').forEach(btn => {
            btn.addEventListener('click', () => {
                const requestId = btn.dataset.requestId;
                showRejectModal(requestId);
            });
        });
    }
    
    /**
     * Refresh API analytics data without reloading the page
     */
    async function refreshApiAnalytics() {
        if (state.isLoading) return;
        
        window.toast.show('Refreshing analytics...', 'info', { loading: true });
        
        try {
            await loadApiAnalytics(true);
            window.toast.show('Analytics refreshed successfully', 'success');
        } catch (error) {
            window.toast.show('Failed to refresh analytics', 'error');
            console.error('Failed to refresh analytics:', error);
        }
    }
    
    /**
     * Show the rejection confirmation dialog
     * @param {string} requestId - The ID of the API key request to reject
     */
    function showRejectModal(requestId) {
        if (!elements.rejectModal || !elements.rejectRequestId) return;
        
        elements.rejectRequestId.value = requestId;
        elements.rejectReason.value = '';
        
        showModal(elements.rejectModal);
        
        // Focus on the reason textarea
        setTimeout(() => {
            elements.rejectReason?.focus();
        }, 100);
    }
    
    /**
     * Handle the rejection form submission
     * @param {Event} event - Form submission event
     */
    function handleRejectFormSubmit(event) {
        event.preventDefault();
        
        const requestId = elements.rejectRequestId.value;
        const reason = elements.rejectReason.value.trim();
        
        if (!reason) {
            window.toast.show('Please provide a reason for rejection', 'error');
            return;
        }
        
        rejectRequest(requestId, reason);
    }
    
    /**
     * View detailed usage statistics for an API key
     * @param {string} key - The API key to view details for
     */
    async function viewKeyDetails(key) {
        if (!elements.usageModal || !elements.usageDetails) return;
        
        showModal(elements.usageModal);
        
        elements.usageDetails.innerHTML = `
            <div class="admin-loading">
                <div class="admin-loading__spinner"></div>
            </div>
        `;
        
        try {
            const response = await fetch(`/api/admin/api-keys/${key}/usage`);
            
            if (!response.ok) {
                throw new Error(`Failed to fetch usage data: ${response.status}`);
            }
            
            const data = await response.json();
            
            // Render usage data
            elements.usageDetails.innerHTML = renderUsageDetails(data, key);
        } catch (error) {
            elements.usageDetails.innerHTML = `
                <div class="admin-section__empty">
                    <p>Failed to load usage data: ${error.message}</p>
                </div>
            `;
            window.toast.show('Error loading usage data', 'error');
        }
    }
    
    /**
     * Render the usage details content
     * @param {Object} data - The usage data from the API
     * @param {string} key - The API key
     * @returns {string} HTML content
     */
    function renderUsageDetails(data, key) {
        return `
            <div class="admin-card">
                <div class="admin-card__header">
                    <h3 class="admin-card__title">API Key Information</h3>
                    <span class="admin-card__icon"><i class="fas fa-key"></i></span>
                </div>
                <div class="admin-form__group">
                    <label class="admin-form__label">API Key</label>
                    <div class="admin-api-key__field">
                        <input type="text" class="admin-api-key__input" value="${key}" readonly />
                        <button type="button" class="admin-api-key__button js-copy-key" data-key="${key}">
                            <i class="fas fa-copy"></i>
                        </button>
                    </div>
                </div>
            </div>
            
            <div class="admin-grid" style="margin-top: 1rem;">
                <div class="admin-card">
                    <div class="admin-card__header">
                        <h3 class="admin-card__title">Today's Requests</h3>
                        <span class="admin-card__icon"><i class="fas fa-calendar-day"></i></span>
                    </div>
                    <div class="admin-card__metric">${data.requestsToday.toLocaleString()}</div>
                </div>
                
                <div class="admin-card">
                    <div class="admin-card__header">
                        <h3 class="admin-card__title">Total Requests</h3>
                        <span class="admin-card__icon"><i class="fas fa-history"></i></span>
                    </div>
                    <div class="admin-card__metric">${data.totalRequests.toLocaleString()}</div>
                </div>
                
                <div class="admin-card">
                    <div class="admin-card__header">
                        <h3 class="admin-card__title">Avg Response Time</h3>
                        <span class="admin-card__icon"><i class="fas fa-clock"></i></span>
                    </div>
                    <div class="admin-card__metric">${data.averageResponseTime.toFixed(1)}ms</div>
                </div>
                
                <div class="admin-card">
                    <div class="admin-card__header">
                        <h3 class="admin-card__title">Success Rate</h3>
                        <span class="admin-card__icon"><i class="fas fa-check-circle"></i></span>
                    </div>
                    <div class="admin-card__metric">${calculateSuccessRate(data.statusCodes)}%</div>
                </div>
            </div>
            
            <div class="admin-section" style="margin-top: 1rem;">
                <div class="admin-section__header">
                    <h2 class="admin-section__title">Status Code Distribution</h2>
                </div>
                <div class="admin-section__content">
                    <div class="admin-chart__container">
                        ${renderStatusCodesChart(data.statusCodes)}
                    </div>
                </div>
            </div>
            
            <div class="admin-section">
                <div class="admin-section__header">
                    <h2 class="admin-section__title">Request Volume Today</h2>
                </div>
                <div class="admin-section__content">
                    <div class="admin-chart__container">
                        ${renderRequestTimeSeriesChart(data.requestsTimeSeries)}
                    </div>
                </div>
            </div>
        `;
    }
    
    /**
     * Calculate success rate from status codes
     * @param {Object} statusCodes - Status code counts
     * @returns {number} Success rate percentage
     */
    function calculateSuccessRate(statusCodes) {
        if (!statusCodes || Object.keys(statusCodes).length === 0) return 0;
        
        let successCount = 0;
        let totalCount = 0;
        
        for (const [code, count] of Object.entries(statusCodes)) {
            totalCount += count;
            
            // Consider 2xx and 3xx as success
            if (code >= 200 && code < 400) {
                successCount += count;
            }
        }
        
        return totalCount > 0 ? Math.round((successCount / totalCount) * 100) : 0;
    }
    
    /**
     * Render status codes as a chart
     * @param {Object} statusCodes - Status code counts
     * @returns {string} HTML for status codes chart
     */
    function renderStatusCodesChart(statusCodes) {
        if (!statusCodes || Object.keys(statusCodes).length === 0) {
            return `
                <div class="admin-chart__placeholder">
                    <p>No status code data available</p>
                </div>
            `;
        }
        
        // Sort codes numerically
        const sortedCodes = Object.entries(statusCodes).sort((a, b) => parseInt(a[0]) - parseInt(b[0]));
        
        // Group by status type
        const grouped = {
            success: sortedCodes.filter(([code]) => code >= 200 && code < 300),
            redirect: sortedCodes.filter(([code]) => code >= 300 && code < 400),
            clientError: sortedCodes.filter(([code]) => code >= 400 && code < 500),
            serverError: sortedCodes.filter(([code]) => code >= 500)
        };
        
        return `
            <div class="admin-status-distribution">
                ${Object.entries(grouped).map(([group, codes]) => codes.length > 0 ? `
                    <div class="admin-status-distribution__group">
                        <h4>${formatStatusGroup(group)}</h4>
                        ${codes.map(([code, count]) => {
                            const percentage = Math.round((count / getTotalCount(statusCodes)) * 100);
                            return `
                                <div class="admin-status-distribution__bar-container">
                                    <div class="admin-status-distribution__bar admin-status-distribution__bar--${group}" 
                                         style="width: ${percentage}%;" 
                                         title="${code}: ${count} requests (${percentage}%)"></div>
                                </div>
                                <div class="admin-status-distribution__label">
                                    <span class="admin-status-distribution__label-code">${code}</span>
                                    <span class="admin-status-distribution__label-count">${count} (${percentage}%)</span>
                                </div>
                            `;
                        }).join('')}
                    </div>
                ` : '').join('')}
            </div>
            
            <div class="admin-chart__legend">
                <div class="admin-chart__legend-item">
                    <span class="admin-chart__legend-color admin-chart__legend-color--success"></span>
                    <span>2xx Success</span>
                </div>
                <div class="admin-chart__legend-item">
                    <span class="admin-chart__legend-color admin-chart__legend-color--warning"></span>
                    <span>3xx Redirect</span>
                </div>
                <div class="admin-chart__legend-item">
                    <span class="admin-chart__legend-color admin-chart__legend-color--error"></span>
                    <span>4xx Client Error</span>
                </div>
                <div class="admin-chart__legend-item">
                    <span class="admin-chart__legend-color" style="background-color: #dc3545;"></span>
                    <span>5xx Server Error</span>
                </div>
            </div>
        `;
    }
    
    /**
     * Render request time series chart
     * @param {Array} timeSeriesData - Array of data points
     * @returns {string} HTML for time series chart
     */
    function renderRequestTimeSeriesChart(timeSeriesData) {
        if (!timeSeriesData || timeSeriesData.length === 0) {
            return `
                <div class="admin-chart__placeholder">
                    <p>No request data available for today</p>
                </div>
            `;
        }
        
        // Sort data points by time
        const sortedData = [...timeSeriesData].sort((a, b) => {
            return a.timeLabel.localeCompare(b.timeLabel);
        });
        
        // Find maximum value for scaling
        const maxValue = Math.max(...sortedData.map(d => d.value));
        
        return `
            <div class="admin-bar-chart">
                <div class="admin-bar-chart__container">
                    ${sortedData.map(point => {
                        const heightPercentage = maxValue > 0 
                            ? Math.max(5, Math.round((point.value / maxValue) * 100))
                            : 0;
                            
                        return `
                            <div class="admin-bar-chart__item">
                                <div class="admin-bar-chart__bar" 
                                     style="height: ${heightPercentage}%;" 
                                     title="${point.value} requests at ${point.timeLabel}">
                                    <span class="admin-bar-chart__value">${point.value}</span>
                                </div>
                                <div class="admin-bar-chart__label">${point.timeLabel}</div>
                            </div>
                        `;
                    }).join('')}
                </div>
            </div>
        `;
    }
    
    /**
     * Format status group name for display
     * @param {string} group - Status group name
     * @returns {string} Formatted group name
     */
    function formatStatusGroup(group) {
        switch (group) {
            case 'success': return 'Success (2xx)';
            case 'redirect': return 'Redirect (3xx)';
            case 'clientError': return 'Client Error (4xx)';
            case 'serverError': return 'Server Error (5xx)';
            default: return group;
        }
    }
    
    /**
     * Get total count across all status codes
     * @param {Object} statusCodes - Status code counts
     * @returns {number} Total count
     */
    function getTotalCount(statusCodes) {
        return Object.values(statusCodes).reduce((sum, count) => sum + count, 0);
    }
    
    /**
     * Load API analytics data and render visualizations
     * @param {boolean} isRefresh - Whether this is a refresh operation
     */
    async function loadApiAnalytics(isRefresh = false) {
        if (state.isLoading) return;
        
        state.isLoading = true;
        
        if (!isRefresh) {
            elements.apiAnalytics.innerHTML = `
                <div class="admin-loading">
                    <div class="admin-loading__spinner"></div>
                </div>
            `;
        }
        
        try {
            const response = await fetch('/api/admin/api-stats/dashboard');
            
            if (!response.ok) {
                throw new Error(`Failed to fetch API analytics: ${response.status}`);
            }
            
            const data = await response.json();
            state.lastFetchTime = new Date();
            
            elements.apiAnalytics.innerHTML = renderAnalytics(data);
            
            // Add event listeners to copy buttons
            document.querySelectorAll('.js-copy-key').forEach(btn => {
                btn.addEventListener('click', () => {
                    const key = btn.dataset.key;
                    navigator.clipboard.writeText(key);
                    window.toast.show('API key copied to clipboard', 'success');
                });
            });
            
        } catch (error) {
            elements.apiAnalytics.innerHTML = `
                <div class="admin-section__empty">
                    <p>Failed to load API analytics: ${error.message}</p>
                    <button class="admin-btn admin-btn--primary" id="retryAnalyticsButton">
                        <i class="admin-btn__icon fas fa-sync"></i>
                        <span>Try Again</span>
                    </button>
                </div>
            `;
            
            // Add event listener to retry button
            document.getElementById('retryAnalyticsButton')?.addEventListener('click', () => {
                loadApiAnalytics();
            });
        } finally {
            state.isLoading = false;
        }
    }
    
    /**
     * Render analytics dashboard
     * @param {Object} data - Dashboard data
     * @returns {string} HTML for analytics dashboard
     */
    function renderAnalytics(data) {
        return `
            <div class="admin-sections">
                <div class="admin-chart__container">
                    <div class="admin-chart__header">Status Code Distribution</div>
                    <div class="admin-chart__body">
                        ${renderStatusDistribution(data.statusCodeDistribution)}
                    </div>
                </div>
                
                <div class="admin-chart__container">
                    <div class="admin-chart__header">Request Volume by Hour (Today)</div>
                    <div class="admin-chart__body">
                        ${renderHourlyRequests(data.hourlyRequests)}
                    </div>
                </div>
                
                <div class="admin-chart__container">
                    <div class="admin-chart__header">Top Endpoints</div>
                    <div class="admin-chart__body">
                        ${renderTopEndpoints(data.topEndpoints)}
                    </div>
                </div>
                
                <div class="admin-chart__container">
                    <div class="admin-chart__header">Success vs Error Rate</div>
                    <div class="admin-chart__body">
                        ${renderErrorRate(data.totalRequests, data.errorCount)}
                    </div>
                </div>
            </div>
            
            <div style="text-align: right; padding-top: 1rem; font-size: 0.75rem; color: var(--dark-grey); font-style: italic;">
                Last updated: ${state.lastFetchTime.toLocaleTimeString()}
            </div>
        `;
    }
    
    /**
     * Render the status code distribution visualization
     * @param {Object} statusCodes - Object with status codes as keys and counts as values
     * @returns {string} HTML for the status distribution chart
     */
    function renderStatusDistribution(statusCodes) {
        if (!statusCodes || Object.keys(statusCodes).length === 0) {
            return '<div class="admin-chart__placeholder"><p>No status code data available</p></div>';
        }
        
        const totalRequests = Object.values(statusCodes).reduce((sum, count) => sum + count, 0);
        
        return `
            <div class="admin-status-distribution">
                ${Object.entries(statusCodes)
                    .sort((a, b) => parseInt(a[0]) - parseInt(b[0]))
                    .map(([code, count]) => {
                        const percentage = totalRequests > 0 
                            ? Math.round((count / totalRequests) * 100) 
                            : 0;
                            
                        const statusClass = getStatusClass(parseInt(code));
                        
                        return `
                            <div class="admin-status-distribution__group">
                                <div class="admin-status-distribution__bar-container">
                                    <div class="admin-status-distribution__bar admin-status-distribution__bar--${statusClass}" 
                                         style="width: ${percentage}%;" 
                                         title="${code}: ${count} requests (${percentage}%)">
                                    </div>
                                </div>
                                <div class="admin-status-distribution__label">
                                    <span class="admin-status-distribution__label-code">${code}</span>
                                    <span class="admin-status-distribution__label-count">${count} (${percentage}%)</span>
                                </div>
                            </div>
                        `;
                    }).join('')}
            </div>
        `;
    }
    
    /**
     * Render hourly request volume chart
     * @param {Object} hourlyData - Object with hours as keys and request counts as values
     * @returns {string} HTML for the hourly requests chart
     */
    function renderHourlyRequests(hourlyData) {
        if (!hourlyData || Object.keys(hourlyData).length === 0) {
            return '<div class="admin-chart__placeholder"><p>No hourly data available</p></div>';
        }
        
        const hours = Object.keys(hourlyData).sort();
        const values = hours.map(hour => hourlyData[hour]);
        const maxValue = Math.max(...values);
        
        return `
            <div class="admin-bar-chart">
                <div class="admin-bar-chart__container">
                    ${hours.map(hour => {
                        const value = hourlyData[hour];
                        const heightPercentage = maxValue > 0 
                            ? Math.max(5, Math.round((value / maxValue) * 100)) 
                            : 0;
                            
                        return `
                            <div class="admin-bar-chart__item">
                                <div class="admin-bar-chart__bar" 
                                     style="height: ${heightPercentage}%;" 
                                     title="${value} requests at ${hour}">
                                    <span class="admin-bar-chart__value">${value}</span>
                                </div>
                                <div class="admin-bar-chart__label">${hour}</div>
                            </div>
                        `;
                    }).join('')}
                </div>
            </div>
        `;
    }
    
    /**
     * Render top endpoints visualization
     * @param {Array} endpoints - Array of endpoint objects
     * @returns {string} HTML for the top endpoints visualization
     */
    function renderTopEndpoints(endpoints) {
        if (!endpoints || endpoints.length === 0) {
            return '<div class="admin-chart__placeholder"><p>No endpoint data available</p></div>';
        }
        
        const totalRequests = endpoints.reduce((sum, endpoint) => sum + endpoint.requestCount, 0);
        
        return `
            <div class="admin-endpoints">
                ${endpoints.map(endpoint => {
                    const percentage = totalRequests > 0 
                        ? Math.round((endpoint.requestCount / totalRequests) * 100) 
                        : 0;
                        
                    return `
                        <div class="admin-endpoints__item">
                            <div class="admin-endpoints__name">${endpoint.endpoint}</div>
                            <div class="admin-endpoints__stats">
                                <div class="admin-endpoints__bar-container">
                                    <div class="admin-endpoints__bar" style="width: ${percentage}%;"></div>
                                </div>
                                <div class="admin-endpoints__count">${endpoint.requestCount}</div>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;
    }
    
    /**
     * Render error rate visualization
     * @param {number} totalRequests - Total number of requests
     * @param {number} errorCount - Number of error responses
     * @returns {string} HTML for the error rate visualization
     */
    function renderErrorRate(totalRequests, errorCount) {
        const successCount = totalRequests - errorCount;
        const successRate = totalRequests > 0 
            ? Math.round((successCount / totalRequests) * 100) 
            : 0;
        const errorRate = totalRequests > 0 
            ? Math.round((errorCount / totalRequests) * 100) 
            : 0;
        
        return `
            <div style="padding: 1rem;">
                <div style="height: 30px; display: flex; border-radius: 4px; overflow: hidden; margin-bottom: 1rem;">
                    <div style="height: 100%; width: ${successRate}%; background-color: var(--status-healthy); color: white; display: flex; align-items: center; justify-content: center; font-size: 0.875rem; font-weight: 600;">
                        ${successRate}%
                    </div>
                    <div style="height: 100%; width: ${errorRate}%; background-color: var(--status-error); color: white; display: flex; align-items: center; justify-content: center; font-size: 0.875rem; font-weight: 600;">
                        ${errorRate}%
                    </div>
                </div>
                
                <div class="admin-chart__legend">
                    <div class="admin-chart__legend-item">
                        <span class="admin-chart__legend-color admin-chart__legend-color--success"></span>
                        <span>Success: ${successCount.toLocaleString()}</span>
                    </div>
                    <div class="admin-chart__legend-item">
                        <span class="admin-chart__legend-color admin-chart__legend-color--error"></span>
                        <span>Error: ${errorCount.toLocaleString()}</span>
                    </div>
                </div>
            </div>
        `;
    }
    
    /**
     * Get CSS class for status code styling
     * @param {number} code - HTTP status code
     * @returns {string} CSS class name
     */
    function getStatusClass(code) {
        if (code >= 200 && code < 300) return 'success';
        if (code >= 300 && code < 400) return 'redirect';
        if (code >= 400 && code < 500) return 'clientError';
        if (code >= 500) return 'serverError';
        return '';
    }
    
    /**
     * Revoke an API key
     * @param {string} key - The API key to revoke
     * @param {HTMLElement} button - The button that was clicked
     */
    async function revokeApiKey(key, button) {
        setButtonLoading(button, true);
        
        try {
            const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            
            const response = await fetch(`/api/admin/api-keys/${key}/revoke`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                }
            });
            
            if (!response.ok) {
                throw new Error(`Failed to revoke API key: ${response.status}`);
            }
            
            window.toast.show('API key revoked successfully', 'success');
            
            // Reload the page to show updated data
            setTimeout(() => {
                window.location.reload();
            }, 1000);
            
        } catch (error) {
            window.toast.show(`Error: ${error.message}`, 'error');
            setButtonLoading(button, false);
        }
    }
    
    /**
     * Approve an API key request
     * @param {number} requestId - The ID of the request to approve
     * @param {HTMLElement} button - The button that was clicked
     */
    async function approveRequest(requestId, button) {
        setButtonLoading(button, true);
        
        try {
            const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            
            const response = await fetch(`/api/admin/api-requests/${requestId}/approve`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                }
            });
            
            if (!response.ok) {
                throw new Error(`Failed to approve request: ${response.status}`);
            }
            
            const data = await response.json();
            
            window.toast.show('API key request approved successfully', 'success', {
                action: {
                    label: 'Copy Key',
                    onClick: () => {
                        navigator.clipboard.writeText(data.apiKey);
                        window.toast.show('API key copied to clipboard', 'success');
                    }
                }
            });
            
            // Reload the page to show updated data
            setTimeout(() => {
                window.location.reload();
            }, 2000);
            
        } catch (error) {
            window.toast.show(`Error: ${error.message}`, 'error');
            setButtonLoading(button, false);
        }
    }
    
    /**
     * Reject an API key request
     * @param {number} requestId - The ID of the request to reject
     * @param {string} reason - The reason for rejection
     */
    async function rejectRequest(requestId, reason) {
        try {
            const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            
            const response = await fetch(`/api/admin/api-requests/${requestId}/reject`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({ reason })
            });
            
            if (!response.ok) {
                throw new Error(`Failed to reject request: ${response.status}`);
            }
            
            window.toast.show('API key request rejected successfully', 'success');
            
            // Hide the rejection modal
            closeAllModals();
            
            // Reload the page to show updated data
            setTimeout(() => {
                window.location.reload();
            }, 1000);
            
        } catch (error) {
            window.toast.show(`Error: ${error.message}`, 'error');
        }
    }
    
    /**
     * Shows a confirmation dialog for an action
     * @param {Object} options - Configuration options
     */
    function confirmAction(options) {
        const {
            title = 'Confirmation',
            message = 'Are you sure you want to perform this action?',
            confirmText = 'Confirm',
            cancelText = 'Cancel',
            confirmClass = 'admin-btn--primary',
            iconClass = 'fa-question-circle',
            iconType = 'warning',
            onConfirm = () => {},
            onCancel = () => {}
        } = options;
        
        // Create modal from template
        const template = elements.confirmTemplate.content.cloneNode(true);
        const modal = template.querySelector('.admin-modal');
        
        // Set modal content
        modal.querySelector('.admin-modal__title').textContent = title;
        modal.querySelector('.admin-confirm__message').innerHTML = `<p>${message}</p>`;
        
        // Set icon
        const iconElement = modal.querySelector('.admin-confirm__icon i');
        iconElement.className = `fas ${iconClass}`;
        modal.querySelector('.admin-confirm__icon').className = `admin-confirm__icon admin-confirm__icon--${iconType}`;
        
        // Set button text and classes
        const confirmButton = modal.querySelector('.js-confirm-action');
        confirmButton.textContent = confirmText;
        confirmButton.className = `admin-btn ${confirmClass} js-confirm-action`;
        
        const cancelButton = modal.querySelector('.js-cancel-action');
        cancelButton.textContent = cancelText;
        
        // Add event listeners
        confirmButton.addEventListener('click', () => {
            document.body.removeChild(modal);
            onConfirm();
        });
        
        cancelButton.addEventListener('click', () => {
            document.body.removeChild(modal);
            onCancel();
        });
        
        modal.querySelector('.js-close-modal').addEventListener('click', () => {
            document.body.removeChild(modal);
            onCancel();
        });
        
        modal.addEventListener('click', event => {
            if (event.target === modal) {
                document.body.removeChild(modal);
                onCancel();
            }
        });
        
        // Add modal to DOM and show it
        document.body.appendChild(modal);
        setTimeout(() => {
            modal.classList.add('admin-modal--active');
        }, 10);
    }
    
    /**
     * Sets a button to loading state
     * @param {HTMLElement} button - The button element
     * @param {boolean} isLoading - Whether to set loading state
     */
    function setButtonLoading(button, isLoading) {
        if (!button) return;
        
        if (isLoading) {
            button.disabled = true;
            button.dataset.originalHtml = button.innerHTML;
            button.innerHTML = `
                <div class="admin-loading admin-loading--button">
                    <div class="admin-loading__spinner"></div>
                </div>
                ${button.textContent}
            `;
            button.classList.add('admin-btn--loading');
        } else {
            button.disabled = false;
            button.innerHTML = button.dataset.originalHtml;
            button.classList.remove('admin-btn--loading');
        }
    }
    
    /**
     * Shows a modal
     * @param {HTMLElement} modal - Modal element to show
     */
    function showModal(modal) {
        if (!modal) return;
        
        // Hide all other modals first
        closeAllModals();
        
        // Show this modal
        modal.classList.add('admin-modal--active');
    }
    
    /**
     * Closes all modals
     */
    function closeAllModals() {
        elements.modals.forEach(modal => {
            modal.classList.remove('admin-modal--active');
        });
    }
    
    // Public API
    return {
        init
    };
})();

// Initialize the module when DOM is loaded
document.addEventListener('DOMContentLoaded', ApiManagement.init);