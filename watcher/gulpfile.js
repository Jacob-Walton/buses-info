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
const { rimraf } = require('rimraf');
const { CloudFront } = require("@aws-sdk/client-cloudfront");

/**
 * Environment configuration flag
 * @type {boolean}
 */
// Production mode detection
const isProd = process.env.NODE_ENV === "production";

/**
 * Build configuration object
 * @type {Object}
 * @property {string[]} entryPoints - JavaScript entry point files
 * @property {string} modulesDir - Directory containing JavaScript modules
 * @property {string} outputDir - Output directory for processed files
 */
// Build paths and entry points configuration
const config = {
	entryPoints: ["js/site.js", "js/businfo.js", "js/settings.js"],
	modulesDir: "js/modules",
	outputDir: "dist",
	devOutputDirs: {
		js: "../wwwroot/js",
		css: "../wwwroot/css"
	},
	s3: {
		bucket: process.env.BUCKET, // e.g. "my-bucket-name"
		region: process.env.REGION // e.g. "uk-west-1"
	},
	cloudfront: {
		distributionId: 'EPH6L1KTA2E87'
	}
};

const s3 = require("gulp-s3-upload")({
	accessKeyId: process.env.AWS_ACCESS_KEY_ID,
	secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
});

/**
 * Uploads files to an S3 bucket
 */
function uploadToS3() {
	// Upload JS files
	const jsUpload = src(`${config.outputDir}/js/**/*.js`)
		.pipe(s3({
			Bucket: config.s3.bucket,
			ACL: "public-read",
			keyTransform: function (relative_filename) {
				return `js/${relative_filename}`;
			}
		}));

	// Upload CSS files
	const cssUpload = src(`${config.outputDir}/css/**/*.css`)
		.pipe(s3({
			Bucket: config.s3.bucket,
			ACL: "public-read",
			keyTransform: function (relative_filename) {
				return `css/${relative_filename}`;
			}
		}));

	return merge(jsUpload, cssUpload);
}

/**
 * Invalidates CloudFront cache
 */
async function invalidateCache() {
	const cloudfront = new CloudFront({
		region: config.s3.region,
		credentials: {
			accessKeyId: process.env.AWS_ACCESS_KEY_ID,
			secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
		}
	});

	const params = {
		DistributionId: config.cloudfront.distributionId,
		InvalidationBatch: {
			CallerReference: Date.now().toString(),
			Paths: {
				Quantity: 2,
				Items: ['/js/*', '/css/*']
			}
		}
	};

	try {
		await cloudfront.createInvalidation(params);
		console.log('CloudFront cache invalidation initiated');
	} catch (err) {
		console.error('CloudFront invalidation error:', err);
	}
}

/**
 * Processes SCSS files into optimized CSS
 * - Compiles SCSS to CSS
 * - Adds vendor prefixes
 * - Minifies output
 * - Handles sourcemaps based on environment
 * @returns {NodeJS.ReadWriteStream} Gulp stream
 */
function buildStyles() {
	// Initialize stream
	let stream = src("src/styles/**/*.scss", { sourcemaps: !isProd })
		// Error handling to prevent watch task from breaking
		.pipe(
			plumber({
				errorHandler: function (err) {
					console.log(err);
					this.emit("end");
				},
			}),
		)
		// Process SCSS
		.pipe(
			sass({
				outputStyle: "compressed",
				includePaths: ["node_modules"],
			}).on("error", sass.logError),
		)
		// Apply vendor prefixes via autoprefixer
		.pipe(postcss([autoprefixer()]));

	// Strip sourcemaps in production mode
	if (isProd) {
		stream = stream.pipe(removeSourcemaps());
		return stream.pipe(dest(config.outputDir + "/css", { sourcemaps: false }));
	}

	// Output to destination
	return stream.pipe(dest(config.devOutputDirs.css, { sourcemaps: "." }));
}

/**
 * Bundles and optimizes JavaScript files
 * - Bundles modules using Rollup
 * - Minifies code in production
 * - Applies obfuscation in production
 * - Handles sourcemaps based on environment
 * @returns {Promise<void[]>} Promise that resolves when all files are processed
 */
async function minifyJs() {
    const tasks = config.entryPoints.map(async (entry) => {
        // Bundle using Rollup
        const bundle = await rollup.rollup({
            input: entry,
            plugins: [nodeResolve()],
        });

        // Extract filename from entry path
        const filename = entry.split("/").pop();
        const outputPath = isProd 
            ? `${config.outputDir}/js/${filename}`
            : `${config.devOutputDirs.js}/${filename}`;

        // Generate bundle in IIFE format
        await bundle.write({
            file: outputPath,
            format: "iife",
            name: filename.replace(".js", ""),
            sourcemap: !isProd,
        });

        if (isProd) {
            return src(outputPath)
                .pipe(buffer())
                .pipe(
                    terser({
                        compress: {
                            drop_console: true,
                            drop_debugger: true,
                        },
                    })
                )
                .pipe(
                    javascriptObfuscator({
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
                    })
                )
                .pipe(removeSourcemaps())
                .pipe(dest(config.outputDir + "/js"));
        }

        return Promise.resolve();
    });

    return Promise.all(tasks);
}

/**
 * Watches for file changes and triggers rebuilds
 * - Monitors SCSS files for style changes
 * - Monitors JS files and modules for script changes
 * @returns {void}
 */
function watchTask() {
	// Watch SCSS files with change logging
	watch(["src/styles/**/*.scss"], buildStyles)
		.on("change", (path) => console.log(`SCSS: ${path} changed`))
		.on("error", (err) => console.log(`SCSS Error: ${err}`));

	// Watch JS files including modules directory
	watch([...config.entryPoints, `${config.modulesDir}/**/*.js`], minifyJs)
		.on("change", (path) => console.log(`JS: ${path} changed`))
		.on("error", (err) => console.log(`JS Error: ${err}`));
}

/**
 * Cleans up the output directory in production
 * @returns {Promise<void>}
 */
async function cleanup() {
    if (isProd) {
        return rimraf(config.outputDir);
    }
    return Promise.resolve();
}

/**
 * Export build tasks
 * @property {Function} styles - Builds CSS files
 * @property {Function} scripts - Builds JavaScript files
 * @property {Function} watch - Starts file watching
 * @property {Function} build - Builds all assets
 * @property {Function} default - Builds assets and starts watching
 */
exports.styles = buildStyles;
exports.scripts = minifyJs;
exports.watch = watchTask;
exports.build = isProd 
    ? series(parallel(buildStyles, minifyJs), uploadToS3, invalidateCache, cleanup)
    : parallel(buildStyles, minifyJs);
exports.default = series(parallel(buildStyles, minifyJs), watchTask);
