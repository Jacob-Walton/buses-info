/**
 * Gulp build system configuration
 * Handles SCSS compilation, JavaScript bundling, and asset optimization
 * @module gulpfile
 */

// Import core Gulp functions and plugins
const { src, dest, watch, series, parallel } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const autoprefixer = require('autoprefixer');
const postcss = require('gulp-postcss');
const terser = require('gulp-terser');
const cleancss = require('gulp-clean-css');
const javascriptObfuscator = require('gulp-javascript-obfuscator');
const plumber = require('gulp-plumber');
const removeSourcemaps = require('gulp-remove-sourcemaps');
const rollup = require('rollup');
const { nodeResolve } = require('@rollup/plugin-node-resolve');
const source = require('vinyl-source-stream');
const buffer = require('vinyl-buffer');

/**
 * Environment configuration flag
 * @type {boolean}
 */
// Production mode detection
const isProd = process.env.NODE_ENV === 'production';

/**
 * Build configuration object
 * @type {Object}
 * @property {string[]} entryPoints - JavaScript entry point files
 * @property {string} modulesDir - Directory containing JavaScript modules
 * @property {string} outputDir - Output directory for processed files
 */
// Build paths and entry points configuration
const config = {
  entryPoints: [
    'js/site.js',
    'js/businfo.js',
    'js/settings.js'
  ],
  modulesDir: 'js/modules',
  outputDir: '../wwwroot/js'
};

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
  let stream = src('src/styles/**/*.scss', { sourcemaps: !isProd })
    // Error handling to prevent watch task from breaking
    .pipe(plumber({
      errorHandler: function (err) {
        console.log(err);
        this.emit('end');
      }
    }))
    // Process SCSS
    .pipe(sass({
      outputStyle: 'compressed',
      includePaths: ['node_modules']
    }).on('error', sass.logError))
    // Apply vendor prefixes via autoprefixer
    .pipe(postcss([autoprefixer()]));
   
  // Strip sourcemaps in production mode
  if (isProd) {
    stream = stream.pipe(removeSourcemaps());
  }
 
  // Output to destination
  return stream.pipe(dest('../wwwroot/css/', { sourcemaps: isProd ? false : '.' }));
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
  const tasks = config.entryPoints.map(async entry => {
    // Bundle using Rollup
    const bundle = await rollup.rollup({
      input: entry,
      plugins: [
        nodeResolve()
      ]
    });

    // Extract filename from entry path
    const filename = entry.split('/').pop();
    
    // Generate bundle in IIFE format
    await bundle.write({
      file: `${config.outputDir}/${filename}`,
      format: 'iife',
      name: filename.replace('.js', ''),
      sourcemap: !isProd
    });

    if (isProd) {
      return src(`${config.outputDir}/${filename}`)
        .pipe(buffer())
        // Remove console logs and debugger statements
        .pipe(terser({
          compress: {
            drop_console: true,
            drop_debugger: true
          }
        }))
        // Apply obfuscation
        .pipe(javascriptObfuscator({
          compact: true,
          controlFlowFlattening: false,
          deadCodeInjection: false,
          debugProtection: false,
          disableConsoleOutput: true,
          identifierNamesGenerator: 'hexadecimal',
          renameGlobals: false,
          rotateStringArray: true,
          selfDefending: false,
          shuffleStringArray: true,
          splitStrings: true,
          splitStringsChunkLength: 5,
          stringArray: true,
          stringArrayEncoding: ['base64'],
          stringArrayThreshold: 0.5,
          transformObjectKeys: false,
          unicodeEscapeSequence: false
        }))
        .pipe(removeSourcemaps())
        .pipe(dest(config.outputDir));
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
  watch(['src/styles/**/*.scss'], buildStyles)
    .on('change', path => console.log(`SCSS: ${path} changed`))
    .on('error', err => console.log(`SCSS Error: ${err}`));

  // Watch JS files including modules directory
  watch([
    ...config.entryPoints,
    `${config.modulesDir}/**/*.js`
  ], minifyJs)
    .on('change', path => console.log(`JS: ${path} changed`))
    .on('error', err => console.log(`JS Error: ${err}`));
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
exports.build = parallel(buildStyles, minifyJs);
exports.default = series(
  parallel(buildStyles, minifyJs),
  watchTask
);