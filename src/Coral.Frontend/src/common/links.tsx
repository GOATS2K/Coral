import { Anchor } from "@mantine/core";
import Link from "next/link";
import { ReactNode } from "react";
import { TrackDto } from "../client/schemas";

export const getLinksForArtist = (track: TrackDto): ReactNode => {
  const links = track.artists.map((a) => {
    if (a.role === "Main") {
      return (
        <Link
          key={a.id}
          className="link wrappedPlayerArtist"
          href={`/artists/${a.id}`}
        >
          <Anchor fz={"sm"} className="link wrappedPlayerArtist">
            {a.name}
          </Anchor>
        </Link>
      );
    }
  });
  return <div className="wrappedLinks">{links}</div>;
};
