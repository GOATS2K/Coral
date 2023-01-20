import { AppProps } from "next/app";
import Head from "next/head";
import { MantineProvider } from "@mantine/core";
import { OpenAPI } from "../client";
import "../styles/global.css";

if (process.env.NODE_ENV === "development") {
  OpenAPI.BASE = "https://localhost:7031";
}

export default function App(props: AppProps) {
  const { Component, pageProps } = props;

  return (
    <>
      <Head>
        <title>Coral</title>
        <meta
          name="viewport"
          content="minimum-scale=1, initial-scale=1, width=device-width"
        />
      </Head>

      <div>
        <MantineProvider
          withGlobalStyles
          withNormalizeCSS
          theme={{
            /** Put your mantine theme override here */
            colorScheme: "dark",
          }}
        >
          <Component {...pageProps} />
        </MantineProvider>
      </div>
    </>
  );
}