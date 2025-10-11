const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const path = require('path');

const config = getDefaultConfig(__dirname);
config.resolver.unstable_enablePackageExports = false;

// Custom resolver for @svta/common-media-library deep imports
const originalResolveRequest = config.resolver.resolveRequest;
config.resolver.resolveRequest = (context, moduleName, platform) => {
  // Handle @svta/common-media-library deep imports
  if (moduleName.startsWith('@svta/common-media-library/')) {
    const subpath = moduleName.replace('@svta/common-media-library/', '');
    const resolvedPath = path.join(
      __dirname,
      'node_modules/@svta/common-media-library/dist',
      subpath + '.js'
    );

    return {
      type: 'sourceFile',
      filePath: resolvedPath,
    };
  }

  // Fall back to default resolution
  if (originalResolveRequest) {
    return originalResolveRequest(context, moduleName, platform);
  }
  return context.resolveRequest(context, moduleName, platform);
};

// Custom transformer to inject hls.js build-time constants
// (hls.js is vendored as a git submodule, so we can't modify its source)
config.transformer.babelTransformerPath = require.resolve('./metro-hls-transformer.js');

module.exports = withNativeWind(config, { input: './global.css', inlineRem: 16 });
