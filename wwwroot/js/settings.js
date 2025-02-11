(function () {
	'use strict';

	class SettingsManager {
		constructor() {
			this.currentSection = "profile";
			this.form = document.getElementById("settingsForm");
			this.initialState = null;
			this.setupEventListeners();
			this.cache = {
				profile: null,
				routes: null,
				preferences: null,
				apiStatus: null,
			};

			document
				.querySelector(".search-box input")
				?.addEventListener("input", (e) => {
					this.filterRoutes(e.target);
				});

			this.setupDropdownListeners();
			this.saveBar = document.querySelector(".settings-save-bar");
			this.isLoading = true;
			this.loadAllData();
			this.passwordForm = document.getElementById("passwordForm");
			this.setupPasswordForm();
			this.isApiRequestFormVisible = false;

			this.toast = window.toast;
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
					const routesElement = document.getElementById("availableRoutes");
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
				document.getElementById("userEmail").value = this.cache.profile.email;
				document.getElementById("accountCreated").value = new Date(
					this.cache.profile.createdAt,
				).toLocaleDateString();
				document.getElementById("lastLogin").value = new Date(
					this.cache.profile.lastLogin,
				).toLocaleString();
			}

			// Populate routes section
			const container = document.getElementById("routesContainer");
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
			const apiContainer = document.getElementById("apiKeyContainer");
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
				document.getElementById("enableEmailNotifications").checked =
					this.cache.preferences.enableEmailNotifications;
				document.getElementById("showPreferredRoutesFirst").checked =
					this.cache.preferences.showPreferredRoutesFirst;
			}
		}

		async savePreferences() {
			console.log("Saving preferences...");
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
						document.getElementById("showPreferredRoutesFirst")?.checked || false,
					enableEmailNotifications:
						document.getElementById("enableEmailNotifications")?.checked || false,
				};

				console.log("Preferences to save:", preferences);

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
				console.error("Error saving preferences:", error);
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
			// Navigation events
			document.querySelectorAll(".settings-nav a").forEach((link) => {
				link.addEventListener("click", (e) => {
					e.preventDefault();
					this.switchSection(link.dataset.section);
				});
			});

			// Main settings form submit
			const saveButton = document.querySelector(
				'.settings-save-bar button[type="submit"]',
			);
			if (saveButton) {
				console.log("Save button found, attaching click handler");
				saveButton.addEventListener("click", async (e) => {
					console.log("Save button clicked");
					e.preventDefault();
					await this.savePreferences();
				});
			}

			if (this.form) {
				console.log("Form found, attaching submit handler");
				this.form.addEventListener("submit", async (e) => {
					console.log("Form submitted");
					e.preventDefault();
					await this.savePreferences();
				});
			}

			// Form change listeners
			const inputs = [
				...document.querySelectorAll('[name="PreferredRoutes"]'),
				document.getElementById("showPreferredRoutesFirst"),
				document.getElementById("enableEmailNotifications"),
			];

			inputs.forEach((input) => {
				if (input) {
					input.addEventListener("change", () => {
						this.checkFormChanges();
					});
				}
			});
		}

		setupPasswordForm() {
			if (this.passwordForm) {
				this.passwordForm.addEventListener("submit", async (e) => {
					e.preventDefault();
					await this.changePassword();
				});
			}
		}

		async changePassword() {
			const currentPassword = document.getElementById("currentPassword").value;
			const newPassword = document.getElementById("newPassword").value;
			const confirmPassword = document.getElementById("confirmPassword").value;

			if (!currentPassword || !newPassword || !confirmPassword) {
				this.toast.show("Please fill in all password fields", "error");
				return;
			}

			if (newPassword !== confirmPassword) {
				this.toast.show("New passwords do not match", "error");
				return;
			}

			try {
				const response = await fetch("/api/accounts/password", {
					method: "PUT",
					headers: {
						"Content-Type": "application/json",
					},
					body: JSON.stringify({
						currentPassword,
						newPassword,
					}),
				});

				if (!response.ok) {
					const error = await response.json();
					throw new Error(error.message || "Failed to change password");
				}

				this.toast.show("Password changed successfully", "success");
				this.passwordForm.reset();
			} catch (error) {
				this.toast.show(error.message || "Error changing password", "error");
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
					document.getElementById("showPreferredRoutesFirst")?.checked || false,
				enableEmailNotifications:
					document.getElementById("enableEmailNotifications")?.checked || false,
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
			const confirmed = await this.showConfirmDialog(
				"Delete Account",
				"This action cannot be undone. Please enter your password to confirm.",
				"password",
			);

			if (confirmed) {
				await this.deleteAccount(confirmed);
			}
		}

		showConfirmDialog(title, message, inputType = "text") {
			return new Promise((resolve) => {
				const dialog = document.createElement("div");
				dialog.className = "confirm-dialog";
				dialog.innerHTML = `
                <div class="dialog-content">
                    <h3>${title}</h3>
                    <p>${message}</p>
                    <input type="${inputType}" placeholder="Enter your password" />
                    <div class="dialog-actions">
                        <button type="button" class="btn-secondary" data-action="cancel">Cancel</button>
                        <button type="button" class="btn-danger" data-action="confirm">Confirm</button>
                    </div>
                </div>`;

				document.body.appendChild(dialog);

				dialog.querySelector('[data-action="confirm"]').onclick = () => {
					const value = dialog.querySelector("input").value;
					dialog.remove();
					resolve(value);
				};

				dialog.querySelector('[data-action="cancel"]').onclick = () => {
					dialog.remove();
					resolve(null);
				};
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
			const countElement = document.getElementById("selected-count");
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
			const hasChanges =
				currentState.preferredRoutes !== this.initialState.preferredRoutes ||
				currentState.showPreferredRoutesFirst !==
					this.initialState.showPreferredRoutesFirst ||
				currentState.enableEmailNotifications !==
					this.initialState.enableEmailNotifications;

			this.saveBar.classList.toggle("visible", hasChanges);
		}

		async requestApiAccess() {
			// Check for pending request first
			if (this.cache.apiStatus?.pendingRequest) {
				this.toast.show("You already have a pending API key request", "error");
				return;
			}

			const form = this.form.querySelector(".api-request-form");
			const reason = form.querySelector('textarea[name="reason"]')?.value;
			const intendedUse = form.querySelector(
				'textarea[name="intendedUse"]',
			)?.value;

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
	}

	// Initialize settings when DOM is loaded
	document.addEventListener("DOMContentLoaded", () => {
		window.settings = new SettingsManager();
	});

})();
//# sourceMappingURL=settings.js.map
