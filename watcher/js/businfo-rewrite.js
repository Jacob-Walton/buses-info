import Logger from "./modules/logging.js";

/**
 * @fileoverview JavaScript module for displaying bus information
 * @description This module is responsible for fetching bus information from the API and displaying it for a user.
 * @version 0.2.0
 * @author Jacob Walton
 * @requires Logger
 * @module BusInfoMod
 */
const BusInfoModule = () => {
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
		collapsedSections: fetchCollapsedSections() || [],
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
	function toggleLoadingIndicator(show) {
		state.isLoading = show;
		elements.loadingIndicator.style.display = show ? "flex" : "none";
		Logger.debug(`Loading indicator ${show ? "shown" : "hidden"}`);
	}

	function displayBusMap() {
		if (state.mapDisplayed) return;

		const mapSection = document.createElement("div");
		mapSection.classname = "bus-section map-section";

		const mapHeader = document.createElement("div");
		mapHeader.className = "bus-section-header";

		const mapContent = document.createElement("div");
		mapContent.className = "bus-section-content single-column collapsed";

		mapHeader.innerHTML = `
            <div class="section-header-content">
                <h3>Bus Map</h3>
            </div>
            <button class="section-toggle" aria-label="Toggle section">
                <i class="fas fa-chevron-up"></i>
            </button>
        `;

		mapContent.innerHTML = `
            <div id="busMap"></div>
        `;

		const map = mapContent.querySelector("#busMap");

		fetch(`${CONFIG.API_ENDPOINT}/map`) // Fetch bus map image
			.then((response) => response.blob()) // Convert response to blob
			.then((blob) => {
				const url = URL.createObjectURL(blob); // Create URL for blob
				map.innerHTML = `<img src="${url}" alt="Bus map" class="bus-map">`; // Display image
			})
			.catch((error) => {
				// Handle errors
				map.innerHTML = `<p class="error">Failed to load bus map</p>`;
				Logger.error("Failed to load bus map", error);
			});

		mapSection.appendChild(mapHeader); // Add header to section
		mapSection.appendChild(mapContent); // Add content to section

		// Insert map section after bus info list
		elements.busInfoList.insertAdjacentElement("afterend", mapSection);

		// Add event listener to toggle map section
		mapHeader.addEventListener("click", () => {
			mapContent.classList.toggle("collapsed");
			mapHeader.querySelector(".section-toggle").style.transform =
				mapContent.classList.contains("collapsed")
					? "rotate(0)"
					: "rotate(180deg)";
		});

		// Add event listener to toggle map section with enter/space key
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

		// Set map displayed flag
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
			displayBusMap();

			state.lastFetchTime = new Date();
			updateLastFetchedTime();

			// Fetch and update predictions from the API
			fetchAndDisplayPredictions();

			Logger.debug("Bus information fetched successfully", {
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

			// Create a document fragment to append list items to
			const fragment = document.createDocumentFragment();
			const entries = Object.entries(busData);

			// Create a container for all bus sections
			const busInfoSections = document.createElement("div");
			businfoSections.className = "bus-info-sections";

			// Create preferred section if there are preferred routes
			const preferredBuses = entries.filter(([number]) =>
				preferences.preferredRoutes.includes(number),
			);

			if (preferredBuses.length > 0 && preferences.showPreferredRoutesFirst) {
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
                        ${renderBusCards(preferredBuses, preferences)}
                    </div>
                `;

				busInfoSections.appendChild(preferredSection);
				applySectionState(sectionId);
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
                        ${renderBusCards(otherBuses, preferences)}
                    </div>
                `;

				busInfoSections.appendChild(otherSection);
				applySectionState(sectionId);
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

				return `
            <div class="bus-item ${info.status.toLowerCase().replace(" ", "-")}${isPreferred ? " preferred" : ""}">
                <div class="bus-content">
                    <div class="star-badge" onclick="togglePreferredRoute('${number}', this)">
                        <i class="fas fa-star"></i>
                    </div>
                    <div class="bus-number>${escapeHTML(number)}</div>
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
};

export default BusInfoModule;
