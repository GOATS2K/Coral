const { defineConfig } = require('eslint/config');
const expoConfig = require('eslint-config-expo/flat');
const reactCompiler = require('eslint-plugin-react-compiler');

module.exports = defineConfig([
  expoConfig,
  reactCompiler.configs.recommended,
  {
    ignores: [
      'node_modules/**',
      '.expo/**',
      'dist/**',
      'build/**',
      '**/generated/**',
      'lib/client/**', // Generated API client code
      'lib/vendor/hls.js/**', // Vendored hls.js library
    ],
  },
]);
