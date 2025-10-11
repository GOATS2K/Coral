// Custom Metro transformer to patch hls.js build-time constants
// This is needed because hls.js is vendored as a git submodule
// and we can't modify its source directly
//
// Based on Expo's babel-transformer API: must accept { src, filename, options, plugins }
// and return { ast, metadata } (or { ast: null })

const upstreamTransformer = require('@expo/metro-config/babel-transformer');

const transform = ({ src, filename, options, plugins }) => {
  // Replace hls.js build-time constants in vendored hls.js files
  if (filename.includes('lib/vendor/hls.js') || filename.includes('lib\\vendor\\hls.js')) {
    // Add constant definitions at the top of each hls.js file that uses them
    if (src.includes('__USE_VARIABLE_SUBSTITUTION__') || src.includes('__USE_EME_DRM__')) {
      const constantsPrefix = `// Build-time constants (injected by Metro transformer)
const __USE_VARIABLE_SUBSTITUTION__ = false;
const __USE_EME_DRM__ = false;
`;
      src = constantsPrefix + src;
    }
  }

  // Use upstream transformer for the actual transformation
  // It will return { ast, metadata } as required by Metro
  return upstreamTransformer.transform({ src, filename, options, plugins });
};

module.exports = {
  transform,
};
