import { Title } from "@mantine/core";
import { SimpleAlbumDto } from "../../client/schemas";
import styles from "../../styles/Search.module.css";
import AlbumListItem from "../common/album/AlbumListItem";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  const artworkSize = 150;

  if (albums == null) {
    return <p>No albums available.</p>;
  }

  const albumItems = albums.map((album) => {
    return <AlbumListItem album={album} key={album.id} artworkSize={artworkSize} />;
  });
  return (
    <div>
      <Title order={3} className={styles.title}>
        Albums
      </Title>
      <div className={styles.albumGrid}>{albumItems}</div>
    </div>
  );
}
