import { useMantineTheme } from "@mantine/core";
import dynamic from "next/dynamic";
import Head from "next/head";
import { usePlayerStore } from "../store";

type LayoutProps = {
  children: React.ReactNode;
};

const Player = dynamic(() => import("./player/Player"), {
  ssr: false,
});

export default function Layout({ children }: LayoutProps) {
  const selectedTrack = usePlayerStore((state) => state.selectedTrack);

  const titleText =
    selectedTrack.artist != null
      ? `${selectedTrack.artist.name} - ${selectedTrack.title} | Coral`
      : "Coral";

  const theme = useMantineTheme();
  const appBackground =
    theme.colorScheme === "dark" ? theme.colors.dark[7] : theme.white;
  const accentColor =
    theme.colorScheme === "dark" ? theme.colors.gray[7] : theme.colors.gray[8];
  const accentBackground =
    theme.colorScheme === "dark" ? theme.colors.gray[8] : theme.colors.gray[6];

  if (typeof window !== "undefined") {
    let root = document.documentElement;
    root.style.setProperty("--app-background", appBackground);
    root.style.setProperty("--accent", accentColor);
    root.style.setProperty("--accent-background", accentBackground);
  }

  return (
    <div className={"wrapper"}>
      <Head>
        <title>{titleText}</title>
      </Head>
      {children}
      <Player></Player>
    </div>
  );
}
