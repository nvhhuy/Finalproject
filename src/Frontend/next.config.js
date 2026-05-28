/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Required for deploy/docker/frontend/Dockerfile (standalone server bundle).
  output: "standalone",
};

module.exports = nextConfig;
