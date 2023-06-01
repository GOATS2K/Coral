# Changelog

## [0.2.0](https://github.com/GOATS2K/Coral/compare/v0.1.0...v0.2.0) (2023-06-01)


### Features

* **search:** setup pagination of search results ([2163936](https://github.com/GOATS2K/Coral/commit/2163936a5d352ac145d0717e5aef1a7422a92ec5))
* **search:** support paginated search in frontend and remember last query ([1084e3d](https://github.com/GOATS2K/Coral/commit/1084e3d77af2766775f4cc301854a8b4b45beed9))


### Bug Fixes

* **album:** include artwork url in album response ([dfa579b](https://github.com/GOATS2K/Coral/commit/dfa579b88d3e8f78d7c9d7c990e1c9ec608f1c6c))
* **frontend:** use album art from server response, instead of fetching it manually ([092a8f3](https://github.com/GOATS2K/Coral/commit/092a8f3a066c9f3c39ebfc32039fd270e477d295))
* **search:** improve search performance by comparing current search value and debounced value after rendering and memoizing search result ([f62eb23](https://github.com/GOATS2K/Coral/commit/f62eb2385a5055b10cbe4b5bc25e7237eb9a33c1))