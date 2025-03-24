/**
 * Gulp build system configuration
 * Handles SCSS compilation, JavaScript bundling, and asset optimization
 * @module gulpfile
 */

// Import core Gulp functions and plugins
const { src, dest, watch, series, parallel } = require("gulp");
const merge = require("merge-stream");
const sass = require("gulp-sass")(require("sass"));
const autoprefixer = require("autoprefixer");
const postcss = require("gulp-postcss");
const terser = require("gulp-terser");
const javascriptObfuscator = require("gulp-javascript-obfuscator");
const plumber = require("gulp-plumber");
const removeSourcemaps = require("gulp-remove-sourcemaps");
const rollup = require("rollup");
const { nodeResolve } = require("@rollup/plugin-node-resolve");
const source = require("vinyl-source-stream");
const buffer = require("vinyl-buffer");
const { rimraf } = require("rimraf");
const path = require("path");
const commonjs = require("@rollup/plugin-commonjs");

// Import local utilities
const config = require("./config/build-config");
const {
	createDebugStream,
	createBufferingStream,
} = require("./utils/stream-utils");
const {
	ensureDirectoryExists,
	verifyEntryPoints,
	printValidationSummary,
} = require("./utils/file-utils");
const { uploadToS3, invalidateCache } = require("./utils/deploy-utils");

/**
 * Processes SCSS files into optimized CSS
 * @param {Function} cb - Callback function
 */
function buildStyles(cb) {
	try {
		// Verify files exist
		const validation = verifyEntryPoints(false);

		if (
			validation.results.scss.missing.length === config.entryPoints.scss.length
		) {
			console.error("No valid SCSS files found");
			return cb();
		}

		const tasks = [];
		let completedTasks = 0;
		const validEntries = config.entryPoints.scss.filter(
			(entry) => !validation.results.scss.missing.includes(entry),
		);
		const totalTasks = validEntries.length;

		for (const entry of validEntries) {
			// Extract filename and directory structure
			const pathParts = entry.split("/");
			const filename = pathParts.pop();
			const subdirs = pathParts.slice(1).join("/");

			// Determine output path
			const outputPath = config.isProd
				? path.join(config.outputDir, "css", subdirs)
				: path.join(config.devOutputDirs.css, subdirs);

			ensureDirectoryExists(outputPath);

			console.log(`Processing SCSS: ${entry} to ${outputPath}`);

			// Create stream with proper error handling and buffering
			const stream = src(config.getSourcePath(entry), {
				sourcemaps: !config.isProd,
			})
				.pipe(createDebugStream(`SCSS-${filename}`))
				.pipe(
					plumber({
						errorHandler: function (err) {
							console.error(`SCSS Error (${entry}):`, err);
							this.emit("end");
						},
					}),
				)
				.pipe(createBufferingStream())
				.pipe(
					sass({
						outputStyle: "compressed",
						includePaths: ["node_modules", config.getSourcePath("styles")],
					}).on("error", sass.logError),
				)
				.pipe(postcss([autoprefixer()]))
				.pipe(config.isProd ? removeSourcemaps() : buffer())
				.pipe(createDebugStream(`SCSS-${filename}-output`))
				.pipe(dest(outputPath, { sourcemaps: !config.isProd ? "." : false }))
				.on("end", () => {
					completedTasks++;
					if (completedTasks === totalTasks) {
						console.log("All SCSS tasks completed");
						cb();
					}
				});

			tasks.push(stream);
		}

		// If no tasks, just return
		if (tasks.length === 0) {
			console.log("No SCSS files to process");
			return cb();
		}

		// Process streams sequentially to avoid write-after-end errors
		return tasks.reduce((promise, stream) => {
			return promise.then(
				() =>
					new Promise((resolve) => {
						stream.on("end", resolve);
					}),
			);
		}, Promise.resolve());
	} catch (err) {
		console.error("buildStyles error:", err);
		cb(err);
	}
}

/**
 * Bundles and optimizes JavaScript files
 * @returns {Promise<void>} Promise that resolves when all files are processed
 */
