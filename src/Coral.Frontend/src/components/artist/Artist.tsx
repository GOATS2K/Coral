import { ArtistDto } from "../../client/schemas";
import styles from "../../styles/Artist.module.css";
import AlbumList from "./AlbumList";
import ArtistBanner from "./ArtistBanner";

type ArtistProps = {
  artist?: ArtistDto;
};

export default function Artist({ artist }: ArtistProps) {
  const singles = artist?.releases.filter((a) => a.type === "Single");
  const eps = artist?.releases.filter((a) => a.type === "EP");
  const albums = artist?.releases.filter((a) => a.type === "Album");
  const miniAlbums = artist?.releases.filter((a) => a.type === "MiniAlbum");
  const untagged = artist?.releases.filter((a) => a.type == null);

  return (
    <div>
      <ArtistBanner artist={artist} />
      <div className={styles.pageWrapper}>
        {albums?.length != 0 && <AlbumList albums={albums} title="Releases" />}
        {miniAlbums?.length != 0 && <AlbumList albums={miniAlbums} title="Mini Albums" />}
        {eps?.length != 0 && <AlbumList albums={eps} title="EPs" />}
        {singles?.length != 0 && <AlbumList albums={singles} title="Singles" />}

        {artist?.featuredIn.length != 0 && (
          <AlbumList albums={artist?.featuredIn} title="Featured In" />
        )}

        {artist?.remixerIn.length != 0 && (
          <AlbumList albums={artist?.remixerIn} title="Remixer In" />
        )}
        {artist?.inCompilation?.length != 0 && (
          <AlbumList albums={artist?.inCompilation} title="Appears In" />
        )}
        {untagged?.length != 0 && <AlbumList albums={untagged} title="Unknown Type" />}
      </div>
    </div>
  );
}
