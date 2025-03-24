(function () {
    'use strict';

    /**
     * BusInfo Admin Dashboard JavaScript
     * Handles interactive functionality for the admin dashboard
     */

    document.addEventListener('DOMContentLoaded', function() {
        // Initialize sidebar functionality
        initSidebar();
        
        // Initialize dashboard components
        initGauges();
        initCharts();
        initApiRequestHandlers();
        initDashboardRefresh();
    });

    /**
     * Initialize mobile sidebar toggle functionality
     */
    function initSidebar() {
        const sidebar = document.getElementById('admin-sidebar');
        const sidebarToggle = document.getElementById('sidebar-toggle');
        const sidebarClose = document.getElementById('sidebar-close');
        
        if (sidebarToggle && sidebar && sidebarClose) {
            sidebarToggle.addEventListener('click', function() {
                sidebar.classList.add('open');
            });
            
            sidebarClose.addEventListener('click', function() {
                sidebar.classList.remove('open');
            });
            
            // Close sidebar when clicking outside
            document.addEventListener('click', function(event) {
                if (sidebar.classList.contains('open') && 
                    !sidebar.contains(event.target) && 
                    event.target !== sidebarToggle) {
                    sidebar.classList.remove('open');
                }
            });
        }
    }

    /**
     * Initialize dashboard gauges for system health visualization
     */
    function initGauges() {
        const gaugeElements = document.querySelectorAll('.gauge-chart');
        
        gaugeElements.forEach(gauge => {
            const value = parseFloat(gauge.getAttribute('data-value') || '0');
            updateGauge(gauge, value);
        });
    }

    /**
     * Update a gauge element with a specific value
     * @param {HTMLElement} gaugeElement - The gauge DOM element
     * @param {number} value - The gauge value (0-100)
     */
    function updateGauge(gaugeElement, value) {
        // Clamp value between 0 and 100
        const clampedValue = Math.max(0, Math.min(100, value));
        
        // Calculate the rotation angle (180 degrees = 100%)
        const angle = (clampedValue / 100) * 180;
        
        // Get color based on value
        let color = getColorForValue(clampedValue);
        
        // Apply styles
        gaugeElement.style.setProperty('--gauge-value', `${clampedValue}%`);
        gaugeElement.style.setProperty('--gauge-angle', `${angle}deg`);
        gaugeElement.style.setProperty('--gauge-color', color);
        
        // Apply custom styles with a clip-path for the gauge
        gaugeElement.style.position = 'relative';
        
        // Remove existing pseudo element style if it exists
        const existingStyle = gaugeElement.querySelector('style');
        if (existingStyle) {
            existingStyle.remove();
        }
        
        // Create a new style element with our dynamic pseudo element rules
        const style = document.createElement('style');
        style.textContent = `
        #${gaugeElement.id}::after {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            width: 120px;
            height: 120px;
            border-radius: 50%;
            border: 10px solid transparent;
            border-top-color: ${color};
            border-right-color: ${color};
            transform: rotate(${45 + angle}deg);
            clip-path: polygon(0 0, 100% 0, 100% 50%, 0 50%);
        }
    `;
        
        gaugeElement.appendChild(style);
    }

    /**
     * Get color based on value thresholds
     * @param {number} value - The value to determine color for
     * @returns {string} - CSS color value
     */
    function getColorForValue(value) {
        if (value <= 60) {
            return '#28a745'; // Green for good
        } else if (value <= 80) {
            return '#ffc107'; // Yellow for warning
        } else {
            return '#dc3545'; // Red for danger
        }
    }

    /**
     * Initialize charts for visualizing API usage data
     */
    function initCharts() {
        initApiUsageChart();
    }

    /**
     * Initialize the API usage chart
     */
    function initApiUsageChart() {
        const ctx = document.getElementById('apiUsageChart');
        if (!ctx) return;
        
        // Sample data - in production this would come from an API
        const apiData = {
            labels: ['00:00', '03:00', '06:00', '09:00', '12:00', '15:00', '18:00', '21:00'],
            datasets: [{
                label: 'Requests',
                data: [450, 310, 280, 840, 1200, 980, 750, 530],
                borderColor: '#e84430',
                backgroundColor: 'rgba(232, 68, 48, 0.1)',
                borderWidth: 2,
                tension: 0.4,
                fill: true
            }]
        };
        
        // Create the chart
        const apiUsageChart = new Chart(ctx, {
            type: 'line',
            data: apiData,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(19, 37, 60, 0.9)',
                        titleFont: {
                            size: 13
                        },
                        bodyFont: {
                            size: 12
                        },
                        callbacks: {
                            label: function(context) {
                                return `Requests: ${context.raw.toLocaleString()}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
        
        // Handle time range selector
        const timeRangeSelector = document.getElementById('apiChartTimeRange');
        if (timeRangeSelector) {
            timeRangeSelector.addEventListener('change', function() {
                // In a real app, this would fetch different data based on the selection
                let newData;
                
                switch(this.value) {
                    case 'week':
                        newData = [3200, 2800, 4100, 5400, 4800, 5900, 6200];
                        apiUsageChart.data.labels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
                        break;
                    case 'month':
                        newData = [14500, 18700, 16300, 19200, 15800, 17400, 20100, 18500, 16700, 15400];
                        apiUsageChart.data.labels = ['1', '3', '6', '9', '12', '15', '18', '21', '24', '27', '30'];
                        break;
                    default: // day
                        newData = [450, 310, 280, 840, 1200, 980, 750, 530];
                        apiUsageChart.data.labels = ['00:00', '03:00', '06:00', '09:00', '12:00', '15:00', '18:00', '21:00'];
                }
                
                apiUsageChart.data.datasets[0].data = newData;
                apiUsageChart.update();
            });
        }
    }

    /**
     * Initialize API request handlers for the approve/reject functionality
     */
    function initApiRequestHandlers() {
        // Handle API request approval buttons
        const approveButtons = document.querySelectorAll('.approve-request-btn');
        approveButtons.forEach(button => {
            button.addEventListener('click', function() {
                const requestId = this.getAttribute('data-request-id');
                if (requestId) {
                    approveApiRequest(requestId);
                }
            });
        });
        
        // Handle API request rejection buttons
        const rejectButtons = document.querySelectorAll('.reject-request-btn');
        rejectButtons.forEach(button => {
            button.addEventListener('click', function() {
                const requestId = this.getAttribute('data-request-id');
                if (requestId) {
                    showRejectModal(requestId);
                }
            });
        });
    }

    /**
     * Handle approving an API request
     * @param {string} requestId - The ID of the request to approve
     */
    function approveApiRequest(requestId) {
        if (confirm('Are you sure you want to approve this API key request?')) {
            // Show loading state
            showLoadingOverlay();
            
            // In production, this would be a real API call
            fetch(`/api/admin/api-requests/${requestId}/approve`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': getCsrfToken()
                }
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                // Show success notification
                showNotification('API key request approved successfully', 'success');
                
                // Remove the row from the table or update UI
                const row = document.querySelector(`[data-request-id="${requestId}"]`).closest('tr');
                if (row) {
                    row.classList.add('fade-out');
                    setTimeout(() => {
                        row.remove();
                        updatePendingRequestsCounter(-1);
                        checkEmptyTable();
                    }, 300);
                }
            })
            .catch(error => {
                console.error('Error approving request:', error);
                showNotification('Error approving request. Please try again.', 'error');
            })
            .finally(() => {
                hideLoadingOverlay();
            });
        }
    }

    /**
     * Show the rejection modal to collect rejection reason
     * @param {string} requestId - The ID of the request to reject
     */
    function showRejectModal(requestId) {
        // Create modal if it doesn't exist
        let modal = document.getElementById('rejectModal');
        
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'rejectModal';
            modal.className = 'admin-modal';
            modal.innerHTML = `
            <div class="admin-modal__content">
                <div class="admin-modal__header">
                    <h3>Reject API Key Request</h3>
                    <button class="admin-modal__close" id="closeRejectModal">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="admin-modal__body">
                    <p>Please provide a reason for rejecting this request:</p>
                    <textarea id="rejectReason" class="admin-textarea" placeholder="Rejection reason..."></textarea>
                </div>
                <div class="admin-modal__footer">
                    <button class="admin-btn admin-btn--outline" id="cancelReject">Cancel</button>
                    <button class="admin-btn admin-btn--danger" id="confirmReject">Reject Request</button>
                </div>
            </div>
        `;
            
            document.body.appendChild(modal);
            
            // Close button handler
            document.getElementById('closeRejectModal').addEventListener('click', function() {
                closeRejectModal();
            });
            
            // Cancel button handler
            document.getElementById('cancelReject').addEventListener('click', function() {
                closeRejectModal();
            });
            
            // Click outside to close
            modal.addEventListener('click', function(e) {
                if (e.target === modal) {
                    closeRejectModal();
                }
            });
        }
        
        // Show the modal
        modal.classList.add('active');
        document.body.classList.add('modal-open');
        
        // Set up the confirm button with the request ID
        const confirmButton = document.getElementById('confirmReject');
        
        // Remove existing event listeners
        const newConfirmButton = confirmButton.cloneNode(true);
        confirmButton.parentNode.replaceChild(newConfirmButton, confirmButton);
        
        // Add new event listener
        newConfirmButton.addEventListener('click', function() {
            const reason = document.getElementById('rejectReason').value.trim();
            
            if (!reason) {
                document.getElementById('rejectReason').classList.add('error');
                return;
            }
            
            rejectApiRequest(requestId, reason);
            closeRejectModal();
        });
    }

    /**
     * Close the rejection modal
     */
    function closeRejectModal() {
        const modal = document.getElementById('rejectModal');
        if (modal) {
            modal.classList.remove('active');
            document.body.classList.remove('modal-open');
            document.getElementById('rejectReason').value = '';
            document.getElementById('rejectReason').classList.remove('error');
        }
    }

    /**
     * Handle rejecting an API request
     * @param {string} requestId - The ID of the request to reject
     * @param {string} reason - The reason for rejection
     */
    function rejectApiRequest(requestId, reason) {
        // Show loading state
        showLoadingOverlay();
        
        // In production, this would be a real API call
        fetch(`/api/admin/api-requests/${requestId}/reject`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': getCsrfToken()
            },
            body: JSON.stringify({ reason: reason })
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            // Show success notification
            showNotification('API key request rejected', 'warning');
            
            // Remove the row from the table or update UI
            const row = document.querySelector(`[data-request-id="${requestId}"]`).closest('tr');
            if (row) {
                row.classList.add('fade-out');
                setTimeout(() => {
                    row.remove();
                    updatePendingRequestsCounter(-1);
                    checkEmptyTable();
                }, 300);
            }
        })
        .catch(error => {
            console.error('Error rejecting request:', error);
            showNotification('Error rejecting request. Please try again.', 'error');
        })
        .finally(() => {
            hideLoadingOverlay();
        });
    }

    /**
     * Initialize dashboard refresh functionality
     */
    function initDashboardRefresh() {
        const refreshButton = document.getElementById('refreshDashboardBtn');
        if (refreshButton) {
            refreshButton.addEventListener('click', function() {
                // Show loading state
                showLoadingOverlay();
                
                // Simple reload for now - in a real app this would fetch data via AJAX
                setTimeout(() => {
                    location.reload();
                }, 500);
            });
        }
        
        const refreshHealthBtn = document.getElementById('refreshHealthBtn');
        if (refreshHealthBtn) {
            refreshHealthBtn.addEventListener('click', function() {
                refreshSystemHealth();
            });
        }
    }

    /**
     * Refresh system health data
     */
    function refreshSystemHealth() {
        // Add spinner to the button
        const button = document.getElementById('refreshHealthBtn');
        const originalContent = button.innerHTML;
        button.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        button.disabled = true;
        
        // In production, this would be a real API call
        fetch('/api/admin/health')
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                // Update CPU gauge
                const cpuGauge = document.getElementById('cpuGauge');
                if (cpuGauge) {
                    updateGauge(cpuGauge, data.cpuUsage);
                    cpuGauge.parentElement.querySelector('.gauge-value').textContent = `${data.cpuUsage.toFixed(1)}%`;
                }
                
                // Update Memory gauge
                const memoryGauge = document.getElementById('memoryGauge');
                if (memoryGauge) {
                    updateGauge(memoryGauge, data.memoryUsage);
                    memoryGauge.parentElement.querySelector('.gauge-value').textContent = `${data.memoryUsage.toFixed(1)}%`;
                }
                
                // Update Disk gauge
                const diskGauge = document.getElementById('diskGauge');
                if (diskGauge) {
                    updateGauge(diskGauge, data.diskUsage);
                    diskGauge.parentElement.querySelector('.gauge-value').textContent = `${data.diskUsage.toFixed(1)}%`;
                }
                
                // Update service statuses
                updateServiceStatuses(data.services);
                
                // Show success notification
                showNotification('System health data updated', 'success');
            })
            .catch(error => {
                console.error('Error fetching health data:', error);
                showNotification('Error updating system health data', 'error');
            })
            .finally(() => {
                // Restore button
                button.innerHTML = originalContent;
                button.disabled = false;
            });
    }

    /**
     * Update service statuses in the UI
     * @param {Object} services - Object containing service status data
     */
    function updateServiceStatuses(services) {
        const serviceGrid = document.querySelector('.admin-service-grid');
        if (!serviceGrid) return;
        
        // Clear existing services
        serviceGrid.innerHTML = '';
        
        // Add updated services
        for (const [name, status] of Object.entries(services)) {
            const icon = getServiceIcon(name);
            const statusClass = status.toLowerCase();
            
            const serviceItem = document.createElement('div');
            serviceItem.className = 'service-item';
            serviceItem.innerHTML = `
            <div class="service-icon">
                <i class="fas ${icon}"></i>
            </div>
            <div class="service-info">
                <div class="service-name">${name}</div>
                <div class="service-status service-status--${statusClass}">
                    ${status}
                </div>
            </div>
        `;
            
            serviceGrid.appendChild(serviceItem);
        }
    }

    /**
     * Get icon class for a service based on its name
     * @param {string} serviceName - The name of the service
     * @returns {string} - Font Awesome icon class
     */
    function getServiceIcon(serviceName) {
        const serviceIcons = {
            'database': 'fa-database',
            'api': 'fa-cloud',
            'cache': 'fa-memory',
            'email': 'fa-envelope',
            'web': 'fa-globe',
            'authentication': 'fa-lock',
            'job': 'fa-tasks'
        };
        
        return serviceIcons[serviceName.toLowerCase()] || 'fa-cog';
    }

    /**
     * Update the pending requests counter
     * @param {number} change - The amount to change the counter by
     */
    function updatePendingRequestsCounter(change) {
        const counter = document.getElementById('pendingRequests');
        if (counter) {
            const currentValue = parseInt(counter.textContent, 10) || 0;
            counter.textContent = Math.max(0, currentValue + change);
        }
    }

    /**
     * Check if the table is empty and show empty state if needed
     */
    function checkEmptyTable() {
        const table = document.querySelector('.admin-table');
        if (!table) return;
        
        const tbody = table.querySelector('tbody');
        const rows = tbody.querySelectorAll('tr');
        
        if (rows.length === 0) {
            const container = table.closest('.admin-card__body');
            table.closest('.admin-table-responsive').remove();
            
            const emptyState = document.createElement('div');
            emptyState.className = 'admin-empty';
            emptyState.innerHTML = `
            <div class="admin-empty__icon">
                <i class="fas fa-paper-plane"></i>
            </div>
            <div class="admin-empty__title">No pending requests</div>
            <div class="admin-empty__message">All API key requests have been processed</div>
        `;
            
            container.appendChild(emptyState);
        }
    }

    /**
     * Show a loading overlay
     */
    function showLoadingOverlay() {
        let overlay = document.querySelector('.admin-loading-overlay');
        
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'admin-loading-overlay';
            overlay.innerHTML = `
            <div class="admin-loading-spinner">
                <i class="fas fa-circle-notch fa-spin"></i>
            </div>
        `;
            document.body.appendChild(overlay);
        }
        
        setTimeout(() => {
            overlay.classList.add('active');
        }, 0);
    }

    /**
     * Hide the loading overlay
     */
    function hideLoadingOverlay() {
        const overlay = document.querySelector('.admin-loading-overlay');
        if (overlay) {
            overlay.classList.remove('active');
            setTimeout(() => {
                overlay.remove();
            }, 300);
        }
    }

    /**
     * Show a notification message
     * @param {string} message - The message to display
     * @param {string} type - The type of notification (success, error, warning, info)
     */
    function showNotification(message, type = 'info') {
        // Create notification container if it doesn't exist
        let container = document.querySelector('.admin-notifications');
        if (!container) {
            container = document.createElement('div');
            container.className = 'admin-notifications';
            document.body.appendChild(container);
        }
        
        // Create a new notification
        const notification = document.createElement('div');
        notification.className = `admin-notification admin-notification--${type}`;
        
        // Get icon based on type
        let icon;
        switch (type) {
            case 'success':
                icon = 'fa-check-circle';
                break;
            case 'error':
                icon = 'fa-exclamation-circle';
                break;
            case 'warning':
                icon = 'fa-exclamation-triangle';
                break;
            default:
                icon = 'fa-info-circle';
        }
        
        notification.innerHTML = `
        <div class="admin-notification__icon">
            <i class="fas ${icon}"></i>
        </div>
        <div class="admin-notification__content">
            ${message}
        </div>
        <button class="admin-notification__close">
            <i class="fas fa-times"></i>
        </button>
    `;
        
        // Add to container
        container.appendChild(notification);
        
        // Add close button functionality
        notification.querySelector('.admin-notification__close').addEventListener('click', function() {
            notification.classList.add('closing');
            setTimeout(() => {
                notification.remove();
            }, 300);
        });
        
        // Auto-remove after 5 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                notification.classList.add('closing');
                setTimeout(() => {
                    notification.remove();
                }, 300);
            }
        }, 5000);
        
        // Animate in
        setTimeout(() => {
            notification.classList.add('active');
        }, 10);
    }

    /**
     * Get the CSRF token from the page
     * @returns {string} - The CSRF token
     */
    function getCsrfToken() {
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenElement ? tokenElement.value : '';
    }

})();
//# sourceMappingURL=dashboard.js.map
