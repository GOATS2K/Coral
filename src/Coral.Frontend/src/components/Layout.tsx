import dynamic from "next/dynamic";

type LayoutProps = {
  children: React.ReactNode;
};

const Player = dynamic(() => import("../components/Player"), {
  ssr: false,
});

export default function Layout({ children }: LayoutProps) {
  return (
    <div>
      {children}
      <Player></Player>
    </div>
  );
}
