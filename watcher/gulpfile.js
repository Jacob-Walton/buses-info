const { src, dest, watch, series, parallel } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const autoprefixer = require('autoprefixer');
const postcss = require('gulp-postcss');
const terser = require('gulp-terser');
const cleancss = require('gulp-clean-css');

// Compile SASS files and add vendor prefixes
function buildStyles() {
  return src('src/styles/**/*.scss')
    .pipe(sass().on('error', sass.logError))
    .pipe(postcss([autoprefixer()]))
    .pipe(cleancss())
    .pipe(dest('../wwwroot/css/'));
}

// Minify JavaScript
function minifyJs() {
  return src('js/**/*.js')
    .pipe(terser())
    .pipe(dest('../wwwroot/js/'));
}

// Watch task
function watchTask() {
  watch(['src/styles/**/*.scss'], buildStyles);
  watch(['js/**/*.js'], minifyJs);
}

// Exports
exports.buildStyles = buildStyles;
exports.minifyJs = minifyJs;
exports.watchTask = watchTask;
exports.default = series(buildStyles, minifyJs, watchTask);
