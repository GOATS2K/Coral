import dynamic from "next/dynamic";
import Head from "next/head";
import { usePlayerStore } from "../store";

type LayoutProps = {
  children: React.ReactNode;
};

const Player = dynamic(() => import("../components/Player"), {
  ssr: false,
});

export default function Layout({ children }: LayoutProps) {
  const selectedTrack = usePlayerStore((state) => state.selectedTrack);

  const titleText =
    selectedTrack.artist != null
      ? `${selectedTrack.artist.name} - ${selectedTrack.title} | Coral`
      : "Coral";

  return (
    <div>
      <Head>
        <title>{titleText}</title>
      </Head>
      {children}
      <Player></Player>
    </div>
  );
}
