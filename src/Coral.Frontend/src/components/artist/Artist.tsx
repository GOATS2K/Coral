import React from "react";
import { ArtistDto } from "../../client/schemas";
import AlbumList from "./AlbumList";
import ArtistBanner from "./ArtistBanner";
import styles from "../../styles/Artist.module.css";
import { Title } from "@mantine/core";

type ArtistProps = {
  artist?: ArtistDto;
};

export default function Artist({ artist }: ArtistProps) {
  const singles = artist?.releases.filter((a) => a.type === "Single");
  const eps = artist?.releases.filter((a) => a.type === "EP");
  const albums = artist?.releases.filter((a) => a.type === "Album");
  const miniAlbums = artist?.releases.filter((a) => a.type === "MiniAlbum");
  return (
    <div>
      <ArtistBanner artist={artist}></ArtistBanner>
      <div className={styles.pageWrapper}>
        {albums?.length != 0 && (
          <AlbumList albums={albums} title={"Releases"}></AlbumList>
        )}
        {miniAlbums?.length != 0 && (
          <AlbumList albums={miniAlbums} title={"Mini Albums"}></AlbumList>
        )}
        {eps?.length != 0 && <AlbumList albums={eps} title={"EPs"}></AlbumList>}
        {singles?.length != 0 && (
          <AlbumList albums={singles} title={"Singles"}></AlbumList>
        )}

        {artist?.featuredIn.length != 0 && (
          <AlbumList
            albums={artist?.featuredIn}
            title={"Featured In"}
          ></AlbumList>
        )}

        {artist?.remixerIn.length != 0 && (
          <AlbumList
            albums={artist?.remixerIn}
            title={"Remixer In"}
          ></AlbumList>
        )}
        {artist?.inCompilation.length != 0 && (
          <AlbumList
            albums={artist?.inCompilation}
            title={"Appears In"}
          ></AlbumList>
        )}
      </div>
    </div>
  );
}
