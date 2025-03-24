/**
 * Stream utilities to help with common stream issues
 */
const { Transform } = require("stream");
const { Readable, Writable } = require("stream");

/**
 * Creates a debugging transform stream
 * @param {string} name - Identifier for this stream in logs
 * @returns {Transform} A transform stream for debugging
 */
function createDebugStream(name) {
	return new Transform({
		objectMode: true,
		transform(file, enc, cb) {
			console.log(`[${name}] Processing file: ${file.path}`);
			this.push(file);
			cb();
		},
	})
		.on("pipe", () => console.log(`[${name}] Source piped in`))
		.on("unpipe", () => console.log(`[${name}] Source unpiped`))
		.on("error", (err) => console.error(`[${name}] Error:`, err))
		.on("end", () => console.log(`[${name}] Stream ended`))
		.on("finish", () => console.log(`[${name}] Stream finished`));
}

/**
 * Safely end a stream, preventing "write after end" errors
 * @param {Readable|Writable} stream - The stream to safely end
 */
function safelyEndStream(stream) {
	if (stream && typeof stream.end === "function" && !stream.destroyed) {
		try {
			stream.end();
		} catch (err) {
			console.error("Error ending stream:", err);
		}
	}
}

/**
 * Creates a buffering stream to prevent "write after end" errors
 * @returns {Transform} A transform stream with buffering capability
 */
function createBufferingStream() {
	const buffer = [];
	let ended = false;

	return new Transform({
		objectMode: true,
		transform(chunk, enc, cb) {
			if (ended) {
				return cb();
			}
			buffer.push(chunk);
			cb();
		},
		flush(cb) {
			ended = true;
			buffer.forEach((chunk) => this.push(chunk));
			buffer.length = 0;
			cb();
		},
	});
}

module.exports = {
	createDebugStream,
	safelyEndStream,
	createBufferingStream,
};
