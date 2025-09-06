# Build stage
FROM node:22-alpine as builder

WORKDIR /app

# Copy package files
COPY package*.json ./
COPY yarn.lock ./

# Install dependencies (including dev dependencies for build)
RUN yarn install --frozen-lockfile

# Copy source code
COPY . .

# Build the application (skip linting during build)
RUN ESLINT_NO_DEV_ERRORS=true yarn build

# Runtime stage
FROM node:22-alpine AS runtime

WORKDIR /app

# Copy package files and install production dependencies
COPY package*.json ./
COPY yarn.lock ./
RUN yarn install --frozen-lockfile --production

# Install TypeScript for next.config.ts support
RUN yarn add typescript

# Copy built application from builder stage
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/next.config.ts ./next.config.ts

# Create non-root user
RUN addgroup -g 1001 -S nodejs
RUN adduser -S nextjs -u 1001
RUN chown -R nextjs:nodejs /app

USER nextjs

# Expose port
EXPOSE 3000

# Ensure Next.js listens on all interfaces
ENV HOST=0.0.0.0

# Start Next.js server
CMD ["yarn", "start"]