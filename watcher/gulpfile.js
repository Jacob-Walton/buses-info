const { src, dest, watch, series, parallel } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const autoprefixer = require('autoprefixer');
const postcss = require('gulp-postcss');
const terser = require('@rollup/plugin-terser');
const cleancss = require('gulp-clean-css');
const javascriptObfuscator = require('gulp-javascript-obfuscator');
const plumber = require('gulp-plumber');
const removeSourcemaps = require('gulp-remove-sourcemaps');
const rollup = require('gulp-better-rollup');
const { nodeResolve } = require('@rollup/plugin-node-resolve'); // Changed this line

const isProd = process.env.NODE_ENV === 'production';

const config = {
  entryPoints: [
    'js/site.js',
    'js/businfo.js'
  ],
  modulesDir: 'js/modules',
  outputDir: '../wwwroot/js'
};

function buildStyles() {
  let stream = src('src/styles/**/*.scss', { sourcemaps: !isProd })
    .pipe(plumber({
      errorHandler: function (err) {
        console.log(err);
        this.emit('end');
      }
    }))
    .pipe(sass({
      outputStyle: 'compressed',
      includePaths: ['node_modules']
    }).on('error', sass.logError))
    .pipe(postcss([autoprefixer()]))
    .pipe(cleancss({
      level: {
        1: {
          specialComments: 0,
          removeEmpty: true,
          removeWhitespace: true
        }
      }
    }));
    
  if (isProd) {
    stream = stream.pipe(removeSourcemaps());
  }
  
  return stream.pipe(dest('../wwwroot/css/', { sourcemaps: isProd ? false : '.' }));
}

function minifyJs() {
  const tasks = config.entryPoints.map(entry => {
    let stream = src(entry, { sourcemaps: !isProd })
      .pipe(plumber({
        errorHandler: function (err) {
          console.log(err);
          this.emit('end');
        }
      }))
      .pipe(rollup(
        {
          input: entry,
          plugins: [
            nodeResolve(),
            isProd && terser()
          ].filter(Boolean)
        },
        {
          format: 'iife',
          name: 'app',
          file: entry.split('/').pop()
        }
      ));

    if (isProd) {
      stream = stream.pipe(javascriptObfuscator({
        compact: true,
        controlFlowFlattening: false,
        deadCodeInjection: false,
        debugProtection: false,
        disableConsoleOutput: false,
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
      .pipe(removeSourcemaps());
    }

    return stream.pipe(dest(config.outputDir, { sourcemaps: isProd ? false : '.' }));
  });

  return Promise.all(tasks);
}

function watchTask() {
  watch(['src/styles/**/*.scss'], buildStyles)
    .on('change', path => console.log(`SCSS: ${path} changed`))
    .on('error', err => console.log(`SCSS Error: ${err}`));

  watch([
    ...config.entryPoints,
    `${config.modulesDir}/**/*.js`
  ], minifyJs)
    .on('change', path => console.log(`JS: ${path} changed`))
    .on('error', err => console.log(`JS Error: ${err}`));
}

exports.styles = buildStyles;
exports.scripts = minifyJs;
exports.watch = watchTask;
exports.build = parallel(buildStyles, minifyJs);
exports.default = series(
  parallel(buildStyles, minifyJs),
  watchTask
);
