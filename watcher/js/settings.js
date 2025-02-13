class SettingsManager {
    constructor() {
        this.currentTab = new URLSearchParams(window.location.search).get('tab') || 'profile';
        this.form = document.querySelector('.settings-form'); // Changed from getElementById to querySelector
        this.cache = {
            profile: null,
            routes: null,
            preferences: null,
            apiStatus: null,
        };
        this.isLoading = true;
        this.isApiRequestFormVisible = false;
        this.toast = window.toast;
        this.saveBar = document.querySelector('.settings-save-bar');
        
        // Wait for DOM to be fully loaded before initialization
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.init());
        } else {
            this.init();
        }
    }

    async init() {
        try {
            // Setup event listeners first
            this.setupEventListeners();
            this.setupDropdownListeners();
            this.setupPasswordForm();
            this.setupTabNavigation();

            // Load data and initialize UI
            await this.loadAllData();
            await this.loadSettings();

            // Store initial state after data is loaded
            this.initialState = this.getCurrentFormState();
            this.isLoading = false;
        } catch (error) {
            console.error('Initialization error:', error);
            this.toast?.show('Failed to initialize settings', 'error');
        }
    }

    async loadSettings() {
        if (this.isLoading || !this.cache) {
            return;
        }

        try {
            this.setLoadingState(true);
            
            // Ensure we have the cache data before proceeding
            if (!this.cache.profile) {
                await this.loadAllData();
            }

            // Load panel content based on current tab
            switch (this.currentTab) {
                case 'profile':
                    await this.loadProfileSettings();
                    break;
                case 'preferences':
                    await this.loadPreferenceSettings();
                    break;
                case 'security':
                    await this.loadSecuritySettings();
                    break;
                case 'notifications':
                    await this.loadNotificationSettings();
                    break;
                case 'api':
                    await this.loadApiSettings();
                    break;
            }

            this.updateNavigationState();
        } catch (error) {
            console.error('Error loading settings:', error);
            this.toast?.show('Failed to load settings. Please try again.', 'error');
        } finally {
            this.setLoadingState(false);
        }
    }

    updateNavigationState() {
        // Update URL
        const url = new URL(window.location);
        url.searchParams.set('tab', this.currentTab);
        window.history.replaceState({}, '', url);

        // Update navigation and panels
        document.querySelectorAll('.nav-link').forEach(link => {
            const isActive = link.getAttribute('href') === `#${this.currentTab}`;
            link.closest('.nav-item')?.classList.toggle('active', isActive);
        });

        document.querySelectorAll('.settings-panel').forEach(panel => {
            if (panel) {
                panel.style.display = panel.id === this.currentTab ? 'block' : 'none';
            }
        });
    }

    setLoadingState(isLoading) {
        this.isLoading = isLoading;
        document.querySelectorAll('.settings-panel').forEach(panel => {
            if (panel) {
                panel.classList.toggle('loading', isLoading);
            }
        });
    }

    async loadProfileSettings() {
        const data = await this.cache.profile || await this.loadAllData();
        if (data) {
            this.updateElementValue('userEmail', data.email);
            this.updateElementValue('accountCreated', new Date(data.createdAt).toLocaleDateString());
        }
    }

    async loadPreferenceSettings() {
        const data = await this.cache.preferences || await this.loadAllData();
        if (data) {
            // Update route selections
            const container = this.getElement('routesContainer');
            if (container && Array.isArray(this.cache.routes)) {
                this.populateRoutes(container, data.preferredRoutes);
            }

            // Update other preferences
            const showPreferredRoutesFirst = this.getElement('showPreferredRoutesFirst');
            if (showPreferredRoutesFirst) {
                showPreferredRoutesFirst.checked = data.showPreferredRoutesFirst || false;
            }
        }
    }

    async loadSecuritySettings() {
        // Reset password form
        const passwordForm = this.getElement('passwordForm');
        if (passwordForm) {
            passwordForm.reset();
        }
    }

    async loadNotificationSettings() {
        const data = await this.cache.preferences || await this.loadAllData();
        if (data) {
            const enableEmailNotifications = this.getElement('enableEmailNotifications');
            if (enableEmailNotifications) {
                enableEmailNotifications.checked = data.enableEmailNotifications || false;
            }
        }
    }

    async loadApiSettings() {
        const data = await this.cache.apiStatus || await this.loadAllData();
        if (data) {
            const container = this.getElement('apiKeyContainer');
            if (container) {
                this.populateApiSection(container, data);
            }
        }
    }

    async loadAllData() {
        try {
            // Load all data in parallel
            const [
                profileResponse,
                preferencesResponse,
                routesResponse,
                apiResponse,
            ] = await Promise.all([
                fetch("/api/accounts/profile"),
                fetch("/api/accounts/preferences"),
                fetch("/api/accounts/routes"),
                fetch("/api/accounts/api-keys"),
            ]);

            // Handle profile data
            if (profileResponse.ok) {
                this.cache.profile = await profileResponse.json();
            }

            // Handle preferences
            if (preferencesResponse.ok) {
                this.cache.preferences = await preferencesResponse.json();
            } else {
                this.cache.preferences = { preferredRoutes: [] };
            }

            // Handle routes
            if (routesResponse.ok) {
                const data = await routesResponse.json();
                this.cache.routes = data.routes;
            } else {
                const routesElement = this.getElement("availableRoutes");
                this.cache.routes = routesElement?.value
                    ? JSON.parse(routesElement.value)
                    : [];
            }

            // Handle API status
            if (apiResponse.ok) {
                const apiData = await apiResponse.json();
                this.cache.apiStatus = {
                    hasApiKey: apiData.hasApiKey,
                    key: apiData.key,
                    pendingRequest: apiData.pendingRequest, // Add this flag
                };
            }

            // Populate all sections
            await this.populateAllSections();

            // Finally, set initial state and mark as loaded
            this.isLoading = false;
            this.initialState = this.getCurrentFormState();

            // Show initial section
            this.switchSection(this.currentSection);
        } catch (error) {
            console.error("Error loading data:", error);
            this.toast.show(
                "Error loading settings. Please refresh the page.",
                "error",
            );
        }
    }

    async populateAllSections() {
        // Populate profile section
        if (this.cache.profile) {
            this.updateElementValue("userEmail", this.cache.profile.email);
            this.updateElementValue("accountCreated", new Date(this.cache.profile.createdAt).toLocaleDateString());
            this.updateElementValue("lastLogin", new Date(this.cache.profile.lastLogin).toLocaleString());
        }

        // Populate routes section
        const container = this.getElement("routesContainer");
        if (container && Array.isArray(this.cache.routes)) {
            container.innerHTML = this.cache.routes
                .map(
                    (route) => `
          <label class="option">
            <div class="checkbox-wrapper">
              <input type="checkbox" 
                     name="PreferredRoutes" 
                     value="${route}"
                     ${this.cache.preferences.preferredRoutes.includes(route) ? "checked" : ""} />
              <div class="checkbox-custom"></div>
            </div>
            <span>Route ${route}</span>
          </label>
        `,
                )
                .join("");

            container
                .querySelectorAll('input[type="checkbox"]')
                .forEach((checkbox) => {
                    checkbox.addEventListener("change", () => this.updateSelectedCount());
                });
            this.updateSelectedCount();
        }

        // Populate API section
        const apiContainer = this.getElement("apiKeyContainer");
        if (apiContainer && this.cache.apiStatus) {
            if (this.cache.apiStatus.hasApiKey) {
                apiContainer.innerHTML = `
          <div class="api-key-display">
              <label>Your API Key</label>
              <div class="api-key-field">
                  <input type="password" value="${this.cache.apiStatus.key}" readonly />
                  <button type="button" onclick="window.settings.toggleApiKeyVisibility(this)">
                      <i class="fas fa-eye"></i>
                  </button>
                  <button type="button" onclick="window.settings.regenerateApiKey()">
                      <i class="fas fa-sync-alt"></i>
                  </button>
              </div>
          </div>`;
            } else if (this.cache.apiStatus.pendingRequest) {
                apiContainer.innerHTML = `
          <div class="api-request-pending">
              <p>Your API access request is being reviewed.</p>
              <p>We'll notify you via email once a decision has been made.</p>
          </div>`;
            } else if (this.isApiRequestFormVisible) {
                apiContainer.innerHTML = `
          <div class="api-request-form">
              <div class="form-group">
                  <label>Reason for API Access</label>
                  <textarea name="reason" class="form-control" rows="3"
                      placeholder="Explain why you need API access"></textarea>
                  <span class="text-danger"></span>
              </div>

              <div class="form-group">
                  <label>Intended Use</label>
                  <textarea name="intendedUse" class="form-control" rows="3"
                      placeholder="Describe how you will use the API"></textarea>
                  <span class="text-danger"></span>
              </div>

              <button type="button" class="btn-secondary" onclick="window.settings.hideApiRequestForm()">
                  Cancel
              </button>
              <button type="button" class="btn-primary" onclick="window.settings.requestApiAccess()">
                  <i class="fas fa-paper-plane"></i> Submit Request
              </button>
          </div>`;
            } else {
                apiContainer.innerHTML = `
          <div class="api-request-intro">
              <p>You currently don't have API access. Request access to integrate our services into your applications.</p>
              <button type="button" class="btn-primary" onclick="window.settings.showApiRequestForm()">
                  <i class="fas fa-key"></i> Request API Access
              </button>
          </div>`;
            }
        }

        // Populate account settings
        if (this.cache.preferences) {
            const enableEmailNotifications = this.getElement("enableEmailNotifications");
            if (enableEmailNotifications) {
                enableEmailNotifications.checked = this.cache.preferences.enableEmailNotifications;
            }
            const showPreferredRoutesFirst = this.getElement("showPreferredRoutesFirst");
            if (showPreferredRoutesFirst) {
                showPreferredRoutesFirst.checked = this.cache.preferences.showPreferredRoutesFirst;
            }
        }
    }

    async savePreferences() {
        try {
            // Validate if we're in routes section
            if (this.currentSection === "routes") {
                const selectedRoutes = document.querySelectorAll(
                    '[name="PreferredRoutes"]:checked',
                );
                if (selectedRoutes.length === 0) {
                    this.toast.show("Please select at least one route", "error");
                    return;
                }
            }

            const preferences = {
                preferredRoutes: Array.from(
                    document.querySelectorAll('[name="PreferredRoutes"]:checked'),
                ).map((cb) => cb.value),
                showPreferredRoutesFirst:
                    this.getElement("showPreferredRoutesFirst")?.checked || false,
                enableEmailNotifications:
                    this.getElement("enableEmailNotifications")?.checked || false,
            };

            const response = await fetch("/api/accounts/preferences", {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(preferences),
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || "Failed to save preferences");
            }

            // Update cache and state
            this.cache.preferences = preferences;
            this.initialState = this.getCurrentFormState();
            this.saveBar?.classList.remove("visible");
            this.toast.show("Preferences saved successfully", "success");
        } catch (error) {
            this.toast.show(error.message || "Error saving preferences", "error");
        }
    }

    async regenerateApiKey() {
        if (!confirm("Are you sure you want to regenerate your API key?")) return;

        try {
            const response = await fetch("/api/accounts/api-keys", {
                method: "PUT",
            });

            if (!response.ok) throw new Error("Failed to regenerate API key");

            const result = await response.json();
            this.toast.show(result.message, "success");
            this.loadAllData();
        } catch (error) {
            this.toast.show("Error regenerating API key", "error");
        }
    }

    async deleteAccount(password) {
        try {
            const response = await fetch("/api/accounts", {
                method: "DELETE",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({ password }),
            });

            if (!response.ok) throw new Error("Failed to delete account");

            await fetch("/api/accounts/logout", { method: "POST" });
            window.location.href = "/";
        } catch (error) {
            this.toast.show("Error deleting account", "error");
        }
    }

    setupEventListeners() {
         // Tab navigation
         document.querySelectorAll('.nav-link').forEach(link => {
            link.addEventListener('click', (e) => {
              e.preventDefault();
              this.switchTab(e.currentTarget.getAttribute('href').substring(1));
            });
          });
      
          // Form submissions
          document.querySelectorAll('.settings-form').forEach(form => {
            form.addEventListener('submit', (e) => {
              e.preventDefault();
              this.saveSettings(e.target.id);
            });
          });

        // Main settings form submit
        const saveButton = document.querySelector(
            '.settings-save-bar button[type="submit"]',
        );
        if (saveButton) {
            saveButton.addEventListener("click", async (e) => {
                e.preventDefault();
                await this.savePreferences();
            });
        }

        if (this.form) {
            this.form.addEventListener("submit", async (e) => {
                e.preventDefault();
                await this.savePreferences();
            });
        }

        // Form change listeners
        const inputs = [
            ...document.querySelectorAll('[name="PreferredRoutes"]'),
            this.getElement("showPreferredRoutesFirst"),
            this.getElement("enableEmailNotifications"),
        ];

        inputs.forEach((input) => {
            if (input) {
                input.addEventListener("change", () => {
                    this.checkFormChanges();
                });
            }
        });

        // Add change listeners for preference toggles
        const toggles = [
            this.getElement('showPreferredRoutesFirst'),
            this.getElement('enableEmailNotifications')
        ];

        toggles.forEach(toggle => {
            if (toggle) {
                toggle.addEventListener('change', () => {
                    this.checkFormChanges();
                });
            }
        });

        // Add change listeners for route checkboxes
        document.querySelectorAll('[name="PreferredRoutes"]').forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                this.checkFormChanges();
            });
        });
    }

    setupPasswordForm() {
        const form = this.getElement('passwordForm');
        if (form) {
            form.addEventListener('submit', async (e) => {
                e.preventDefault();
                await this.changePassword();
            });
        }
    }

    async changePassword() {
        const currentPassword = this.getElement('currentPassword')?.value;
        const newPassword = this.getElement('newPassword')?.value;
        const confirmPassword = this.getElement('confirmPassword')?.value;

        if (!currentPassword || !newPassword || !confirmPassword) {
            this.toast.show('Please fill in all password fields', 'error');
            return;
        }

        if (newPassword !== confirmPassword) {
            this.toast.show('New passwords do not match', 'error');
            return;
        }

        try {
            const response = await fetch('/api/accounts/password', {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    currentPassword,
                    newPassword,
                    confirmPassword
                }),
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to change password');
            }

            this.toast.show('Password changed successfully', 'success');
            this.getElement('passwordForm')?.reset();
        } catch (error) {
            this.toast.show(error.message || 'Error changing password', 'error');
        }
    }

    getCurrentFormState() {
        return {
            preferredRoutes: Array.from(
                document.querySelectorAll('[name="PreferredRoutes"]:checked'),
            )
                .map((cb) => cb.value)
                .sort()
                .join(","),
            showPreferredRoutesFirst:
                this.getElement("showPreferredRoutesFirst")?.checked || false,
            enableEmailNotifications:
                this.getElement("enableEmailNotifications")?.checked || false,
        };
    }

    switchSection(sectionId) {
        // Update navigation
        document.querySelectorAll(".settings-nav a").forEach((link) => {
            link.classList.toggle("active", link.dataset.section === sectionId);
        });

        // Hide all sections and show selected
        document.querySelectorAll(".settings-section").forEach((section) => {
            section.style.display =
                section.id === `${sectionId}-section` ? "block" : "none";
        });

        this.currentSection = sectionId;
    }

    async exportData() {
        try {
            const response = await fetch("/api/accounts/export");
            if (!response.ok) throw new Error("Export failed");

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = `account-data-${new Date().toISOString().split("T")[0]}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);

            this.toast.show("Data exported successfully", "success");
        } catch (error) {
            this.toast.show("Error exporting data", "error");
        }
    }

    async confirmDeleteAccount() {
        const password = await this.showConfirmDialog(
            'Delete Account',
            'This action cannot be undone. Please enter your password to confirm.',
            'password'
        );

        if (password) {
            try {
                const response = await fetch('/api/accounts', {
                    method: 'DELETE',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({ password }),
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.message || 'Failed to delete account');
                }

                // Log out and redirect to home page
                await fetch('/api/accounts/logout', { method: 'POST' });
                window.location.href = '/';
            } catch (error) {
                this.toast.show(error.message || 'Error deleting account', 'error');
            }
        }
    }

    async showConfirmDialog(title, message, inputType = "text") {
        // Create modal dialog
        const modal = document.createElement('div');
        modal.className = 'custom-modal';
        modal.innerHTML = `
            <div class="custom-modal-content">
                <h2>${title}</h2>
                <p>${message}</p>
                <input type="${inputType}" class="modal-input" placeholder="Enter your password" />
                <div class="modal-actions">
                    <button type="button" class="btn-secondary" data-action="cancel">Cancel</button>
                    <button type="button" class="btn-danger" data-action="confirm">Confirm</button>
                </div>
            </div>
        `;

        // Add to document
        document.body.appendChild(modal);

        // Handle confirmation
        return new Promise((resolve) => {
            modal.querySelector('[data-action="confirm"]').addEventListener('click', () => {
                const value = modal.querySelector('input').value;
                document.body.removeChild(modal);
                resolve(value);
            });

            modal.querySelector('[data-action="cancel"]').addEventListener('click', () => {
                document.body.removeChild(modal);
                resolve(null);
            });
        });
    }

    filterRoutes(input) {
        const filter = input.value.toLowerCase();
        const options = input
            .closest(".dropdown-options")
            .querySelectorAll(".option");

        options.forEach((option) => {
            const text = option.textContent.toLowerCase();
            option.style.display = text.includes(filter) ? "" : "none";
        });
    }

    updateSelectedCount() {
        const selected = document.querySelectorAll(
            '[name="PreferredRoutes"]:checked',
        ).length;
        const countElement = this.getElement("selected-count");
        if (countElement) {
            countElement.textContent = `${selected} routes selected`;
        }
        this.checkFormChanges();
    }

    setupDropdownListeners() {
        // Close dropdown when clicking outside
        document.addEventListener("click", (e) => {
            if (!e.target.closest(".custom-dropdown")) {
                document.querySelectorAll(".custom-dropdown").forEach((dropdown) => {
                    dropdown.classList.remove("open");
                });
            }
        });

        // Toggle dropdown on click
        document.querySelectorAll(".selected-options").forEach((elem) => {
            elem.addEventListener("click", (e) => {
                e.preventDefault();
                e.stopPropagation();
                const dropdown = elem.closest(".custom-dropdown");
                dropdown.classList.toggle("open");
            });
        });

        // Setup search functionality
        document.querySelectorAll(".search-box input").forEach((input) => {
            input.addEventListener("input", (e) => this.filterRoutes(e.target));
        });
    }

    toggleDropdown(elem) {
        const parent = elem.closest(".custom-dropdown");
        parent.classList.toggle("open");
    }

    checkFormChanges() {
        if (!this.saveBar || this.isLoading || !this.initialState) return;

        const currentState = this.getCurrentFormState();
        const hasChanges = JSON.stringify(currentState) !== JSON.stringify(this.initialState);

        this.saveBar.classList.toggle('visible', hasChanges);
    }

    async requestApiAccess() {
        if (this.cache.apiStatus?.pendingRequest) {
            this.toast.show("You already have a pending API key request", "error");
            return;
        }

        // Get the form from the API panel instead of this.form
        const form = document.querySelector('#api .api-request-form');
        if (!form) {
            return;
        }

        const reason = form.querySelector('textarea[name="reason"]')?.value;
        const intendedUse = form.querySelector('textarea[name="intendedUse"]')?.value;

        if (!reason || !intendedUse) {
            this.toast.show("Please fill in all fields", "error");
            return;
        }

        try {
            const response = await fetch("/api/accounts/api-keys", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({ reason, intendedUse }),
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || "Failed to submit request");
            }

            const result = await response.json();
            this.toast.show(result.message, "success");
            await this.loadAllData(); // Refresh the view
        } catch (error) {
            this.toast.show(error.message || "Error submitting request", "error");
        }
    }

    showApiRequestForm() {
        if (this.cache.apiStatus?.pendingRequest) {
            this.toast.show("You already have a pending API key request", "error");
            return;
        }
        this.isApiRequestFormVisible = true;
        this.populateAllSections();
    }

    hideApiRequestForm() {
        this.isApiRequestFormVisible = false;
        this.populateAllSections();
    }

    // Helper method to safely get DOM elements
    getElement(id) {
        const element = document.getElementById(id);
        if (!element) {
            return null;
        }
        return element;
    }

    // Use this helper method when accessing DOM elements
    updateElementValue(id, value) {
        const element = this.getElement(id);
        if (element) {
            element.value = value;
        }
    }

    setupTabNavigation() {
        document.querySelectorAll('.nav-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const tabId = e.currentTarget.getAttribute('href').substring(1);
                this.switchTab(tabId);
            });
        });
    }

    switchTab(tabId) {
        // Update navigation
        document.querySelectorAll('.nav-item').forEach(item => {
            item.classList.toggle('active', 
                item.querySelector('.nav-link').getAttribute('href') === `#${tabId}`);
        });

        // Update panels
        document.querySelectorAll('.settings-panel').forEach(panel => {
            panel.style.display = panel.id === tabId ? 'block' : 'none';
        });

        // Update URL
        const url = new URL(window.location);
        url.searchParams.set('tab', tabId);
        window.history.pushState({}, '', url);

        // Update current tab
        this.currentTab = tabId;

        // Load content for the tab if needed
        this.loadTabContent(tabId);
    }

    async loadTabContent(tabId) {
        switch (tabId) {
            case 'profile':
                await this.loadProfileSettings();
                break;
            case 'preferences':
                await this.loadPreferenceSettings();
                break;
            case 'security':
                await this.loadSecuritySettings();
                break;
            case 'notifications':
                await this.loadNotificationSettings();
                break;
            case 'api':
                await this.loadApiSettings();
                break;
        }
    }

    populateRoutes(container, selectedRoutes) {
        if (!container || !Array.isArray(this.cache.routes)) return;

        // Update selected count text in the dropdown trigger
        const selectedCount = selectedRoutes.length;
        const dropdownTrigger = container.closest('.custom-dropdown')?.querySelector('.selected-options span');
        if (dropdownTrigger) {
            dropdownTrigger.textContent = selectedCount > 0 
                ? `${selectedCount} routes selected`
                : 'Select Routes';
        }

        // Create route options
        const html = this.cache.routes.map(route => `
            <label class="option">
                <div class="checkbox-wrapper">
                    <input type="checkbox" 
                           name="PreferredRoutes" 
                           value="${route}"
                           ${selectedRoutes.includes(route) ? "checked" : ""} />
                    <div class="checkbox-custom"></div>
                </div>
                <span>Route ${route}</span>
            </label>
        `).join('');

        container.innerHTML = html;

        // Add change listeners to checkboxes
        container.querySelectorAll('input[type="checkbox"]').forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                this.updateSelectedCount();
                // Update dropdown trigger text
                const selectedCount = container.querySelectorAll('input[type="checkbox"]:checked').length;
                if (dropdownTrigger) {
                    dropdownTrigger.textContent = selectedCount > 0 
                        ? `${selectedCount} routes selected`
                        : 'Select Routes';
                }
                this.checkFormChanges(); // Add this line
            });
        });

        // Initial count update
        this.updateSelectedCount();
        this.checkFormChanges(); // Add this line
    }

    updateSelectedCount() {
        const selected = document.querySelectorAll('[name="PreferredRoutes"]:checked').length;
        const countElement = document.querySelector('#selected-count');
        if (countElement) {
            countElement.textContent = `${selected} routes selected`;
        }
        this.checkFormChanges();
    }

    populateApiSection(container, data) {
        if (!container) return;

        if (data.hasApiKey) {
            container.innerHTML = `
                <div class="api-key-display">
                    <label>Your API Key</label>
                    <div class="api-key-field">
                        <input type="password" value="${data.key}" readonly />
                        <button type="button" onclick="window.settings.toggleApiKeyVisibility(this)">
                            <i class="fas fa-eye"></i>
                        </button>
                        <button type="button" onclick="window.settings.regenerateApiKey()">
                            <i class="fas fa-sync-alt"></i>
                        </button>
                    </div>
                </div>`;
        } else if (data.pendingRequest) {
            container.innerHTML = `
                <div class="api-request-pending">
                    <p>API Access Request Pending</p>
                    <p>Your request is currently being reviewed. We'll notify you via email once a decision has been made.</p>
                </div>`;
        } else if (this.isApiRequestFormVisible) {
            container.innerHTML = `
                <div class="api-request-form">
                    <div class="form-group">
                        <label>Reason for API Access</label>
                        <textarea name="reason" 
                                placeholder="Please explain why you need API access and how you plan to use it"></textarea>
                        <span class="text-danger"></span>
                    </div>

                    <div class="form-group">
                        <label>Intended Use</label>
                        <textarea name="intendedUse" 
                                placeholder="Describe the specific features or functionality you plan to implement"></textarea>
                        <span class="text-danger"></span>
                    </div>

                    <div class="form-actions">
                        <button type="button" class="btn-secondary" onclick="window.settings.hideApiRequestForm()">
                            Cancel
                        </button>
                        <button type="button" class="btn-primary" onclick="window.settings.requestApiAccess()">
                            <i class="fas fa-paper-plane"></i> Submit Request
                        </button>
                    </div>
                </div>`;
        } else {
            container.innerHTML = `
                <div class="api-request-intro">
                    <p>Request API access to integrate our bus information services into your applications.</p>
                    <button type="button" class="btn-primary" onclick="window.settings.showApiRequestForm()">
                        <i class="fas fa-key"></i> Request API Access
                    </button>
                </div>`;
        }
    }

    toggleApiKeyVisibility(button) {
        const input = button.parentElement.querySelector('input');
        const icon = button.querySelector('i');
        
        if (input.type === 'password') {
            input.type = 'text';
            icon.classList.remove('fa-eye');
            icon.classList.add('fa-eye-slash');
        } else {
            input.type = 'password';
            icon.classList.remove('fa-eye-slash');
            icon.classList.add('fa-eye');
        }
    }
}

// Initialize settings when DOM is loaded
document.addEventListener("DOMContentLoaded", () => {
    window.settings = new SettingsManager();
});
