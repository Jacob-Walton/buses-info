/**
 * Shared configuration for build processes
 * Used by gulpfile.js and related utilities
 */
const path = require("path");

// Environment configuration
const isProd = process.env.NODE_ENV === "production";

/**
 * Build configuration object
 */
const config = {
	// Source and output paths
	sourceDir: "src",
	outputDir: "dist",
	devOutputDirs: {
		js: "../wwwroot/js",
		css: "../wwwroot/css",
	},

	// Entry points
	entryPoints: {
		js: [
			"js/site.js",
			"js/businfo.js",
			"js/settings.js",
			"js/authTransitions.js",
			"js/contentPages.js",
			"js/navbar.js",
		],
		scss: ["styles/site.scss"],
	},

	// JavaScript configuration
	js: {
		modulesDir: "js/modules",
		minifyOptions: {
			compress: {
				drop_console: true,
				drop_debugger: true,
			},
		},
		obfuscateOptions: {
			compact: true,
			controlFlowFlattening: false,
			deadCodeInjection: false,
			debugProtection: false,
			disableConsoleOutput: true,
			identifierNamesGenerator: "hexadecimal",
			renameGlobals: false,
			rotateStringArray: true,
			selfDefending: false,
			shuffleStringArray: true,
			splitStrings: true,
			splitStringsChunkLength: 5,
			stringArray: true,
			stringArrayEncoding: ["base64"],
			stringArrayThreshold: 0.5,
			transformObjectKeys: false,
			unicodeEscapeSequence: false,
		},
	},

	// Deployment configuration
	deployment: {
		s3: {
			bucket: process.env.BUCKET,
			region: process.env.REGION,
		},
		cloudfront: {
			distributionId: process.env.DISTRIBUTION_ID,
		},
	},

	// Environment helpers
	isProd: isProd,

	// Path helpers
	getSourcePath: function (filePath) {
		return path.join(this.sourceDir, filePath);
	},

	getOutputPath: function (type, subdir = "") {
		const baseDir = isProd
			? path.join(this.outputDir, type)
			: this.devOutputDirs[type];
		return path.join(baseDir, subdir);
	},
};

module.exports = config;
