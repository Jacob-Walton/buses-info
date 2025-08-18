import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || ''
  },
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'cdn.jsdelivr.net',
        port: '',
        pathname: '/gh/devicons/devicon@latest/icons/**'
      },
      {
        protocol: 'https',
        hostname: 'd1tl6qv7xwsvxx.cloudfront.net',
        port: '',
        pathname: '/assets/**'
      }
    ]
  }
};

export default nextConfig;