async function minifyJs() {
	try {
		// Verify files exist
		const validation = verifyEntryPoints(false);

		if (validation.results.js.missing.length === config.entryPoints.js.length) {
			console.error("No valid JavaScript files found");
			return;
		}

		const validEntries = config.entryPoints.js.filter(
			(entry) => !validation.results.js.missing.includes(entry),
		);

		const results = [];

		for (const entry of validEntries) {
			try {
				console.log(`Loading JS entry: ${entry}`);

				const bundle = await rollup.rollup({
					input: config.getSourcePath(entry),
					plugins: [nodeResolve(), commonjs()],
					onwarn: (warning, warn) => {
						console.log(`Rollup warning for ${entry}:`, warning.message);
					},
				});

				// Extract filename and directory structure
				const pathParts = entry.split("/");
				const filename = pathParts.pop();
				const subdirs = pathParts.slice(1).join("/"); // Remove 'js/' prefix

				// Construct output path
				const outputPath = config.isProd
					? path.join(config.outputDir, "js", subdirs)
					: path.join(config.devOutputDirs.js, subdirs);

				const outputFile = path.join(outputPath, filename);

				// Ensure directory exists
				ensureDirectoryExists(outputPath);

				console.log(`Bundling JS: ${entry} to ${outputFile}`);

				// Generate bundle in IIFE format
				await bundle.write({
					file: outputFile,
					format: "iife",
					name: filename.replace(".js", ""),
					sourcemap: !config.isProd,
				});

				if (config.isProd) {
					results.push(
						new Promise((resolve, reject) => {
							src(outputFile)
								.pipe(
									plumber({
										errorHandler: function (err) {
											console.error(`JS processing error (${entry}):`, err);
											this.emit("end");
										},
									}),
								)
								.pipe(buffer())
								.pipe(terser(config.js.minifyOptions))
								.pipe(javascriptObfuscator(config.js.obfuscateOptions))
								.pipe(removeSourcemaps())
								.pipe(dest(outputPath))
								.on("error", reject)
								.on("end", resolve);
						}),
					);
				}
			} catch (err) {
				console.error(`JS processing error for ${entry}:`, err);
			}
		}

		await Promise.all(results);
		console.log("JavaScript processing complete");
		return;
	} catch (err) {
		console.error("minifyJs error:", err);
		throw err;
	}
}

/**
 * Watches for file changes and triggers rebuilds
 * @param {Function} cb - Callback function
 */
function watchTask(cb) {
	// Watch all SCSS files but only rebuild entry points
	watch([`${config.sourceDir}/styles/**/*.scss`], function (changedCb) {
		buildStyles(() => changedCb());
	})
		.on("change", (path) => console.log(`SCSS: ${path} changed`))
		.on("error", (err) => console.log(`SCSS Error: ${err}`));

	// Watch JS files including modules directory
	watch(
		[
			...config.entryPoints.js.map((entry) => config.getSourcePath(entry)),
			`${config.getSourcePath(config.js.modulesDir)}/**/*.js`,
		],
		minifyJs,
	)
		.on("change", (path) => console.log(`JS: ${path} changed`))
		.on("error", (err) => console.log(`JS Error: ${err}`));

	cb();
}

/**
 * Cleans up the output directory
 * @returns {Promise<void>}
 */
async function cleanup() {
	if (config.isProd) {
		return rimraf(config.outputDir);
	}
	return Promise.resolve();
}

/**
 * Verifies that all paths and files exist before building
 * @param {Function} cb - Callback function
 */
function verifyPaths(cb) {
	try {
		const validation = verifyEntryPoints(false);
		printValidationSummary(validation);

		// In production, fail if any required files are missing
		if (config.isProd && !validation.valid) {
			return cb(new Error("Missing required files in production build"));
		}

		// Create necessary directories
		ensureDirectoryExists(config.outputDir);
		ensureDirectoryExists(path.join(config.outputDir, "js"));
		ensureDirectoryExists(path.join(config.outputDir, "css"));

		cb();
	} catch (err) {
		cb(err);
	}
}

/**
 * Export build tasks
 */
exports.styles = buildStyles;
exports.scripts = minifyJs;
exports.watch = watchTask;
exports.verify = verifyPaths;
exports.build = config.isProd
	? series(
			cleanup,
			verifyPaths,
			parallel(buildStyles, minifyJs),
			uploadToS3,
			invalidateCache,
		)
	: parallel(buildStyles, minifyJs);
exports.default = series(
	verifyPaths,
	parallel(buildStyles, minifyJs),
	watchTask,
);
