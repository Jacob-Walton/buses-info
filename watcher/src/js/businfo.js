import Logger from "./modules/logging.js";

/**
 * @fileoverview JavaScript module for displaying bus information
 * @description This module is responsible for fetching bus information from the API and displaying it for a user.
 * @version 0.2.0
 * @author Jacob Walton
 * @requires Logger
 * @module BusInfoModule
 */
const BusInfoModule = (() => {
	/**
	 * @constant {Object} CONFIG
	 * @description Configuration object for the module
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
	 * @property {number|null} errorTimeout - Timeout ID for error message
	 * @property {Object|null} busData - Fetched bus data
	 * @property {Object} preferences - User preferences
	 * @property {string[]} preferences.preferredRoutes - List of preferred routes
	 * @property {boolean} preferences.showPreferredRoutesFirst - Whether to show preferred routes first
	 * @property {number|null} refreshInterval - Interval ID for data refresh
	 * @property {number|null} updateTimeInterval - Interval ID for updating timestamps
	 * @property {boolean} initialized - Whether the module has been initialized
	 * @property {string[]} collapsedSections - List of collapsed sections
	 * @property {boolean} mapDisplayed - Whether the map is currently displayed
	 * @property {boolean} predictionsDisplayed - Whether the predictions are currently displayed
	 */
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
		collapsedSections: new Set(fetchCollapsedSections() || []),
		mapDisplayed: false,
		predictionsDisplayed: false,
	};

	/** @type {Object.<string, HTMLElement} */
	const elements = {};

	/**
	 * Initializes DOM elements used by the module
	 * @throws {Error} If any required elements are missing
	 */
	function initializeElements() {
		Logger.info("Initializing BusInfoModule elements");
		for (const key in CONFIG.SELECTORS) {
			if (Object.hasOwn(CONFIG.SELECTORS, key)) {
				const element = document.querySelector(CONFIG.SELECTORS[key]);
				if (!element) {
					Logger.error(`Element not found ${CONFIG.SELECTORS[key]}`);
					throw new Error(`Element not found ${CONFIG.SELECTORS[key]}`);
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

	/**
	 * Displays the bus map
	 * @param {Blob} mapBlob - The map image blob
	 * @returns {void}
	 */
	function displayBusMap(mapBlob) {
		if (state.mapDisplayed) return;

		const mapSection = document.createElement("div");
		mapSection.id = "bus-map-section"; // Add ID for state management
		mapSection.className = "bus-section map-section";

		const mapHeader = document.createElement("div");
		mapHeader.className = "bus-section-header";

		const mapContent = document.createElement("div");
		mapContent.className = "bus-section-content single-column";
		mapContent.style.transition = "max-height 0.3s ease-in-out";

		mapHeader.innerHTML = `
			<div class="section-header-content">
				<h3>Bus Map</h3>
			</div>
			<button class="section-toggle" aria-label="Toggle section">
				<i class="fas fa-chevron-up"></i>
			</button>
		`;

		mapContent.innerHTML = `<div id="busMap"></div>`;

		// Append header and content to section before adding the map
		mapSection.appendChild(mapHeader);
		mapSection.appendChild(mapContent);

		// Create and add the map image
		const map = mapContent.querySelector("#busMap");
		const url = URL.createObjectURL(mapBlob);
		const img = new Image();
		img.src = url;
		img.className = "bus-map";
		img.alt = "Bus map";
		map.appendChild(img);

		// Add section to DOM
		elements.busInfoList.insertAdjacentElement("afterend", mapSection);

		// Set initial collapsed state
		const isCollapsed = state.collapsedSections.has("bus-map-section");
		if (isCollapsed) {
			mapContent.classList.add("collapsed");
			mapContent.style.maxHeight = "0px";
			mapHeader.querySelector(".section-toggle").style.transform =
				"rotate(0deg)";
		} else {
			mapContent.style.maxHeight = "500px";
			mapHeader.querySelector(".section-toggle").style.transform =
				"rotate(180deg)";
		}

		state.mapDisplayed = true;
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
				throw new Error(
					`Failed to fetch bus information: ${response.status} ${response.statusText}`,
				);

			const data = await response.json();

			if (!data || typeof data !== "object" || !data.busData) {
				throw new Error("Invalid API response");
			}

			state.busData = data.busData;
			displayBusInfo();

			state.lastFetchTime = new Date();
			updateLastFetchedTime();

			// Move map and predictions fetching outside of bus data success
			fetchAndDisplayMap();
			fetchAndDisplayPredictions();

			Logger.debug("Bus information fetched successfully", {
				busCount: Object.keys(state.busData).length,
			});
		} catch (error) {
			Logger.error("Error fetching bus information", { error: error.message });
			displayError("Unable to fetch bus information. Please try again.");

			// Still try to fetch map and predictions even if bus data fails
			fetchAndDisplayMap();
			fetchAndDisplayPredictions();
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
				showPreferredRoutesFirst: data.showPreferredRoutesFirst || true,
			};
			Logger.debug("User preferences fetched successfully", state.preferences);
		} catch (error) {
			Logger.error("Error fetching preferences", { error: error.message });
		}
	}

	/**
	 * Displays fetched bus information
	 */
	function displayBusInfo() {
		Logger.debug("Displaying bus information");
		const busData = Object.freeze(state.busData);
		const preferences = Object.freeze(state.preferences);

		if (busData && Object.keys(busData).length > 0) {
			elements.busInfoList.innerHTML = ""; // Clear existing list

			const fragment = document.createDocumentFragment();
			const entries = Object.entries(busData);

			// Create a container for all bus sections
			const busInfoSections = document.createElement("div");
			busInfoSections.className = "bus-info-sections";

			// Create preferred section if there are preferred routes
			const preferredBuses = entries.filter(([number]) =>
				preferences.preferredRoutes.includes(number),
			);

			if (preferredBuses.length > 0 && preferences.showPreferredRoutesFirst) {
				const sectionId = "preferred-routes-section";
				const preferredSection = document.createElement("div");
				preferredSection.id = sectionId;
				preferredSection.className = "bus-section preferred-section";

				const isCollapsed = state.collapsedSections.has(sectionId);

				preferredSection.innerHTML = `
					<div class="bus-section-header" role="button" tabindex="0">
						<div class="section-header-content">
							<h3>Preferred Routes</h3>
							<span class="bus-count">${preferredBuses.length} buses</span>
						</div>
						<button class="section-toggle" aria-label="Toggle section" style="transform: ${isCollapsed ? "rotate(0)" : "rotate(180deg)"}">
							<i class="fas fa-chevron-up"></i>
						</button>
					</div>
					<div class="bus-section-content ${isCollapsed ? "collapsed" : ""}" style="max-height: ${isCollapsed ? "0px" : "none"}">
						${renderBusCards(preferredBuses, preferences)}
					</div>
				`;

				busInfoSections.appendChild(preferredSection);
			}

			// Create section for all other buses
			const otherBuses = entries.filter(
				([number]) =>
					!preferences.preferredRoutes.includes(number) ||
					!preferences.showPreferredRoutesFirst,
			);

			if (otherBuses.length > 0) {
				const sectionId = "all-routes-section";
				const otherSection = document.createElement("div");
				otherSection.id = sectionId;
				otherSection.className = "bus-section";

				const isCollapsed = state.collapsedSections.has(sectionId);

				otherSection.innerHTML = `
					<div class="bus-section-header" role="button" tabindex="0">
						<div class="section-header-content">
							<h3>All Routes</h3>
							<span class="bus-count">${otherBuses.length} buses</span>
						</div>
						<button class="section-toggle" aria-label="Toggle section" style="transform: ${isCollapsed ? "rotate(0deg)" : "rotate(180deg)"}">
							<i class="fas fa-chevron-up"></i>
						</button>
					</div>
					<div class="bus-section-content ${isCollapsed ? "collapsed" : ""}" style="max-height: ${isCollapsed ? "0px" : "none"}">
						${renderBusCards(otherBuses, preferences)}
					</div>
				`;

				busInfoSections.appendChild(otherSection);
			}

			fragment.appendChild(busInfoSections);
			elements.busInfoList.appendChild(fragment);
			hideError();
		} else {
			Logger.warn("No bus data to display");
			displayError("No bus information available", true);
		}
	}

	/**
	 * Helper function to render bus cards
	 * @param {Array} buses - Array of bus data entries
	 * @returns {string} - HTML string of bus cards
	 */
	function renderBusCards(buses, preferences) {
		return buses
			.sort((a, b) => a[0].localeCompare(b[0]))
			.map(([number, info]) => {
				const isPreferred = preferences.preferredRoutes.includes(number);
				const arrivalTime = info.lastArrival;
				const timeString = formatArrivalTime(arrivalTime);

				return `
        <div class="bus-item ${info.status.toLowerCase().replace(" ", "-")}${isPreferred ? " preferred" : ""}">
          <div class="bus-content">
            <div class="star-badge" 
                 role="button"
                 tabindex="0"
                 aria-label="${isPreferred ? "Remove from" : "Add to"} preferred routes"
                 aria-pressed="${isPreferred}">
              <i class="fas fa-star" aria-hidden="true"></i>
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

	/**
	 * Toggles the preferred status of a bus route
	 * @param {string} route - Bus route number
	 * @param {HTMLElement} element - Element that triggered the event
	 */
	async function togglePreferredRoute(route, element) {
		const busItem = element.closest(".bus-item");
		if (!busItem) return;

		const isPreferred = busItem.classList.contains("preferred");

		const updatedPreferences = {
			...state.preferences,
			preferredRoutes: isPreferred
				? state.preferences.preferredRoutes.filter((r) => r !== route)
				: [...state.preferences.preferredRoutes, route],
		};

		try {
			const response = await fetch(CONFIG.PREFERENCES_ENDPOINT, {
				method: "PUT",
				headers: {
					"Content-Type": "application/json",
					RequestVerificationToken: document.querySelector(
						'input[name="__RequestVerificationToken"]',
					).value,
				},
				body: JSON.stringify(updatedPreferences),
			});

			if (!response.ok) throw new Error("Failed to update preferences");

			// Update state with new preferences object
			state.preferences = updatedPreferences;

			displayBusInfo();

			Logger.debug(
				`Route ${route} ${isPreferred ? "removed from" : "added to"} preferred routes`,
			);
		} catch (error) {
			Logger.error("Failed to update preferred routes", {
				error: error.message,
			});
			displayError("Failed to update preferred routes. Please try again.");
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
			? dateToFormat.toLocaleTimeString("en-GB", {
					hour: "2-digit",
					minute: "2-digit",
				})
			: dateToFormat.toLocaleString("en-GB", {
					day: "numeric",
					month: "numeric",
					hour: "2-digit",
					minute: "2-digit",
				});
	}

	/**
	 * Displays an error message
	 * @param {string} message - Error message to display
	 * @param {boolean} critical - Whether the error is critical
	 */
	function displayError(message, critical = false) {
		Logger.warn(message);
		elements.error.textContent = message;
		elements.error.style.display = "block";
		clearTimeout(state.errorTimeout);
		if (critical) return;
		state.errorTimeout = setTimeout(
			() => hideError(),
			CONFIG.ERROR_DISPLAY_DURATION,
		);
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
			elements.lastUpdated.textContent = `Last updated ${timeDiff} seconds ago`;
		}
	}

	/**
	 * Sets up event listeners for the module
	 */
	function setupEventListeners() {
		Logger.info("Setting up event listeners");
		elements.searchInput.addEventListener(
			"input",
			debounce(handleSearchInput, CONFIG.SEARCH_DEBOUNCE_DELAY),
		);

		window.addEventListener("online", fetchBusInfo);
		window.addEventListener("offline", () =>
			displayError("No internet connection", true),
		);

		// Remove all other section toggle handlers - we'll use one central handler
		document.addEventListener("click", (event) => {
			// Don't handle if clicking an input
			if (event.target.tagName === "INPUT") return;

			// Find the closest header that was clicked
			const header = event.target.closest(".bus-section-header");
			if (!header) return;

			// Find the section and toggle it
			const section = header.closest(".bus-section");
			if (section?.id) {
				toggleSection(section.id);
			}
		});

		// Keep keyboard navigation
		document.addEventListener("keydown", (event) => {
			if (event.key === "Enter" || event.key === " ") {
				const header = event.target.closest(".bus-section-header");
				if (header) {
					event.preventDefault();
					const section = header.closest(".bus-section");
					if (section) {
						toggleSection(section.id);
					}
				}
			}
		});

		// Star badge event delegation
		elements.busInfoList.addEventListener("click", (event) => {
			const starBadge = event.target.closest(".star-badge");
			if (!starBadge) return;

			const busItem = starBadge.closest(".bus-item");
			if (!busItem) return;

			const busNumber = busItem.querySelector(".bus-number").textContent.trim();
			togglePreferredRoute(busNumber, starBadge);
		});

		// Keyboard support for star badges
		elements.busInfoList.addEventListener("keydown", (event) => {
			if (event.key === "Enter" || event.key === " ") {
				const starBadge = event.target.closest(".star-badge");
				if (starBadge) {
					event.preventDefault();
					const busItem = starBadge.closest(".bus-item");
					const busNumber = busItem
						.querySelector(".bus-number")
						.textContent.trim();
					togglePreferredRoute(busNumber, starBadge);
				}
			}
		});
	}

	/**
	 * Handles the search input event
	 * @param {Event} event - Input event
	 */
	function handleSearchInput(event) {
		const query = event.target.value.trim().toLowerCase();
		const sections = document.querySelectorAll(".bus-section");

		sections.forEach((section) => {
			const buses = section.querySelectorAll(".bus-item");
			buses.forEach((bus) => {
				const number = bus
					.querySelector(".bus-number")
					.textContent.trim()
					.toLowerCase();
				bus.style.display = number.includes(query) ? "flex" : "none";
			});
		});
	}

	/**
	 * Creates a debounced function
	 * @param {Function} func - Function to debounce
	 * @param {number} delay - Debounce delay
	 * @returns {Function} - Debounced function
	 */
	function debounce(func, delay) {
		let timeout;
		return function (...args) {
			clearTimeout(timeout);
			timeout = setTimeout(() => func.apply(this, args), delay);
		};
	}

	/**
	 * Escapes HTML special characters
	 * @param {string} str - String to escape
	 * @returns {string} - Escaped string
	 */
	function escapeHTML(unsafe) {
		return unsafe
			.replace(/&/g, "&amp;")
			.replace(/</g, "&lt;")
			.replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;")
			.replace(/'/g, "&#039;")
			.replace(/`/g, "&#96;");
	}

	/**
	 * Fetches collapsed sections from local storage
	 * @returns {string[]} - List of collapsed section IDs
	 */
	function fetchCollapsedSections() {
		try {
			const sections = localStorage.getItem(CONFIG.SECTION_STORAGE_KEY);
			return sections ? JSON.parse(sections) : [];
		} catch (error) {
			Logger.error("Failed to fetch collapsed sections", {
				error: error.message,
			});
			return [];
		}
	}

	/**
	 * Saves collapsed sections to local storage
	 */
	function saveCollapsedSections() {
		try {
			localStorage.setItem(
				CONFIG.SECTION_STORAGE_KEY,
				JSON.stringify(Array.from(state.collapsedSections)),
			);
		} catch (error) {
			Logger.error("Failed to save collapsed sections", {
				error: error.message,
			});
		}
	}

	/**
	 * Toggles the visibility of a section
	 * @param {string} sectionId - ID of the section to toggle
	 */
	function toggleSection(sectionId) {
		if (!sectionId) {
			Logger.warn("No section ID provided");
			return;
		}

		const section = document.getElementById(sectionId);
		if (!section) return;

		const content = section.querySelector(".bus-section-content");
		const toggle = section.querySelector(".section-toggle");
		const isCollapsed = !content.classList.contains("collapsed");
		const isSpecialSection =
			sectionId === "bus-map-section" || sectionId === "predictions-section";

		if (isCollapsed) {
			state.collapsedSections.add(sectionId);
			content.classList.add("collapsed");
			content.style.maxHeight = "0px";
			toggle.style.transform = "rotate(0deg)";
		} else {
			state.collapsedSections.delete(sectionId);
			content.classList.remove("collapsed");
			content.style.maxHeight = isSpecialSection
				? "500px"
				: `${content.scrollHeight}px`;
			toggle.style.transform = "rotate(180deg)";
		}

		saveCollapsedSections();
	}

	/**
	 * Applies the collapsed state to a section
	 * @param {string} sectionId - ID of the section to apply the state to
	 */
	function applySectionState(sectionId) {
		const section = document.getElementById(sectionId);
		if (!section) return;

		const content = section.querySelector(".bus-section-content");
		const header = section.querySelector(".bus-section-header");

		if (state.collapsedSections.has(sectionId)) {
			content.style.maxHeight = "0px";
			header.classList.add("collapsed");
		} else {
			content.style.maxHeight = `${content.scrollHeight}px`;
			header.classList.remove("collapsed");
		}
	}

	/**
	 * Intializes the module
	 */
	async function init() {
		// Prevent multiple initializations
		if (state.initialized) {
			Logger.warn("Module already initialized");
			return;
		}

		Logger.info("Initializing BusInfoModule");
		try {
			initializeElements();
			await fetchPreferences();
			setupEventListeners();
			await fetchBusInfo();

			// Clear any existing intervals
			clearInterval(state.refreshInterval);
			clearInterval(state.updateTimeInterval);

			state.refreshInterval = setInterval(
				fetchBusInfo,
				CONFIG.REFRESH_INTERVAL,
			);
			state.updateTimeInterval = setInterval(updateLastFetchedTime, 1000);

			state.collapsedSections.forEach((sectionId) =>
				applySectionState(sectionId),
			);

			state.initialized = true;
			Logger.info("BusInfoModule initialized successfully");
		} catch (error) {
			Logger.error("Failed to initialize BusInfoModule", {
				error: error.message,
			});
		}
	}

	/**
	 * Cleans up the module and clears intervals
	 */
	function cleanup() {
		clearInterval(state.refreshInterval);
		clearInterval(state.updateTimeInterval);
		state.initialized = false;
		Logger.info("BusInfoModule cleaned up");
	}

	/**
	 * Fetch and display bus predictions
	 * @returns {Promise<void>}
	 */
	async function fetchAndDisplayPredictions() {
		try {
			const response = await fetch(`${CONFIG.API_ENDPOINT}/predictions`);
			if (response.status === 429) {
				Logger.warn("Too many requests for predictions");
				return;
			}

			if (!response.ok) {
				throw new Error(
					`Failed to fetch bus predictions: ${response.status} ${response.statusText}`,
				);
			}

			const predictions = await response.json();
			if (
				!predictions ||
				typeof predictions !== "object" ||
				!predictions.predictions
			) {
				throw new Error("Invalid API response");
			}

			updatePredictionsDisplay(predictions);
		} catch (error) {
			Logger.error("Error fetching bus predictions", { error: error.message });
		}
	}

	/**
	 * Fetches and displays the bus map
	 * @returns {Promise<void>}
	 */
	async function fetchAndDisplayMap() {
		if (state.mapDisplayed) return;

		try {
			const response = await fetch(`${CONFIG.API_ENDPOINT}/map`);
			if (!response.ok) {
				throw new Error("Failed to fetch map");
			}

			const blob = await response.blob();
			if (blob.size === 0) {
				throw new Error("Empty map image received");
			}

			displayBusMap(blob);
		} catch (error) {
			Logger.error("Failed to load bus map", error);
			state.mapDisplayed = false;
		}
	}

	/**
	 * Creates the predictions section
	 * @returns {HTMLElement} - Predictions section element
	 */
	function createPredictionsSection() {
		if (document.querySelector(".predictions-section")) return;

		const predictionsSection = document.createElement("div");
		predictionsSection.id = "predictions-section"; // Add ID for state management
		predictionsSection.className =
			"bus-section map-section predictions-section";

		const predictionsHeader = document.createElement("div");
		predictionsHeader.className = "bus-section-header";

		const predictionsContent = document.createElement("div");
		predictionsContent.className =
			"bus-section-content single-column collapsed";
		// Set initial max-height to 0 for collapsed state
		predictionsContent.style.maxHeight = "0px";
		predictionsContent.style.transition = "max-height 0.3s ease-in-out";

		predictionsHeader.innerHTML = `
				<div class="section-header-content">
					<h3>Bay Predictions</h3>
					<div class="predictions-search">
						<input type="text" id="predictionsSearch" placeholder="Search bus number...">
					</div>
				</div>
				<button class="section-toggle" aria-label="Toggle section">
					<i class="fas fa-chevron-up"></i>
				</button>
			`;

		predictionsContent.innerHTML = `
				<div id="predictionsList" class="predictions-list"></div>
			`;

		predictionsSection.appendChild(predictionsHeader);
		predictionsSection.appendChild(predictionsContent);

		const mapSection = document.querySelector(".map-section");
		if (mapSection) {
			mapSection.insertAdjacentElement("afterend", predictionsSection);
		} else {
			elements.busInfoList.insertAdjacentElement(
				"afterend",
				predictionsSection,
			);
		}

		// Set initial collapsed state from saved state
		const isCollapsed = state.collapsedSections.has("predictions-section");
		predictionsContent.classList.toggle("collapsed", isCollapsed);
		predictionsContent.style.maxHeight = isCollapsed ? "0px" : "500px";
		predictionsHeader.querySelector(".section-toggle").style.transform =
			isCollapsed ? "rotate(0)" : "rotate(180deg)";

		setupPredictionsSearch();
	}

	/**
	 * Sets up the predictions search input
	 * @returns {void}
	 */
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

	/**
	 * Updates the bus predictions display
	 * @param {Object} predictions - Bus predictions data
	 */
	function updatePredictionsDisplay(predictions) {
		if (
			!predictions ||
			typeof predictions !== "object" ||
			!predictions.predictions ||
			Object.keys(predictions.predictions).length === 0
		) {
			// Remove predictions section if it exists but there's no data
			const existingSection = document.querySelector(".predictions-section");
			if (existingSection) {
				existingSection.remove();
			}
			state.predictionsDisplayed = false;
			Logger.warn("No predictions to display");
			return;
		}

		if (!state.predictionsDisplayed) {
			createPredictionsSection();
		}

		const predictionsList = document.getElementById("predictionsList");
		if (!predictionsList) return;
		predictionsList.innerHTML = "";

		const fragment = document.createDocumentFragment();
		const entries = Object.entries(predictions.predictions);
		let hasValidPredictions = false;

		for (const [busNumber, info] of entries) {
			if (info.predictions && info.predictions.length > 0) {
				hasValidPredictions = true;
				const card = document.createElement("div");
				card.className = "prediction-card";
				card.dataset.busNumber = busNumber.toLowerCase();

				card.innerHTML = `
					<div class="prediction-header">
						<div class="bus-number">${escapeHTML(busNumber)}</div>
						<div class="confidence">
							<i class="fas fa-chart-line"></i>
							${info.overallConfidence}% confidence
						</div>
					</div>
					<div class="prediction-content">
						${info.predictions
							.map(
								(pred) => `
								<div class="predicted-bay">
									${pred.bay === "No historical data" ? "No historical data" : `Bay ${pred.bay}`}
									<span class="probability">${pred.probability}%</span>
								</div>
								`,
							)
							.join("")}
					</div>
				`;

				fragment.appendChild(card);

				predictionsList.appendChild(fragment);
			}
		}

		if (!hasValidPredictions) {
			// Remove section if no valid predictions
			const section = document.querySelector(".predictions-section");
			if (section) {
				section.remove();
			}
			state.predictionsDisplayed = false;
			return;
		}

		predictionsList.appendChild(fragment);
		state.predictionsDisplayed = true;
	}

	/**
	 * Public API
	 */
	return {
		init: init,
		refreshBusInfo: fetchBusInfo,
		refreshPreferences: fetchPreferences,
		cleanup: cleanup,
	};
})();

document.addEventListener("DOMContentLoaded", BusInfoModule.init);

export default BusInfoModule;
