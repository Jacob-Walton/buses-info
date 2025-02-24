// wwwroot/js/admin/apiManagement.js
const ApiManagement = {
    state: {
        currentRequestId: null,
        currentApiKey: null,
        chart: null
    },

    init() {
        this.initEventListeners();
        this.initApproveRejectButtons();
        this.initModals();
        this.initSearchFilter();
        this.initUsageChart();
    },

    initEventListeners() {
        // Refresh buttons
        document.getElementById('refreshPendingBtn')?.addEventListener('click', this.refreshPendingRequests);

        // Export button
        document.getElementById('exportStatsBtn')?.addEventListener('click', this.exportApiStats);

        // Copy button for API keys
        document.querySelectorAll('.copy-btn').forEach(btn => {
            btn.addEventListener('click', this.copyApiKey);
        });
    },

    initApproveRejectButtons() {
        // Approve buttons
        document.querySelectorAll('.approve-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                const requestId = e.currentTarget.dataset.requestId;
                this.showApproveModal(requestId);
            });
        });

        // Reject buttons
        document.querySelectorAll('.reject-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                const requestId = e.currentTarget.dataset.requestId;
                this.showRejectModal(requestId);
            });
        });

        // Revoke buttons
        document.querySelectorAll('.revoke-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                const apiKey = e.currentTarget.dataset.key;
                this.showRevokeModal(apiKey);
            });
        });

        // Restore buttons
        document.querySelectorAll('.restore-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                const apiKey = e.currentTarget.dataset.key;
                this.restoreApiKey(apiKey);
            });
        });
    },

    initModals() {
        // Close buttons for all modals
        document.querySelectorAll('.close-modal, .cancel-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.admin-modal').forEach(modal => {
                    modal.classList.remove('active');
                });
            });
        });

        // Confirm approve button
        document.querySelector('.confirm-approve')?.addEventListener('click', this.approveRequest);

        // Confirm reject button
        document.querySelector('.confirm-reject')?.addEventListener('click', this.rejectRequest);

        // Confirm revoke button
        document.querySelector('.confirm-revoke')?.addEventListener('click', this.revokeApiKey);
    },

    initSearchFilter() {
        const searchInput = document.getElementById('apiKeySearch');
        const filterSelect = document.getElementById('apiKeyFilter');

        if (searchInput) {
            searchInput.addEventListener('input', this.filterApiKeys);
        }

        if (filterSelect) {
            filterSelect.addEventListener('change', this.filterApiKeys);
        }
    },

    initUsageChart() {
        const ctx = document.getElementById('apiUsageChart')?.getContext('2d');
        if (!ctx) return;

        // Sample data - in a real implementation, this would come from your API
        const labels = Array.from({ length: 24 }, (_, i) => `${i}:00`);
        const data = Array.from({ length: 24 }, () => Math.floor(Math.random() * 100));

        this.state.chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [{
                    label: 'API Requests',
                    data,
                    borderColor: '#e84430',
                    backgroundColor: 'rgba(232, 68, 48, 0.2)',
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });

        // Handle time range changes
        document.getElementById('timeRange')?.addEventListener('change', e => {
            this.updateChartTimeRange(e.target.value);
        });
    },

    updateChartTimeRange(range) {
        // In a real implementation, this would fetch data for the selected time range
        console.log(`Updating chart for time range: ${range}`);
        
        // Example: update chart with random data
        let labels, data;
        
        switch(range) {
            case '7d':
                labels = Array.from({ length: 7 }, (_, i) => {
                    const d = new Date();
                    d.setDate(d.getDate() - 6 + i);
                    return d.toLocaleDateString();
                });
                data = Array.from({ length: 7 }, () => Math.floor(Math.random() * 1000));
                break;
            case '30d':
                labels = Array.from({ length: 30 }, (_, i) => {
                    const d = new Date();
                    d.setDate(d.getDate() - 29 + i);
                    return d.toLocaleDateString();
                });
                data = Array.from({ length: 30 }, () => Math.floor(Math.random() * 5000));
                break;
            default: // 24h
                labels = Array.from({ length: 24 }, (_, i) => `${i}:00`);
                data = Array.from({ length: 24 }, () => Math.floor(Math.random() * 100));
                break;
        }
        
        if (this.state.chart) {
            this.state.chart.data.labels = labels;
            this.state.chart.data.datasets[0].data = data;
            this.state.chart.update();
        }
    },

    async refreshPendingRequests() {
        try {
            const response = await fetch('/api/admin/api/requests?status=Pending');
            if (!response.ok) throw new Error('Failed to fetch pending requests');
            
            const requests = await response.json();
            this.updatePendingRequestsTable(requests);
        } catch (error) {
            console.error('Error refreshing pending requests:', error);
            window.toast?.show('Failed to refresh pending requests', 'error');
        }
    },

    updatePendingRequestsTable(requests) {
        const tbody = document.querySelector('#pending-requests table tbody');
        if (!tbody) return;
        
        if (requests.length === 0) {
            document.querySelector('#pending-requests .table-responsive').innerHTML = `
                <div class="empty-state">
                    <i class="fas fa-check-circle"></i>
                    <p>No pending API key requests</p>
                </div>
            `;
            return;
        }
        
        tbody.innerHTML = requests.map(request => `
            <tr data-request-id="${request.id}">
                <td>${request.user?.email || 'Unknown'}</td>
                <td>${new Date(request.requestedAt).toLocaleString()}</td>
                <td>${request.reason}</td>
                <td>${request.intendedUse || 'N/A'}</td>
                <td class="action-buttons">
                    <button class="btn-small btn-success approve-btn" data-request-id="${request.id}">
                        Approve
                    </button>
                    <button class="btn-small btn-danger reject-btn" data-request-id="${request.id}">
                        Reject
                    </button>
                </td>
            </tr>
        `).join('');
        
        // Re-initialize event listeners
        this.initApproveRejectButtons();
    },

    showApproveModal(requestId) {
        this.state.currentRequestId = requestId;
        document.getElementById('approveModal').classList.add('active');
        document.getElementById('approveNotes').value = '';
    },

    showRejectModal(requestId) {
        this.state.currentRequestId = requestId;
        document.getElementById('rejectModal').classList.add('active');
        document.getElementById('rejectNotes').value = '';
    },

    showRevokeModal(apiKey) {
        this.state.currentApiKey = apiKey;
        document.getElementById('revokeModal').classList.add('active');
    },

    async approveRequest() {
        const requestId = ApiManagement.state.currentRequestId;
        if (!requestId) return;
        
        const notes = document.getElementById('approveNotes').value;
        
        try {
            const response = await fetch(`/api/admin/api/requests/${requestId}/approve`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ notes })
            });
            
            if (!response.ok) throw new Error('Failed to approve request');
            
            const result = await response.json();
            window.toast?.show('Request approved successfully', 'success');
            
            // Update UI
            document.querySelector(`tr[data-request-id="${requestId}"]`)?.remove();
            
            // Close modal
            document.getElementById('approveModal').classList.remove('active');
            
            // Refresh the lists
            await ApiManagement.refreshPendingRequests();
            
            // Show approved key
            window.toast?.show(`API Key: ${result.apiKey}`, 'info', { persistent: true });
        } catch (error) {
            console.error('Error approving request:', error);
            window.toast?.show('Failed to approve request', 'error');
        }
    },

    async rejectRequest() {
        const requestId = ApiManagement.state.currentRequestId;
        if (!requestId) return;
        
        const notes = document.getElementById('rejectNotes').value;
        if (!notes) {
            window.toast?.show('Please provide a reason for rejection', 'warning');
            return;
        }
        
        try {
            const response = await fetch(`/api/admin/api/requests/${requestId}/reject`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ notes })
            });
            
            if (!response.ok) throw new Error('Failed to reject request');
            
            window.toast?.show('Request rejected successfully', 'success');
            
            // Update UI
            document.querySelector(`tr[data-request-id="${requestId}"]`)?.remove();
            
            // Close modal
            document.getElementById('rejectModal').classList.remove('active');
            
            // Refresh the list
            await ApiManagement.refreshPendingRequests();
        } catch (error) {
            console.error('Error rejecting request:', error);
            window.toast?.show('Failed to reject request', 'error');
        }
    },

    async revokeApiKey() {
        const apiKey = ApiManagement.state.currentApiKey;
        if (!apiKey) return;
        
        try {
            const response = await fetch(`/api/admin/api/keys/${apiKey}/revoke`, {
                method: 'POST'
            });
            
            if (!response.ok) throw new Error('Failed to revoke API key');
            
            window.toast?.show('API key revoked successfully', 'success');
            
            // Update UI
            const row = document.querySelector(`tr[data-key="${apiKey}"]`);
            if (row) {
                const statusCell = row.querySelector('td:nth-last-child(2)');
                const actionsCell = row.querySelector('td:last-child');
                
                if (statusCell) {
                    statusCell.innerHTML = '<span class="status-badge inactive">Inactive</span>';
                }
                
                if (actionsCell) {
                    actionsCell.innerHTML = `
                        <button class="btn-small btn-success restore-btn" data-key="${apiKey}">
                            Restore
                        </button>
                    `;
                }
            }
            
            // Close modal
            document.getElementById('revokeModal').classList.remove('active');
            
            // Re-init listeners
            ApiManagement.initApproveRejectButtons();
        } catch (error) {
            console.error('Error revoking API key:', error);
            window.toast?.show('Failed to revoke API key', 'error');
        }
    },

    async restoreApiKey(apiKey) {
        try {
            const response = await fetch(`/api/admin/api/keys/${apiKey}/restore`, {
                method: 'POST'
            });
            
            if (!response.ok) throw new Error('Failed to restore API key');
            
            window.toast?.show('API key restored successfully', 'success');
            
            // Update UI
            const row = document.querySelector(`tr[data-key="${apiKey}"]`);
            if (row) {
                const statusCell = row.querySelector('td:nth-last-child(2)');
                const actionsCell = row.querySelector('td:last-child');
                
                if (statusCell) {
                    statusCell.innerHTML = '<span class="status-badge active">Active</span>';
                }
                
                if (actionsCell) {
                    actionsCell.innerHTML = `
                        <button class="btn-small btn-danger revoke-btn" data-key="${apiKey}">
                            Revoke
                        </button>
                    `;
                }
            }
            
            // Re-init listeners
            ApiManagement.initApproveRejectButtons();
        } catch (error) {
            console.error('Error restoring API key:', error);
            window.toast?.show('Failed to restore API key', 'error');
        }
    },

    filterApiKeys() {
        const searchTerm = document.getElementById('apiKeySearch')?.value.toLowerCase() || '';
        const filterType = document.getElementById('apiKeyFilter')?.value || 'all';
        
        document.querySelectorAll('#apiKeysList tr').forEach(row => {
            const email = row.cells[0].textContent.toLowerCase();
            const key = row.cells[1].querySelector('code').textContent.toLowerCase();
            const isActive = row.querySelector('.status-badge')?.classList.contains('active');
            const expiryText = row.cells[4].textContent.toLowerCase();
            const isExpiring = row.cells[4].classList.contains('expiring-soon');
            
            const matchesSearch = email.includes(searchTerm) || key.includes(searchTerm);
            let matchesFilter = true;
            
            if (filterType === 'active' && !isActive) {
                matchesFilter = false;
            } else if (filterType === 'expiring' && !isExpiring) {
                matchesFilter = false;
            }
            
            row.style.display = matchesSearch && matchesFilter ? '' : 'none';
        });
    },

    copyApiKey(e) {
        const key = e.currentTarget.dataset.key;
        navigator.clipboard.writeText(key)
            .then(() => {
                window.toast?.show('API key copied to clipboard', 'success');
            })
            .catch(err => {
                console.error('Failed to copy API key:', err);
                window.toast?.show('Failed to copy API key', 'error');
            });
    },

    exportApiStats() {
        const date = new Date().toISOString().split('T')[0];
        const filename = `api-stats-${date}.csv`;
        
        // In a real implementation, this would fetch and format data from the server
        const csvContent = [
            'Date,API Key,User,Endpoint,Response Time,Status Code',
            '2023-01-01,API-123456,user@example.com,/api/v1/businfo,45.2,200',
            '2023-01-01,API-789012,other@example.com,/api/v1/businfo/predictions,120.5,200'
        ].join('\n');
        
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', filename);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};

// Bind methods to maintain 'this' context
Object.keys(ApiManagement).forEach(key => {
    if (typeof ApiManagement[key] === 'function') {
        ApiManagement[key] = ApiManagement[key].bind(ApiManagement);
    }
});

document.addEventListener('DOMContentLoaded', ApiManagement.init);

// Make it available globally
window.ApiManagement = ApiManagement;