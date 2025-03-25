/**
 * Admin Panel JavaScript
 * Handles dashboard, API key management, and user management functionality
 */

document.addEventListener("DOMContentLoaded", function () {
  // Initialize the specific admin page based on URL
  const currentPath = window.location.pathname;
  if (currentPath === "/Admin" || currentPath === "/Admin/") {
    initializeDashboard();
  } else if (currentPath === "/Admin/ApiKeys") {
    initializeApiKeyManagement();
  } else if (currentPath === "/Admin/Users") {
    initializeUserManagement();
  }
});

/**
 * Dashboard initialization and data loading
 */
function initializeDashboard() {
  loadDashboardData();

  // Set up refresh button
  document.getElementById("refreshDashboard")?.addEventListener("click", loadDashboardData);
}

async function loadDashboardData() {
  try {
    const response = await fetch("/api/admin/dashboard");
    if (!response.ok) {
      throw new Error("Failed to load dashboard data");
    }

    const data = await response.json();
    updateDashboardUI(data);
  } catch (error) {
    console.error("Error loading dashboard data:", error);
    // Show error toast or notification
  }
}

function updateDashboardUI(data) {
  document.getElementById("userCount").textContent = data.userCount;
  document.getElementById("apiKeyCount").textContent = data.activeApiKeys;
  document.getElementById("pendingRequestCount").textContent = data.pendingApiKeyRequests;
  
  const lastUpdated = new Date(data.lastUpdated);
  document.getElementById("lastUpdated").textContent = `Last updated: ${lastUpdated.toLocaleString()}`;
}

/**
 * API Key Management initialization and functionality
 */
function initializeApiKeyManagement() {
  // Initialize tabs
  const tabButtons = document.querySelectorAll('.tab-button');
  tabButtons.forEach(button => {
    button.addEventListener('click', () => {
      const tabId = button.getAttribute('data-tab');
      switchTab(tabId);
    });
  });

  // Set up filter for API key requests
  document.getElementById('requestStatusFilter')?.addEventListener('change', (e) => {
    loadApiKeyRequests(e.target.value);
  });

  // Set up search for API keys
  document.getElementById('apiKeySearch')?.addEventListener('input', (e) => {
    filterApiKeys(e.target.value);
  });

  // Initial data loading
  loadApiKeyRequests('all');
  loadApiKeys();

  // Set up modals
  setupReviewModal();
  setupMetricsModal();
}

function switchTab(tabId) {
  // Update active tab button
  document.querySelectorAll('.tab-button').forEach(btn => {
    btn.classList.toggle('active', btn.getAttribute('data-tab') === tabId);
  });

  // Show the selected tab content and hide others
  document.querySelectorAll('.tab-content').forEach(content => {
    content.classList.toggle('active', content.id === `${tabId}-tab`);
  });
}

async function loadApiKeyRequests(status = 'all') {
  const loadingElement = document.getElementById('apiKeyRequestsLoading');
  const errorElement = document.getElementById('apiKeyRequestsError');
  const emptyElement = document.getElementById('apiKeyRequestsEmpty');
  const listElement = document.getElementById('apiKeyRequestsList');

  try {
    showElement(loadingElement);
    hideElement(errorElement);
    hideElement(emptyElement);
    hideElement(listElement);

    const response = await fetch(`/api/admin/api-key-requests?status=${status}`);
    if (!response.ok) {
      throw new Error("Failed to load API key requests");
    }

    const requests = await response.json();
    
    // Update counter
    document.getElementById('requestCount').textContent = requests.length;

    if (requests.length === 0) {
      showElement(emptyElement);
    } else {
      renderApiKeyRequests(requests, listElement);
      showElement(listElement);
    }
  } catch (error) {
    console.error("Error loading API key requests:", error);
    showElement(errorElement);
  } finally {
    hideElement(loadingElement);
  }
}

