/**
 * File system utilities for the build process
 */
const fs = require('fs');
const path = require('path');
const config = require('../config/build-config');

/**
 * Safely ensures a directory exists
 * @param {string} dirPath - Directory path to create
 */
function ensureDirectoryExists(dirPath) {
    try {
        if (!fs.existsSync(dirPath)) {
            fs.mkdirSync(dirPath, { recursive: true });
            console.log(`Created directory: ${dirPath}`);
        }
    } catch (err) {
        console.error(`Failed to create directory ${dirPath}:`, err);
        throw err;
    }
}

/**
 * Verify if all specified entry points exist
 * @param {boolean} throwOnMissing - Whether to throw an error if files are missing
 * @returns {Object} Result object with validation information
 */
function verifyEntryPoints(throwOnMissing = false) {
    const results = {
        js: { found: 0, missing: [] },
        scss: { found: 0, missing: [] }
    };
    
    // Check if source directory exists
    if (!fs.existsSync(config.sourceDir)) {
        const error = new Error(`Source directory "${config.sourceDir}" not found!`);
        console.error(error.message);
        if (throwOnMissing) throw error;
        return { valid: false, error };
    }
    
    // Check JavaScript files
    for (const entry of config.entryPoints.js) {
        const fullPath = path.join(config.sourceDir, entry);
        if (fs.existsSync(fullPath)) {
            results.js.found++;
        } else {
            results.js.missing.push(entry);
        }
    }
    
    // Check SCSS files
    for (const entry of config.entryPoints.scss) {
        const fullPath = path.join(config.sourceDir, entry);
        if (fs.existsSync(fullPath)) {
            results.scss.found++;
        } else {
            results.scss.missing.push(entry);
        }
    }
    
    const allValid = results.js.missing.length === 0 && results.scss.missing.length === 0;
    
    if (!allValid && throwOnMissing) {
        const error = new Error('Missing required entry point files');
        error.details = results;
        throw error;
    }
    
    return {
        valid: allValid,
        results
    };
}

/**
 * Print a validation summary to the console
 * @param {Object} validationResult - Result from verifyEntryPoints()
 */
function printValidationSummary(validationResult) {
    const { results } = validationResult;
    
    console.log('\nEntry Point Validation:');
    console.log(`JavaScript files: ${results.js.found}/${results.js.found + results.js.missing.length} found`);
    console.log(`SCSS files: ${results.scss.found}/${results.scss.found + results.scss.missing.length} found`);
    
    if (!validationResult.valid) {
        console.log('\nMissing files:');
        
        if (results.js.missing.length > 0) {
            console.log('  JavaScript:');
            results.js.missing.forEach(file => console.log(`    - ${file}`));
        }
        
        if (results.scss.missing.length > 0) {
            console.log('  SCSS:');
            results.scss.missing.forEach(file => console.log(`    - ${file}`));
        }
        
        console.log('\nPossible solutions:');
        console.log('1. Create the missing files');
        console.log('2. Update build configuration to match actual file paths');
        console.log('3. Check if the "src" directory is correctly specified');
    }
}

module.exports = {
    ensureDirectoryExists,
    verifyEntryPoints,
    printValidationSummary
};
