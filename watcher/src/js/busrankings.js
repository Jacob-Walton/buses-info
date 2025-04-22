import Logger from "./modules/logging.js";

/**
 * @fileoverview JavaScript module for displaying bus rankings
 * @description This module handles fetching and displaying bus service rankings based on historical data.
 * @version 1.0.0
 * @requires Logger
 * @module BusRankingsModule
 */
const BusRankingsModule = (() => {
    /**
     * @constant {Object} CONFIG
     * @description Configuration object for the module
     */
    const CONFIG = Object.freeze({
        API_ENDPOINT: "/api/v2/businfo/rankings",
        REFRESH_INTERVAL: 3600000, // 1 hour
        ERROR_DISPLAY_DURATION: 5000,
        SELECTORS: Object.freeze({
            rankingsContainer: "#rankingsContainer",
            lastUpdated: "#lastUpdated",
            error: "#error",
            loadingIndicator: "#loadingIndicator",
        }),
    });

    /**
     * @typedef {Object} BusRankingsState
     * @property {boolean} isLoading - Flag indicating if data is being fetched
     * @property {Date|null} lastFetchTime - Timestamp of the last successful fetch
     * @property {number|null} errorTimeout - Timeout ID for error message
     * @property {Object|null} rankingsData - Fetched rankings data
     * @property {number|null} refreshInterval - Interval ID for data refresh
     * @property {number|null} updateTimeInterval - Interval ID for updating timestamps
     * @property {boolean} initialized - Whether the module has been initialized
     */
    const state = {
        isLoading: false,
        lastFetchTime: null,
        errorTimeout: null,
        rankingsData: null,
        refreshInterval: null,
        updateTimeInterval: null,
        initialized: false,
    };

    /** @type {Object.<string, HTMLElement} */
    const elements = {};

    /**
     * Initializes DOM elements used by the module
     * @throws {Error} If any required elements are missing
     */
    function initializeElements() {
        Logger.info("Initializing BusRankingsModule elements");
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
     * Fetches bus rankings from the API
     * @returns {Promise<void>}
     */
    async function fetchRankings() {
        Logger.info("Fetching bus rankings");
        Logger.startPerformanceMark("fetchRankings");
        showLoading(true);

        try {
            // Fetch bus rankings from the API
            const response = await fetch(CONFIG.API_ENDPOINT);
            if (!response.ok)
                throw new Error(
                    `Failed to fetch bus rankings: ${response.status} ${response.statusText}`,
                );

            const data = await response.json();

            if (!data || typeof data !== "object" || !data.rankings) {
                throw new Error("Invalid API response");
            }

            state.rankingsData = data;
            displayRankings();

            state.lastFetchTime = new Date();
            updateLastFetchedTime();

            Logger.debug("Bus rankings fetched successfully", {
                rankingsCount: Object.keys(state.rankingsData.rankings).length,
            });
        } catch (error) {
            Logger.error("Error fetching bus rankings", { error: error.message });
            displayError("Unable to fetch bus rankings. Please try again.");
        } finally {
            showLoading(false);
            Logger.endPerformanceMark("fetchRankings");
        }
    }

    /**
     * Displays fetched bus rankings
     */
    function displayRankings() {
        Logger.debug("Displaying bus rankings");
        const rankingsData = state.rankingsData;

        if (rankingsData && rankingsData.rankings && Object.keys(rankingsData.rankings).length > 0) {
            elements.rankingsContainer.innerHTML = ""; // Clear existing content
            
            // Get all rankings and sort by rank
            const sortedRankings = Object.values(rankingsData.rankings)
                .sort((a, b) => a.rank - b.rank);
            
            const fragment = document.createDocumentFragment();
            
            // Create top 3 podium section
            if (sortedRankings.length >= 3) {
                const topBusesSection = document.createElement("div");
                topBusesSection.className = "top-buses";
                
                const sectionHeader = document.createElement("div");
                sectionHeader.className = "top-buses-header";
                sectionHeader.textContent = "Top Performing Bus Services";
                topBusesSection.appendChild(sectionHeader);
                
                const podium = document.createElement("div");
                podium.className = "podium";
                
                // Create the podium with 1st, 2nd, and 3rd places
                const positions = [
                    { place: "second", rank: 2 },
                    { place: "first", rank: 1 },
                    { place: "third", rank: 3 }
                ];
                
                positions.forEach(position => {
                    const bus = sortedRankings.find(r => r.rank === position.rank);
                    if (bus) {
                        const podiumPlace = document.createElement("div");
                        podiumPlace.className = `podium-place ${position.place}`;
                        
                        podiumPlace.innerHTML = `
                            <div class="position">${position.rank}</div>
                            <div class="bus-service">${escapeHTML(bus.service)}</div>
                            <div class="score">${bus.score} points</div>
                            <div class="pedestal"></div>
                        `;
                        
                        podium.appendChild(podiumPlace);
                    }
                });
                
                topBusesSection.appendChild(podium);
                fragment.appendChild(topBusesSection);
            }
            
            // Create the rankings table
            const rankingsTable = document.createElement("div");
            rankingsTable.className = "rankings-table";
            
            // Add the table header
            const tableHeader = document.createElement("div");
            tableHeader.className = "table-header";
            tableHeader.innerHTML = `
                <div class="header-cell rank">Rank</div>
                <div class="header-cell service">Bus Service</div>
                <div class="header-cell score">Score</div>
            `;
            rankingsTable.appendChild(tableHeader);
            
            // Add a row for each bus service
            sortedRankings.forEach(ranking => {
                const row = document.createElement("div");
                row.className = "table-row";
                
                // Add special styling for top 3
                if (ranking.rank <= 3) {
                    row.classList.add("top-rank", `rank-${ranking.rank}`);
                }
                
                row.innerHTML = `
                    <div class="cell rank">${ranking.rank}</div>
                    <div class="cell service">${escapeHTML(ranking.service)}</div>
                    <div class="cell score">${ranking.score}</div>
                `;
                
                rankingsTable.appendChild(row);
            });
            
            fragment.appendChild(rankingsTable);
            elements.rankingsContainer.appendChild(fragment);
            hideError();
        } else {
            Logger.warn("No rankings data to display");
            displayError("No bus rankings available", true);
        }
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
            const timeText = timeDiff < 60 
                ? `${timeDiff} seconds ago` 
                : `${Math.floor(timeDiff / 60)} minutes ago`;
            elements.lastUpdated.textContent = `Last updated ${timeText}`;
        }
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
     * Initializes the module
     */
    async function init() {
        // Prevent multiple initializations
        if (state.initialized) {
            Logger.warn("Module already initialized");
            return;
        }

        Logger.info("Initializing BusRankingsModule");
        try {
            initializeElements();
            await fetchRankings();

            // Clear any existing intervals
            clearInterval(state.refreshInterval);
            clearInterval(state.updateTimeInterval);

            state.refreshInterval = setInterval(
                fetchRankings,
                CONFIG.REFRESH_INTERVAL,
            );
            state.updateTimeInterval = setInterval(updateLastFetchedTime, 60000); // Update every minute

            state.initialized = true;
            Logger.info("BusRankingsModule initialized successfully");
        } catch (error) {
            Logger.error("Failed to initialize BusRankingsModule", {
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
        Logger.info("BusRankingsModule cleaned up");
    }

    /**
     * Public API
     */
    return {
        init: init,
        refreshRankings: fetchRankings,
        cleanup: cleanup,
    };
})();

document.addEventListener("DOMContentLoaded", BusRankingsModule.init);

export default BusRankingsModule;