function renderApiKeyRequests(requests, container) {
  container.innerHTML = '';

  requests.forEach(request => {
    const requestElement = document.createElement('div');
    requestElement.className = `request-item ${request.status.toLowerCase()}`;
    
    const requestDate = new Date(request.requestedAt);
    const updatedDate = request.updatedAt ? new Date(request.updatedAt) : null;
    
    requestElement.innerHTML = `
      <div class="request-header">
        <div class="request-user">
          <div class="user-name">${escapeHtml(request.userName)}</div>
          <div class="user-email">${escapeHtml(request.userEmail)}</div>
        </div>
        <div class="request-meta">
          <div class="request-date">Requested: ${requestDate.toLocaleDateString()}</div>
          <div class="request-status ${request.status.toLowerCase()}">${request.status}</div>
        </div>
      </div>
      <div class="request-body">
        <div class="detail-row">
          <div class="detail-label">Reason:</div>
          <div class="detail-value">${escapeHtml(request.reason)}</div>
        </div>
        <div class="detail-row">
          <div class="detail-label">Intended Use:</div>
          <div class="detail-value">${escapeHtml(request.intendedUse)}</div>
        </div>
        ${updatedDate ? `
          <div class="detail-row">
            <div class="detail-label">Last Updated:</div>
            <div class="detail-value">${updatedDate.toLocaleString()}</div>
          </div>
        ` : ''}
        ${request.reviewedBy ? `
          <div class="detail-row">
            <div class="detail-label">Reviewed By:</div>
            <div class="detail-value">${escapeHtml(request.reviewedBy)}</div>
          </div>
        ` : ''}
        ${request.reviewNotes ? `
          <div class="detail-row">
            <div class="detail-label">Review Notes:</div>
            <div class="detail-value">${escapeHtml(request.reviewNotes)}</div>
          </div>
        ` : ''}
        ${request.rejectionReason ? `
          <div class="detail-row">
            <div class="detail-label">Rejection Reason:</div>
            <div class="detail-value">${escapeHtml(request.rejectionReason)}</div>
          </div>
        ` : ''}
      </div>
      <div class="request-actions">
        ${request.status === 'Pending' ? `
          <button class="button primary review-button" data-request-id="${request.id}">
            <i class="fas fa-clipboard-check"></i> Review
          </button>
        ` : ''}
      </div>
    `;
    
    container.appendChild(requestElement);
    
    // Add event listener for review button
    const reviewButton = requestElement.querySelector('.review-button');
    if (reviewButton) {
      reviewButton.addEventListener('click', () => {
        openReviewModal(request);
      });
    }
  });
}

async function loadApiKeys() {
  const loadingElement = document.getElementById('apiKeysLoading');
  const errorElement = document.getElementById('apiKeysError');
  const emptyElement = document.getElementById('apiKeysEmpty');
  const listElement = document.getElementById('apiKeysList');

  try {
    showElement(loadingElement);
    hideElement(errorElement);
    hideElement(emptyElement);
    hideElement(listElement);

    const response = await fetch('/api/admin/api-keys');
    if (!response.ok) {
      throw new Error("Failed to load API keys");
    }

    const apiKeys = await response.json();
    
    // Update counter
    document.getElementById('apiKeyCount').textContent = apiKeys.length;

    if (apiKeys.length === 0) {
      showElement(emptyElement);
    } else {
      renderApiKeys(apiKeys, listElement);
      showElement(listElement);
    }
  } catch (error) {
    console.error("Error loading API keys:", error);
    showElement(errorElement);
  } finally {
    hideElement(loadingElement);
  }
}

function renderApiKeys(apiKeys, container) {
  container.innerHTML = '';

  apiKeys.forEach(apiKey => {
    const keyElement = document.createElement('div');
    keyElement.className = `api-key-item ${apiKey.isActive ? 'active' : 'inactive'}`;
    keyElement.dataset.key = apiKey.key;
    keyElement.dataset.userName = apiKey.userName;
    keyElement.dataset.userEmail = apiKey.userEmail;
    
    const createdDate = new Date(apiKey.createdAt);
    const lastUsedDate = new Date(apiKey.lastUsed);
    
    keyElement.innerHTML = `
      <div class="api-key-header">
        <div class="api-key-user">
          <div class="user-name">${escapeHtml(apiKey.userName)}</div>
          <div class="user-email">${escapeHtml(apiKey.userEmail)}</div>
        </div>
        <div class="api-key-toggle">
          <label class="switch">
            <input type="checkbox" class="toggle-switch" ${apiKey.isActive ? 'checked' : ''}>
            <span class="slider"></span>
          </label>
        </div>
      </div>
      <div class="api-key-body">
        <div class="api-key-value">
          <code>${apiKey.key}</code>
          <button class="copy-button" data-clipboard-text="${apiKey.key}">
            <i class="fas fa-copy"></i>
          </button>
        </div>
        <div class="api-key-meta">
          <div class="meta-item">
            <i class="fas fa-calendar-plus"></i> Created: ${createdDate.toLocaleDateString()}
          </div>
          <div class="meta-item">
            <i class="fas fa-clock"></i> Last used: ${lastUsedDate.toLocaleDateString()}
          </div>
        </div>
      </div>
      <div class="api-key-actions">
        <button class="button secondary view-metrics" data-key="${apiKey.key}">
          <i class="fas fa-chart-line"></i> View Metrics
        </button>
      </div>
    `;
    
    container.appendChild(keyElement);
    
    // Add event listener for toggle switch
    const toggleSwitch = keyElement.querySelector('.toggle-switch');
    toggleSwitch.addEventListener('change', () => {
      toggleApiKey(apiKey.key);
    });
    
    // Add event listener for copy button
    const copyButton = keyElement.querySelector('.copy-button');
    copyButton.addEventListener('click', () => {
      copyToClipboard(apiKey.key);
      alert('API key copied to clipboard!');
    });
    
    // Add event listener for metrics button
    const metricsButton = keyElement.querySelector('.view-metrics');
    metricsButton.addEventListener('click', () => {
      openMetricsModal(apiKey.key, apiKey.userName);
    });
  });
}

