/**
 * @fileoverview JavaScript module for logging messages
 * @description This module provides logging functionality with different log levels.
 * @version 1.0.0
 */

const Logger = (function () {
    "use strict";

    /**
     * @constant {Object} LOG_LEVELS
     * @description Enumeration of log levels with corresponding numeric values.
     * Higher values indicate higher severity.
     */
    const LOG_LEVELS = Object.freeze({
        TRACE: 0,
        DEBUG: 1,
        INFO: 2,
        WARN: 3,
        ERROR: 4,
        FATAL: 5,
    });

    /**
     * @constant {Object} LOG_COLORS
     * @description Mapping of log levels to their respective console color styles.
     */
    const LOG_COLORS = Object.freeze({
        TRACE: "color: #A9A9A9",
        DEBUG: "color: #4169E1",
        INFO: "color: #2E8B57",
        WARN: "color: #FF8C00",
        ERROR: "color: #DC143C",
        FATAL: "color: #8B0000; font-weight: bold",
    });

    /**
     * @typedef {Object} LoggerConfig
     * @property {number} MIN_LOG_LEVEL - Minimum log level to be displayed
     * @property {number} MAX_CONSOLE_LOGS - Maximum number of logs to keep in console
     * @property {boolean} TRACE_ENABLED - Flag to enable TRACE level logging
     * @property {boolean} PERFORMANCE_LOGGING - Flag to enable performance logging
     * @property {boolean} GROUP_LOGS - Flag to group logs in console
     */

    /**
     * @type {LoggerConfig}
     * @description Default configuration for the Logger
     */
    const CONFIG = {
        MIN_LOG_LEVEL: LOG_LEVELS.WARN,
        MAX_CONSOLE_LOGS: 1000,
        TRACE_ENABLED: false,
        PERFORMANCE_LOGGING: false,
        GROUP_LOGS: true,
    };

    let logCount = 0;
    const performanceMarks = new Map();

    /**
     * Formats the log message with timestamp and context
     * @param {string} level - Log level
     * @param {string} message - Log message
     * @param {Object} [context] - Additional context for the log
     * @returns {string} Formatted log message
     */
    function formatMessage(level, message, context) {
        const timestamp = new Date().toISOString();
        const formattedContext = context
            ? `\n${JSON.stringify(context, null, 2)}`
            : "";
        return `[${timestamp}] [${level}] ${message}${formattedContext}`;
    }

    /**
     * Logs the formatted message to the console if it meets the minimum log level
     * @param {number} level - Numeric log level
     * @param {string} formattedMessage - Pre-formatted log message
     */
    function logToConsole(level, formattedMessage) {
        if (level >= CONFIG.MIN_LOG_LEVEL) {
            const logMethod =
                level === LOG_LEVELS.TRACE
                    ? "trace"
                    : level === LOG_LEVELS.DEBUG
                        ? "debug"
                        : level === LOG_LEVELS.INFO
                            ? "info"
                            : level === LOG_LEVELS.WARN
                                ? "warn"
                                : "error";

            const logColor = LOG_COLORS[Object.keys(LOG_LEVELS)[level]];

            if (CONFIG.GROUP_LOGS) {
                console.groupCollapsed(`%c${formattedMessage}`, logColor);
                console.trace("Log location");
                console.groupEnd();
            } else {
                console[logMethod](`%c${formattedMessage}`, logColor);
            }

            logCount++;
            if (logCount > CONFIG.MAX_CONSOLE_LOGS) {
                console.warn(
                    `Maximum log count (${CONFIG.MAX_CONSOLE_LOGS}) exceeded. Oldest logs may be lost in console.`
                );
            }
        }
    }

    /**
     * Core logging function
     * @param {number} level - Numeric log level
     * @param {string} message - Log message
     * @param {Object} [context] - Additional context for the log
     */
    function log(level, message, context) {
        const formattedMessage = formatMessage(level, message, context);
        logToConsole(level, formattedMessage);
    }

    /**
     * Starts a performance mark for timing operations
     * @param {string} markName - Unique identifier for the performance mark
     */
    function startPerformanceMark(markName) {
        if (CONFIG.PERFORMANCE_LOGGING) {
            const start = performance.now();
            performanceMarks.set(markName, start);
        }
    }

    /**
     * Ends a performance mark and logs the duration
     * @param {string} markName - Identifier of the performance mark to end
     */
    function endPerformanceMark(markName) {
        if (CONFIG.PERFORMANCE_LOGGING && performanceMarks.has(markName)) {
            const end = performance.now();
            const start = performanceMarks.get(markName);
            const duration = end - start;
            performanceMarks.delete(markName);
            log(
                LOG_LEVELS.DEBUG,
                `Performance: ${markName} took ${duration.toFixed(2)}ms`
            );
        }
    }

    /**
     * Displays a large warning message in the console for developers
     */
    function displaySecurityAdvisory() {
        const baseStyles = [
            "font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
            "line-height: 1.5",
            "padding: 15px",
        ].join(";");

        const warningStyles = [
            "background-color: #e84430",
            "color: #ffffff",
            "font-size: 18px",
            "font-weight: bold",
            baseStyles,
        ].join(";");

        const headerStyles = [
            "background-color: #13253c",
            "color: #ffffff",
            "font-size: 16px",
            "font-weight: bold",
            baseStyles,
        ].join(";");

        const bodyStyles = [
            "background-color: #13253c",
            "color: #ffffff",
            "font-size: 14px",
            "font-weight: normal",
            baseStyles,
        ].join(";");

        console.log(
            "%cWARNING: %cDeveloper Console%c" +
            "This area is intended for development use only. " +
            "Do not copy-paste or run code from untrusted sources here. " +
            "Executing unfamiliar code can compromise your account and system security. " +
            "Only use this console if you fully understand the implications of your actions. " +
            "This is a site for bus information, not a site for storing sensitive information. " +
            "Please do not enter any sensitive information here or interact with any prompts. " +
            "If you see any unexpected prompts, close this page immediately.",
            warningStyles,
            headerStyles,
            bodyStyles
        );
    }

    // Display security advisory
    displaySecurityAdvisory();

    // Public API
    return {
        /**
         * Updates the Logger configuration
         * @param {Partial<LoggerConfig>} newConfig - New configuration options
         */
        setConfig: function (newConfig) {
            Object.assign(CONFIG, newConfig);
        },
        /**
         * Logs a TRACE level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        trace: (message, context) => {
            if (CONFIG.TRACE_ENABLED) log(LOG_LEVELS.TRACE, message, context);
        },
        /**
         * Logs a DEBUG level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        debug: (message, context) => log(LOG_LEVELS.DEBUG, message, context),
        /**
         * Logs an INFO level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        info: (message, context) => log(LOG_LEVELS.INFO, message, context),
        /**
         * Logs a WARN level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        warn: (message, context) => log(LOG_LEVELS.WARN, message, context),
        /**
         * Logs an ERROR level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        error: (message, context) => log(LOG_LEVELS.ERROR, message, context),
        /**
         * Logs a FATAL level message
         * @param {string} message - Log message
         * @param {Object} [context] - Additional context
         */
        fatal: (message, context) => log(LOG_LEVELS.FATAL, message, context),
        /**
         * Starts a performance mark
         * @param {string} markName - Unique identifier for the performance mark
         */
        startPerformanceMark: startPerformanceMark,
        /**
         * Ends a performance mark and logs the duration
         * @param {string} markName - Identifier of the performance mark to end
         */
        endPerformanceMark: endPerformanceMark,
        /**
         * Returns the current log count
         * @returns {number} Total number of logs
         */
        getLogCount: () => logCount,
    };
})();

// Added default export to allow default import in businfo.js
export default Logger;
