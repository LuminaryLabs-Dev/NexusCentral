/** @type {import('next').NextConfig} */
const apiOrigin = process.env.NEXUS_WEB_LAB_API_ORIGIN || "http://127.0.0.1:3016";

const nextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiOrigin}/api/:path*`,
      },
      {
        source: "/health",
        destination: `${apiOrigin}/health`,
      },
    ];
  },
};

module.exports = nextConfig;