function filterApiKeys(searchTerm) {
  const apiKeyItems = document.querySelectorAll('.api-key-item');
  const lowerSearchTerm = searchTerm.toLowerCase();
  
  apiKeyItems.forEach(item => {
    const userName = item.dataset.userName.toLowerCase();
    const userEmail = item.dataset.userEmail.toLowerCase();
    const apiKey = item.dataset.key.toLowerCase();
    
    const matches = userName.includes(lowerSearchTerm) || 
                   userEmail.includes(lowerSearchTerm) || 
                   apiKey.includes(lowerSearchTerm);
    
    item.style.display = matches ? 'block' : 'none';
  });
  
  // Update count of visible items
  const visibleCount = document.querySelectorAll('.api-key-item[style="display: block"]').length;
  document.getElementById('apiKeyCount').textContent = visibleCount;
}

async function toggleApiKey(key) {
  try {
    const response = await fetch(`/api/admin/api-keys/${key}/toggle`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });
    
    if (!response.ok) {
      throw new Error("Failed to toggle API key");
    }
    
    const result = await response.json();
    
    // Update UI to reflect the new state
    const keyElement = document.querySelector(`.api-key-item[data-key="${key}"]`);
    if (keyElement) {
      keyElement.classList.toggle('active', result.isActive);
      keyElement.classList.toggle('inactive', !result.isActive);
    }
  } catch (error) {
    console.error("Error toggling API key:", error);
    alert("Failed to update API key status. Please try again.");
  }
}

function setupReviewModal() {
  const modal = document.getElementById('reviewModal');
  const closeButton = modal.querySelector('.close-button');
  const cancelButton = document.getElementById('cancelButton');
  const approveButton = document.getElementById('approveButton');
  const rejectButton = document.getElementById('rejectButton');
  
  closeButton.addEventListener('click', closeReviewModal);
  cancelButton.addEventListener('click', closeReviewModal);
  
  approveButton.addEventListener('click', () => {
    submitReview('Approved');
  });
  
  rejectButton.addEventListener('click', () => {
    // Show rejection reason field
    document.getElementById('rejectionReasonContainer').classList.remove('hidden');
    
    // Check if rejection reason is provided
    const rejectionReason = document.getElementById('rejectionReason').value.trim();
    if (rejectionReason) {
      submitReview('Rejected');
    } else {
      alert('Please provide a reason for rejection.');
    }
  });
}

function openReviewModal(request) {
  const modal = document.getElementById('reviewModal');
  
  // Populate request details
  document.getElementById('reviewRequestId').value = request.id;
  document.getElementById('reviewUserName').textContent = request.userName;
  document.getElementById('reviewUserEmail').textContent = request.userEmail;
  document.getElementById('reviewReason').textContent = request.reason;
  document.getElementById('reviewIntendedUse').textContent = request.intendedUse;
  document.getElementById('reviewRequestedAt').textContent = new Date(request.requestedAt).toLocaleString();
  
  // Reset form fields
  document.getElementById('reviewNotes').value = '';
  document.getElementById('rejectionReason').value = '';
  document.getElementById('rejectionReasonContainer').classList.add('hidden');
  
  // Show modal
  modal.classList.add('active');
}

function closeReviewModal() {
  const modal = document.getElementById('reviewModal');
  modal.classList.remove('active');
}

