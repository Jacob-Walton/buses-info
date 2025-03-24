/**
 * Utility script to verify file paths before running the build
 * Run with: node check-paths.js
 */
const config = require("./config/build-config");
const {
	verifyEntryPoints,
	printValidationSummary,
} = require("./utils/file-utils");

console.log("Bus Info Path Checker");
console.log("====================");
console.log(
	`Current environment: ${config.isProd ? "PRODUCTION" : "DEVELOPMENT"}`,
);
console.log(`Source directory: ${config.sourceDir}`);
console.log(
	`Output directory: ${config.isProd ? config.outputDir : "Development mode - using wwwroot"}`,
);

// Perform validation
const validation = verifyEntryPoints();
printValidationSummary(validation);

// Exit with error code if validation failed
if (!validation.valid) {
	process.exit(1);
} else {
	console.log("\n✓ All entry point files verified successfully");
}
