// TODO: Rewrite to accomodate our new API and project structure

import dayjs from "dayjs";
import Logger from "./modules/logging.js";

/**
 * @fileoverview JavaScript module for displaying bus information
 * @description This module fetches bus information from an API and displays it in the UI.
 * @version 0.1.0
 * @author Jacob Walton
 * @requires dayjs
 * @requires Logger
 * @module BusInfoModule
 */

const BusInfoModule = (() => {
	/**
	 * @constant {Object} CONFIG
	 * @description Configuration object for the BusInfoModule
	 */
	const CONFIG = Object.freeze({
		API_ENDPOINT: "/api/v2/businfo",
		REFRESH_INTERVAL: 30000,
		ERROR_DISPLAY_DURATION: 5000,
		SEARCH_DEBOUNCE_DELAY: 300,
		SELECTORS: Object.freeze({
			busInfoList: "#busInfoList",
			lastUpdated: "#lastUpdated",
			error: "#error",
			loadingIndicator: "#loadingIndicator",
			searchInput: "#searchInput",
		}),
		TIME_FORMAT: {
			today: "HH:mm",
			other: "DD/MM HH:mm",
		},
		PREFERENCES_ENDPOINT: "/api/accounts/preferences",
		SECTION_STORAGE_KEY: "businfo_collapsed_sections",
	});

	/**
	 * @typedef {Object} BusInfoState
	 * @property {boolean} isLoading - Flag indicating if data is being fetched
	 * @property {Date|null} lastFetchTime - Timestamp of the last successful fetch
	 * @property {number|null} errorTimeout - Timeout ID for error message display
	 * @property {Object|null} busData - Fetched bus information data
	 */

	/** @type {BusInfoState} */
	const state = {
		isLoading: false,
		lastFetchTime: null,
		errorTimeout: null,
		busData: null,
		preferences: {
			preferredRoutes: [],
			showPreferredRoutesFirst: true,
		},
		refreshInterval: null,
		updateTimeInterval: null,
		initialized: false,
		collapsedSections: new Set(),
		mapDisplayed: false,
		predictionsDisplayed: false,
	};

	/** @type {Object.<string, HTMLElement>} */
	const elements = {};

	/**
	 * Initializes DOM elements used by the module
	 * @throws {Error} If any required element is not found in the DOM
	 */
	function initializeElements() {
		Logger.info("Initializing BusInfoModule elements");
		for (const key in CONFIG.SELECTORS) {
			if (Object.hasOwn(CONFIG.SELECTORS, key)) {
				const element = document.querySelector(CONFIG.SELECTORS[key]);
				if (!element) {
					Logger.error(`Element not found: ${CONFIG.SELECTORS[key]}`);
					throw new Error(`Element not found: ${CONFIG.SELECTORS[key]}`);
				}
				elements[key] = element;
			}
		}
	}

	/**
	 * Toggles the visibility of the loading indicator
	 * @param {boolean} show - Whether to show or hide the loading indicator
	 */
	function showLoading(show) {
		state.isLoading = show;
		elements.loadingIndicator.style.display = show ? "flex" : "none";
		Logger.debug(`Loading indicator ${show ? "shown" : "hidden"}`);
	}

	function displayBusMap() {
		if (state.mapDisplayed) return;

		const mapSection = document.createElement("div");
		mapSection.className = "bus-section map-section";

		const mapHeader = document.createElement("div");
		mapHeader.className = "bus-section-header";

		const mapContent = document.createElement("div");
		mapContent.className = "bus-section-content single-column collapsed";

		mapHeader.innerHTML = `
			<div class="section-header-content">
				<h3>Bus Map</h3>
			</div>
			<button class="section-toggle" aria-label="Toggle section">
				<i class="fas fa-chevron-down"></i>
			</button>
		`;

		mapContent.innerHTML = `
			<div id="busMap"></div>
		`;

		const map = mapContent.querySelector("#busMap");

		fetch("/api/v2/businfo/map")
			.then((response) => response.blob())
			.then((blob) => {
				const url = URL.createObjectURL(blob);
				map.innerHTML = `<img src="${url}" alt="Bus map" class="bus-map"/>`;
			})
			.catch((error) => {
				map.innerHTML = "Error loading map";
				console.error("Error loading map:", error);
			});

		mapSection.appendChild(mapHeader);
		mapSection.appendChild(mapContent);

		// Append it below the bus info list
		elements.busInfoList.insertAdjacentElement("afterend", mapSection);

		// Add map section toggle handling
		mapHeader.addEventListener("click", () => {
			mapContent.classList.toggle("collapsed");
			mapHeader.querySelector(".section-toggle").style.transform =
				mapContent.classList.contains("collapsed")
					? "rotate(-180deg)"
					: "rotate(0)";
		});

		// Add keyboard support for accessibility
		document.addEventListener("keydown", (e) => {
			if (e.key === "Enter" || e.key === " ") {
				const header = e.target.closest(".bus-section-header");
				if (header) {
					e.preventDefault();
					const section = header.closest(".bus-section");
					if (section) {
						toggleSection(section.id);
					}
				}
			}
		});

		state.mapDisplayed = true; // Set the flag after successful map creation
	}

	/**
	 * Fetches bus information from the API
	 * @returns {Promise<void>}
	 */
	async function fetchBusInfo() {
		Logger.info("Fetching bus information");
		Logger.startPerformanceMark("fetchBusInfo");
		showLoading(true);
		try {
			// Fetch bus information from the API
			const response = await fetch(CONFIG.API_ENDPOINT);
			if (!response.ok)
				// Fallback to local API if the main API fails
				throw new Error("API request failed");
			const data = await response.json();

			if (!data || typeof data !== "object" || !data.busData) {
				throw new Error("Invalid API response format");
			}

			state.busData = data.busData;
			displayBusInfo(state.busData);
			displayBusMap();
			state.lastFetchTime = new Date();
			updateLastFetchedTime();

			// Fetch and update predictions from new BusInfoController endpoint
			fetchAndDisplayPredictions();

			Logger.info("Bus information fetched successfully", {
				busCount: Object.keys(state.busData).length,
			});
		} catch (error) {
			Logger.error("Error fetching bus information", { error: error.message });
			displayError("Unable to fetch bus information. Please try again.");
		} finally {
			showLoading(false);
			Logger.endPerformanceMark("fetchBusInfo");
		}
	}

	/**
	 * Fetches user preferences from the API
	 * @returns {Promise<void>}
	 */
	async function fetchPreferences() {
		try {
			const response = await fetch(CONFIG.PREFERENCES_ENDPOINT);
			if (!response.ok) return;
			const data = await response.json();
			state.preferences = {
				preferredRoutes: data.preferredRoutes || [],
				showPreferredRoutesFirst: data.showPreferredRoutesFirst,
			};
			Logger.debug("Preferences loaded", state.preferences);
		} catch (error) {
			Logger.error("Error fetching preferences", { error: error.message });
		}
	}

	/**
	 * Displays fetched bus information
	 * @param {Object} busData - Bus information data
	 */
	function displayBusInfo(busData) {
		Logger.debug("Displaying bus information");
		elements.busInfoList.innerHTML = "";

		if (busData && Object.keys(busData).length > 0) {
			const fragment = document.createDocumentFragment();
			const entries = Object.entries(busData);

			// Create container for all bus sections
			const busInfoSections = document.createElement("div");
			busInfoSections.className = "bus-info-sections";

			// Create preferred section if there are preferred routes
			const preferredBuses = entries.filter(([number]) =>
				state.preferences.preferredRoutes.includes(number),
			);

			if (
				preferredBuses.length > 0 &&
				state.preferences.showPreferredRoutesFirst
			) {
				const sectionId = "preferred-routes-section";
				const preferredSection = document.createElement("div");
				preferredSection.id = sectionId;
				preferredSection.className = "bus-section preferred-section";

				preferredSection.innerHTML = `
					<div class="bus-section-header" role="button" tabindex="0">
						<div class="section-header-content">
							<h3>Preferred Routes</h3>
							<span class="bus-count">${preferredBuses.length} buses</span>
						</div>
						<button class="section-toggle" aria-label="Toggle section">
							<i class="fas fa-chevron-down"></i>
						</button>
					</div>
					<div class="bus-section-content">
						${renderBusCards(preferredBuses)}
					</div>
				`;

				busInfoSections.appendChild(preferredSection);
				applySectionState(sectionId);
			}

			// Create section for other buses
			const otherBuses = entries.filter(
				([number]) =>
					!state.preferences.preferredRoutes.includes(number) ||
					!state.preferences.showPreferredRoutesFirst,
			);

			if (otherBuses.length > 0) {
				const sectionId = "all-routes-section";
				const otherSection = document.createElement("div");
				otherSection.id = sectionId;
				otherSection.className = "bus-section";

				otherSection.innerHTML = `
					<div class="bus-section-header" role="button" tabindex="0">
						<div class="section-header-content">
							<h3>All Routes</h3>
							<span class="bus-count">${otherBuses.length} buses</span>
						</div>
						<button class="section-toggle" aria-label="Toggle section">
							<i class="fas fa-chevron-down"></i>
						</button>
					</div>
					<div class="bus-section-content">
						${renderBusCards(otherBuses)}
					</div>
				`;

				busInfoSections.appendChild(otherSection);
				applySectionState(sectionId);
			}

			fragment.appendChild(busInfoSections);
			elements.busInfoList.appendChild(fragment);
			hideError();
		} else {
			Logger.warn("No bus information available");
			displayError("No bus information available", true);
		}
	}

	// Helper function to render bus cards
	function renderBusCards(buses) {
		return buses
			.sort((a, b) => a[0].localeCompare(b[0]))
			.map(([number, info]) => {
				const isPreferred = state.preferences.preferredRoutes.includes(number);
				const arrivalTime = info.lastArrival;
				const timeString = formatArrivalTime(arrivalTime);

				return `
        <div class="bus-item ${info.status.toLowerCase().replace(" ", "-")}${isPreferred ? " preferred" : ""}">
          <div class="bus-content">
            <div class="star-badge" onclick="togglePreferredRoute('${number}', this)">
              <i class="fas fa-star"></i>
            </div>
            <div class="bus-number">${escapeHTML(number)}</div>
            <div class="bus-bay">
              ${info.bay ? `Bay <span class="bus-bay__number">${escapeHTML(info.bay)}</span>` : "Not arrived"}
            </div>
          </div>
        </div>
      `;
			})
			.join("");
	}

	async function togglePreferredRoute(route, element) {
		const busItem = element.closest(".bus-item");
		const isCurrentlyPreferred = busItem.classList.contains("preferred");
		const updatedRoutes = isCurrentlyPreferred
			? state.preferences.preferredRoutes.filter((r) => r !== route)
			: [...state.preferences.preferredRoutes, route];

		try {
			const response = await fetch("/api/preferences/save", {
				method: "POST",
				headers: {
					"Content-Type": "application/json",
					RequestVerificationToken: document.querySelector(
						'input[name="__RequestVerificationToken"]',
					).value,
				},
				body: JSON.stringify({
					...state.preferences,
					preferredRoutes: updatedRoutes,
				}),
			});

			if (!response.ok) throw new Error("Failed to update preferences");

			state.preferences.preferredRoutes = updatedRoutes;
			busItem.classList.toggle("preferred");

			// Re-sort the bus list if showing preferred first
			if (state.preferences.showPreferredRoutesFirst) {
				displayBusInfo(state.busData);
			}
		} catch (error) {
			console.error("Error updating preferences:", error);
		}
	}

	function formatArrivalTime(date) {
		if (!date) return "";

		const now = new Date();
		const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
		const dateToFormat = new Date(date);

		const isToday =
			dateToFormat >= today &&
			dateToFormat < new Date(today.getTime() + 24 * 60 * 60 * 1000);

		return isToday
			? dayjs(dateToFormat).format(CONFIG.TIME_FORMAT.today)
			: dayjs(dateToFormat).format(CONFIG.TIME_FORMAT.other);
	}

	/**
	 * Displays an error message
	 * @param {string} message - Error message to display
	 */
	function displayError(message, critical = false) {
		Logger.warn(message);
		elements.error.textContent = message;
		elements.error.style.display = "block";
		clearTimeout(state.errorTimeout);
		if (critical) return;
		state.errorTimeout = setTimeout(hideError, CONFIG.ERROR_DISPLAY_DURATION);
	}

	/**
	 * Hides the error message
	 */
	function hideError() {
		elements.error.style.display = "none";
	}

	/**
	 * Updates the last fetched time display
	 */
	function updateLastFetchedTime() {
		if (state.lastFetchTime) {
			const timeDiff = Math.round((new Date() - state.lastFetchTime) / 1000);
			elements.lastUpdated.textContent = `Last updated: ${timeDiff} seconds ago`;
		}
	}

	/**
	 * Sets up event listeners for the module
	 */
	function setupEventListeners() {
		Logger.info("Setting up event listeners");
		elements.searchInput.addEventListener(
			"input",
			debounce(handleSearch, CONFIG.SEARCH_DEBOUNCE_DELAY),
		);
		window.addEventListener("online", fetchBusInfo);
		window.addEventListener("focus", fetchBusInfo);

		// Add map section toggle handling
		const mapHeader = document.querySelectorAll(
			".map-section .bus-section-header",
		)[1];
		const mapContent = document.querySelectorAll(
			".map-section .bus-section-content",
		)[1];

		if (mapHeader && mapContent) {
			mapHeader.addEventListener("click", () => {
				mapContent.classList.toggle("collapsed");
				mapHeader.querySelector(".section-toggle").style.transform =
					mapContent.classList.contains("collapsed")
						? "rotate(-180deg)"
						: "rotate(0)";
			});
		}

		const predictionsHeader = document.querySelector(
			".predictions-section .bus-section-header",
		);
		const predictionsContent = document.querySelector(
			".predictions-section .bus-section-content",
		);

		if (predictionsHeader && predictionsContent) {
			predictionsHeader.addEventListener("click", () => {
				// check we're not clicking in an input
				const notClickingInInput = !event.target.closest("input");
				if (!notClickingInInput) {
					return;
				}

				predictionsContent.classList.toggle("collapsed");
				predictionsHeader.querySelector(".section-toggle").style.transform =
					predictionsContent.classList.contains("collapsed")
						? "rotate(-180deg)"
						: "rotate(0)";
			});
		}

		// Add event delegation for section toggling
		document.addEventListener("click", (e) => {
			const header = e.target.closest(".bus-section-header");
			if (header) {
				const section = header.closest(".bus-section");

				if (section) {
					toggleSection(section.id, event);
				}
			}
		});

		// Add keyboard support for accessibility
		document.addEventListener("keydown", (e) => {
			if (e.key === "Enter" || e.key === " ") {
				const header = e.target.closest(".bus-section-header");
				if (header) {
					e.preventDefault();
					const section = header.closest(".bus-section");
					if (section) {
						toggleSection(section.id);
					}
				}
			}
		});
	}

	/**
	 * Handles the search functionality
	 * @param {Event} event - Input event from the search field
	 */
	function handleSearch(event) {
		const searchTerm = event.target.value.toLowerCase();
		Logger.debug("Handling search", { searchTerm });
		const busItems = elements.busInfoList.querySelectorAll(".bus-item");

		for (const item of busItems) {
			const busNumber = item
				.querySelector(".bus-number")
				.textContent.toLowerCase();
			const status = item
				.querySelector(".bus-status")
				.textContent.toLowerCase();
			const isVisible =
				busNumber.includes(searchTerm) || status.includes(searchTerm);
			item.style.display = isVisible ? "" : "none";
		}
	}

	/**
	 * Creates a debounced function that delays invoking func until after wait milliseconds have elapsed since the last time the debounced function was invoked
	 * @param {Function} func - The function to debounce
	 * @param {number} wait - The number of milliseconds to delay
	 * @returns {Function} The debounced function
	 */
	function debounce(func, wait) {
		let timeout;
		return function executedFunction(...args) {
			const later = () => {
				clearTimeout(timeout);
				func(...args);
			};
			clearTimeout(timeout);
			timeout = setTimeout(later, wait);
		};
	}

	/**
	 * Escapes HTML special characters in a given string
	 * @param {string} unsafe - The string to escape
	 * @returns {string} The escaped string
	 */
	function escapeHTML(unsafe) {
		return unsafe
			.replace(/&/g, "&amp;")
			.replace(/</g, "&lt;")
			.replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;")
			.replace(/'/g, "&#039;");
	}

	/**
	 * Loads collapsed section states from localStorage
	 */
	function loadSectionStates() {
		try {
			const stored = localStorage.getItem(CONFIG.SECTION_STORAGE_KEY);
			if (stored) {
				state.collapsedSections = new Set(JSON.parse(stored));
			}
		} catch (error) {
			Logger.error("Error loading section states", { error: error.message });
		}
	}

	/**
	 * Saves collapsed section states to localStorage
	 */
	function saveSectionStates() {
		try {
			localStorage.setItem(
				CONFIG.SECTION_STORAGE_KEY,
				JSON.stringify([...state.collapsedSections]),
			);
		} catch (error) {
			Logger.error("Error saving section states", { error: error.message });
		}
	}

	/**
	 * Toggles a section's collapsed state
	 * @param {string} sectionId - ID of the section to toggle
	 */
	function toggleSection(sectionId, event = null) {
		const section = document.getElementById(sectionId);
		if (!section) return;

		if (event) {
			// check it's not an input element
			const notClickingInInput = !event.target.closest("input");
			if (!notClickingInInput) {
				return;
			}
		}

		const content = section.querySelector(".bus-section-content");
		const header = section.querySelector(".bus-section-header");
		const isCollapsed = state.collapsedSections.has(sectionId);

		if (isCollapsed) {
			state.collapsedSections.delete(sectionId);
			content.style.maxHeight = `${content.scrollHeight}px`;
			header.classList.remove("collapsed");
		} else {
			state.collapsedSections.add(sectionId);
			content.style.maxHeight = "0px";
			header.classList.add("collapsed");
		}

		saveSectionStates();
	}

	/**
	 * Applies the current collapsed state to a section
	 * @param {string} sectionId - ID of the section
	 */
	function applySectionState(sectionId) {
		const section = document.getElementById(sectionId);
		if (!section) return;

		const content = section.querySelector(".bus-section-content");
		const header = section.querySelector(".bus-section-header");
		const isCollapsed = state.collapsedSections.has(sectionId);

		if (isCollapsed) {
			content.style.maxHeight = "0px";
			header.classList.add("collapsed");
		} else {
			content.style.maxHeight = `${content.scrollHeight}px`;
			header.classList.remove("collapsed");
		}
	}

	/**
	 * Initializes the BusInfoModule
	 */
	async function init() {
		// Prevent multiple initializations
		if (state.initialized) {
			Logger.warn("BusInfoModule already initialized");
			return;
		}

		Logger.info("Initializing BusInfoModule");
		try {
			initializeElements();
			await fetchPreferences();
			setupEventListeners();
			await fetchBusInfo();

			// Clear any existing intervals before setting new ones
			clearInterval(state.refreshInterval);
			clearInterval(state.updateTimeInterval);

			state.refreshInterval = setInterval(
				fetchBusInfo,
				CONFIG.REFRESH_INTERVAL,
			);
			state.updateTimeInterval = setInterval(updateLastFetchedTime, 1000);

			loadSectionStates();
			setupPredictionsSearch();

			state.initialized = true;
			Logger.info("BusInfoModule initialization complete");
		} catch (error) {
			Logger.error("Error during initialization", { error: error.message });
		}
	}

	/**
	 * Cleans up the module by clearing intervals
	 */
	function cleanup() {
		clearInterval(state.refreshInterval);
		clearInterval(state.updateTimeInterval);
		state.initialized = false;
		Logger.info("BusInfoModule cleanup complete");
	}

	// Updated fetchAndDisplayPredictions with extra status and JSON validation
	async function fetchAndDisplayPredictions() {
		try {
			const response = await fetch(`${CONFIG.API_ENDPOINT}/predictions`);
			if (response.status === 429) {
				Logger.warn("Too many requests to predictions endpoint (HTTP 429)");
				return;
			}
			if (!response.ok) {
				Logger.warn("Predictions not enabled or failed to fetch");
				return;
			}
			const predictionData = await response.json();
			if (
				!predictionData ||
				typeof predictionData !== "object" ||
				!predictionData.predictions
			) {
				throw new Error("Invalid predictions response format");
			}
			updatePredictionsDisplay(predictionData);
		} catch (error) {
			Logger.error("Error fetching bus predictions", { error: error.message });
		}
	}

	// Updated updatePredictionsDisplay with a guard check
	function updatePredictionsDisplay(predictionData) {
		if (
			!predictionData ||
			typeof predictionData !== "object" ||
			!predictionData.predictions
		) {
			Logger.error("Cannot render predictions: Invalid data format");
			return;
		}
		// ...existing code...
		let predictionsSection = document.querySelector(".predictions-section");
		if (!predictionsSection) {
			predictionsSection = document.createElement("div");
			predictionsSection.className =
				"bus-section map-section predictions-section";

			const predictionsHeader = document.createElement("div");
			predictionsHeader.className = "bus-section-header";

			const predictionsContent = document.createElement("div");
			predictionsContent.className =
				"bus-section-content single-column collapsed";

			predictionsHeader.innerHTML = `
				<div class="section-header-content">
					<h3>Bay Predictions</h3>
					<div class="predictions-search">
						<input type="text" id="predictionsSearch" placeholder="Search bus number...">
					</div>
				</div>
				<button class="section-toggle" aria-label="Toggle section">
					<i class="fas fa-chevron-down"></i>
				</button>
			`;

			predictionsContent.innerHTML = `
				<div id="predictionsList" class="predictions-list"></div>
			`;

			predictionsSection.appendChild(predictionsHeader);
			predictionsSection.appendChild(predictionsContent);

			// Insert after the map section if it exists, otherwise after the bus info list
			const mapSection = document.querySelector(".map-section");
			if (mapSection) {
				mapSection.insertAdjacentElement("afterend", predictionsSection);
			} else {
				elements.busInfoList.insertAdjacentElement(
					"afterend",
					predictionsSection,
				);
			}

			// Add predictions section toggle handling
			predictionsHeader.addEventListener("click", (event) => {
				// Don't toggle if clicking in the search input
				if (event.target.closest(".predictions-search")) return;

				predictionsContent.classList.toggle("collapsed");
				predictionsHeader.querySelector(".section-toggle").style.transform =
					predictionsContent.classList.contains("collapsed")
						? "rotate(-180deg)"
						: "rotate(0)";
			});

			setupPredictionsSearch();
		}

		const predictionsList = document.getElementById("predictionsList");
		if (!predictionsList) return;
		predictionsList.innerHTML = "";

		Object.entries(predictionData.predictions).forEach(([busNumber, info]) => {
			if (info.predictions && info.predictions.length > 0) {
				const card = document.createElement("div");
				card.className = "prediction-card";
				card.dataset.busNumber = busNumber.toLowerCase();
				card.innerHTML = `
					<div class="prediction-header">
						<div class="bus-number">${escapeHTML(busNumber)}</div>
						<div class="confidence">
							<i class="fas fa-chart-line"></i>
							${info.predictions[0].probability}% confidence
						</div>
					</div>
					<div class="prediction-content">
						${info.predictions
							.map(
								(pred) => `
							<div class="predicted-bay">
								Bay ${pred.bay}
								<span class="probability">${pred.probability}%</span>
							</div>`,
							)
							.join("")}
					</div>
				`;
				predictionsList.appendChild(card);
			}
		});
	}

	function setupPredictionsSearch() {
		const searchInput = document.getElementById("predictionsSearch");
		if (!searchInput) return;

		searchInput.addEventListener("input", (e) => {
			const searchTerm = e.target.value.toLowerCase();
			const cards = document.querySelectorAll(".prediction-card");

			cards.forEach((card) => {
				const busNumber = card.dataset.busNumber;
				card.classList.toggle("hidden", !busNumber.includes(searchTerm));
			});
		});
	}

	// Public API
	return {
		/**
		 * Initializes the BusInfoModule
		 */
		init: init,
		/**
		 * Manually refreshes bus information
		 */
		refreshBusInfo: fetchBusInfo,
		/**
		 * Manually refreshes user preferences
		 */
		refreshPreferences: fetchPreferences,
		/**
		 * Cleans up the module by clearing intervals
		 */
		cleanup: cleanup,
	};
})();

// Initialize the module when the DOM is ready
document.addEventListener("DOMContentLoaded", () => {
	Logger.info("DOM content loaded, initializing BusInfoModule");
	BusInfoModule.init();
});