async function submitReview(status) {
  const requestId = document.getElementById('reviewRequestId').value;
  const notes = document.getElementById('reviewNotes').value.trim();
  const rejectionReason = document.getElementById('rejectionReason').value.trim();
  
  try {
    const response = await fetch(`/api/admin/api-key-requests/${requestId}/review`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        status: status,
        notes: notes,
        rejectionReason: status === 'Rejected' ? rejectionReason : null
      })
    });
    
    if (!response.ok) {
      throw new Error("Failed to submit review");
    }
    
    // Close modal and reload requests
    closeReviewModal();
    loadApiKeyRequests(document.getElementById('requestStatusFilter').value);
    
    // Also reload API keys if approved
    if (status === 'Approved') {
      loadApiKeys();
    }
    
    alert(`API key request ${status.toLowerCase()} successfully.`);
  } catch (error) {
    console.error("Error submitting review:", error);
    alert("Failed to submit review. Please try again.");
  }
}

function setupMetricsModal() {
  const modal = document.getElementById('metricsModal');
  const closeButton = modal.querySelector('.close-button');
  
  closeButton.addEventListener('click', closeMetricsModal);
}

function openMetricsModal(key, userName) {
  const modal = document.getElementById('metricsModal');
  
  // Set basic info
  document.getElementById('metricsUserName').textContent = userName;
  document.getElementById('metricsApiKey').textContent = key;
  
  // Show loading, hide content
  document.getElementById('metricsLoading').classList.remove('hidden');
  document.getElementById('metricsContent').classList.add('hidden');
  
  // Show modal
  modal.classList.add('active');
  
  // Load metrics data
  loadApiKeyMetrics(key);
}

function closeMetricsModal() {
  const modal = document.getElementById('metricsModal');
  modal.classList.remove('active');
  
  // Clean up charts to prevent memory leaks
  if (window.timeSeriesChart) {
    window.timeSeriesChart.destroy();
  }
  if (window.statusCodesChart) {
    window.statusCodesChart.destroy();
  }
}

async function loadApiKeyMetrics(key) {
  try {
    const response = await fetch(`/api/admin/api-keys/${key}/metrics`);
    if (!response.ok) {
      throw new Error("Failed to load API key metrics");
    }
    
    const metrics = await response.json();
    renderApiKeyMetrics(metrics);
  } catch (error) {
    console.error("Error loading API key metrics:", error);
    alert("Failed to load metrics. Please try again.");
    closeMetricsModal();
  }
}

function renderApiKeyMetrics(metrics) {
  // Update basic metrics
  document.getElementById('metricsTotalRequests').textContent = metrics.totalRequests.toLocaleString();
  document.getElementById('metricsRequestsToday').textContent = metrics.requestsToday.toLocaleString();
  document.getElementById('metricsAvgResponseTime').textContent = `${metrics.averageResponseTime} ms`;
  
  // Render time series chart
  renderTimeSeriesChart(metrics.requestsTimeSeries);
  
  // Render status codes chart
  renderStatusCodesChart(metrics.statusCodes);
  
  // Hide loading, show content
  document.getElementById('metricsLoading').classList.add('hidden');
  document.getElementById('metricsContent').classList.remove('hidden');
}

function renderTimeSeriesChart(timeSeriesData) {
  const ctx = document.getElementById('timeSeriesChart').getContext('2d');
  
  // Destroy existing chart if it exists
  if (window.timeSeriesChart) {
    window.timeSeriesChart.destroy();
  }
  
  window.timeSeriesChart = new Chart(ctx, {
    type: 'line',
    data: {
      labels: timeSeriesData.map(point => point.timeLabel),
      datasets: [{
        label: 'Requests',
        data: timeSeriesData.map(point => point.value),
        backgroundColor: 'rgba(232, 68, 48, 0.2)',
        borderColor: 'rgba(232, 68, 48, 1)',
        borderWidth: 2,
        tension: 0.4,
        pointRadius: 3
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: false
        }
      },
      scales: {
        y: {
          beginAtZero: true,
          ticks: {
            precision: 0
          }
        }
      }
    }
  });
}

function renderStatusCodesChart(statusCodes) {
  const ctx = document.getElementById('statusCodesChart').getContext('2d');
  
  // Destroy existing chart if it exists
  if (window.statusCodesChart) {
    window.statusCodesChart.destroy();
  }
  
  // Prepare data
  const labels = Object.keys(statusCodes);
  const data = Object.values(statusCodes);
  
  // Prepare colors based on status code
  const backgroundColors = labels.map(code => {
    if (code >= 200 && code < 300) return 'rgba(40, 167, 69, 0.7)';  // Success: green
    if (code >= 400 && code < 500) return 'rgba(255, 193, 7, 0.7)';  // Client error: yellow
    if (code >= 500) return 'rgba(231, 76, 60, 0.7)';                // Server error: red
    return 'rgba(108, 117, 125, 0.7)';                               // Other: gray
  });
  
  window.statusCodesChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: labels,
      datasets: [{
        label: 'Count',
        data: data,
        backgroundColor: backgroundColors,
        borderColor: backgroundColors.map(color => color.replace('0.7', '1')),
        borderWidth: 1
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: false
        }
      },
      scales: {
        y: {
          beginAtZero: true,
          ticks: {
            precision: 0
          }
        }
      }
    }
  });
}

