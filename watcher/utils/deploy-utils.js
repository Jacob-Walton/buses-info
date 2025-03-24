/**
 * Deployment utilities for S3 and CloudFront
 */
const { CloudFront } = require("@aws-sdk/client-cloudfront");
const { src } = require("gulp");
const plumber = require("gulp-plumber");
const merge = require("merge-stream");
const config = require("../config/build-config");
const { createDebugStream } = require("./stream-utils");

/**
 * Creates an S3 uploader configured with AWS credentials
 * @returns {Function} Configured S3 upload function
 */
function createS3Uploader() {
	const s3 = require("gulp-s3-upload")({
		accessKeyId: process.env.AWS_ACCESS_KEY_ID,
		secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
	});

	return s3;
}

/**
 * Uploads files to an S3 bucket
 * @param {Function} cb - Callback function
 * @returns {Stream} Combined upload stream
 */
function uploadToS3(cb) {
	if (!config.isProd) {
		console.log("Skipping S3 upload in development mode");
		return cb();
	}

	try {
		const s3 = createS3Uploader();
		let completed = 0;
		const totalUploads = 2; // JS and CSS uploads

		const handleCompletion = () => {
			completed++;
			if (completed === totalUploads) {
				console.log("All uploads complete");
				cb();
			}
		};

		// Upload JavaScript files
		const jsUpload = src(`${config.outputDir}/js/**/*.js`, {
			base: config.outputDir,
		})
			.pipe(createDebugStream("JS-Upload"))
			.pipe(
				plumber({
					errorHandler: function (err) {
						console.error("JS upload error:", err);
						this.emit("end");
					},
				}),
			)
			.pipe(
				s3({
					Bucket: config.deployment.s3.bucket,
					ACL: "public-read",
					keyTransform: function (relative_filename) {
						return relative_filename;
					},
				}),
			)
			.on("end", handleCompletion);

		// Upload CSS files
		const cssUpload = src(`${config.outputDir}/css/**/*.css`, {
			base: config.outputDir,
		})
			.pipe(createDebugStream("CSS-Upload"))
			.pipe(
				plumber({
					errorHandler: function (err) {
						console.error("CSS upload error:", err);
						this.emit("end");
					},
				}),
			)
			.pipe(
				s3({
					Bucket: config.deployment.s3.bucket,
					ACL: "public-read",
					keyTransform: function (relative_filename) {
						return relative_filename;
					},
				}),
			)
			.on("end", handleCompletion);

		return merge(jsUpload, cssUpload).on("error", function (err) {
			console.error("Upload error:", err);
			cb(err);
		});
	} catch (err) {
		console.error("S3 upload function error:", err);
		cb(err);
	}
}

/**
 * Invalidates CloudFront cache
 * @param {Function} cb - Callback function
 */
function invalidateCache(cb) {
	if (!config.isProd) {
		console.log("Skipping CloudFront invalidation in development mode");
		return cb();
	}

	const cloudfront = new CloudFront({
		region: config.deployment.s3.region,
		credentials: {
			accessKeyId: process.env.AWS_ACCESS_KEY_ID,
			secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
		},
	});

	const params = {
		DistributionId: config.deployment.cloudfront.distributionId,
		InvalidationBatch: {
			CallerReference: Date.now().toString(),
			Paths: {
				Quantity: 2,
				Items: ["/js/*", "/css/*"],
			},
		},
	};

	cloudfront
		.createInvalidation(params)
		.then(() => {
			console.log("CloudFront cache invalidation initiated");
			cb();
		})
		.catch((err) => {
			console.error("CloudFront invalidation error:", err);
			cb(err);
		});
}

module.exports = {
	uploadToS3,
	invalidateCache,
};
