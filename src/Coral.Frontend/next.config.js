const process = require("process")

const getApiBaseUrl = () => {
  if (process.env.NODE_ENV === "development") {
    return process.env.ASPNETCORE_HTTPS_PORT != null
      ? `https://localhost:${process.env.ASPNETCORE_HTTPS_PORT}`
      : process.env.ASPNETCORE_URLS != null
      ? process.env.ASPNETCORE_URLS.split(";")[0]
      : "http://localhost:5031";
  }
  return "";
};

module.exports = {
  publicRuntimeConfig: {
    apiBaseUrl: getApiBaseUrl()
  }
}