/**
 * User Management initialization and functionality
 */
function initializeUserManagement() {
  // Set up search for users
  document.getElementById('userSearch')?.addEventListener('input', (e) => {
    filterUsers(e.target.value);
  });

  // Initial data loading
  loadUsers();
}

async function loadUsers() {
  const loadingElement = document.getElementById('usersLoading');
  const errorElement = document.getElementById('usersError');
  const emptyElement = document.getElementById('usersEmpty');
  const listElement = document.getElementById('usersList');

  try {
    showElement(loadingElement);
    hideElement(errorElement);
    hideElement(emptyElement);
    hideElement(listElement);

    const response = await fetch('/api/admin/users');
    if (!response.ok) {
      throw new Error("Failed to load users");
    }

    const users = await response.json();
    
    // Update counter
    document.getElementById('userCount').textContent = users.length;

    if (users.length === 0) {
      showElement(emptyElement);
    } else {
      renderUsers(users);
      showElement(listElement);
    }
  } catch (error) {
    console.error("Error loading users:", error);
    showElement(errorElement);
  } finally {
    hideElement(loadingElement);
  }
}

function renderUsers(users) {
  const tableBody = document.getElementById('usersTableBody');
  tableBody.innerHTML = '';

  users.forEach(user => {
    const row = document.createElement('tr');
    
    const isLocked = user.lockoutEnd && new Date(user.lockoutEnd) > new Date();
    
    row.innerHTML = `
      <td>${escapeHtml(user.userName)}</td>
      <td>${escapeHtml(user.email)}</td>
      <td>
        <span class="status-badge ${user.emailConfirmed ? 'confirmed' : 'not-confirmed'}">
          ${user.emailConfirmed ? 'Confirmed' : 'Not Confirmed'}
        </span>
      </td>
      <td>
        <span class="status-badge ${isLocked ? 'locked' : 'active'}">
          ${isLocked ? 'Locked' : 'Active'}
        </span>
      </td>
      <td>
        <button class="button ${isLocked ? 'success' : 'danger'} btn-sm toggle-lock" data-user-id="${user.id}">
          ${isLocked ? 'Unlock' : 'Lock'}
        </button>
      </td>
    `;
    
    tableBody.appendChild(row);
    
    // Add event listener for lock/unlock button
    const lockButton = row.querySelector('.toggle-lock');
    lockButton.addEventListener('click', () => {
      toggleUserLock(user.id);
    });
  });
}

function filterUsers(searchTerm) {
  const rows = document.querySelectorAll('#usersTableBody tr');
  const lowerSearchTerm = searchTerm.toLowerCase();
  let visibleCount = 0;
  
  rows.forEach(row => {
    const userName = row.cells[0].textContent.toLowerCase();
    const email = row.cells[1].textContent.toLowerCase();
    
    const matches = userName.includes(lowerSearchTerm) || email.includes(lowerSearchTerm);
    
    row.style.display = matches ? '' : 'none';
    
    if (matches) {
      visibleCount++;
    }
  });
  
  // Update count of visible users
  document.getElementById('userCount').textContent = visibleCount;
}

async function toggleUserLock(userId) {
  try {
    const response = await fetch(`/api/admin/users/${userId}/toggle-lock`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });
    
    if (!response.ok) {
      throw new Error("Failed to toggle user lock");
    }
    
    const result = await response.json();
    
    // Reload users to reflect changes
    loadUsers();
  } catch (error) {
    console.error("Error toggling user lock:", error);
    alert("Failed to update user account status. Please try again.");
  }
}

/**
 * Utility functions
 */
function showElement(element) {
  if (element) {
    element.classList.remove('hidden');
  }
}

function hideElement(element) {
  if (element) {
    element.classList.add('hidden');
  }
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function copyToClipboard(text) {
  navigator.clipboard.writeText(text).catch(err => {
    console.error('Could not copy text: ', err);
  });
}
