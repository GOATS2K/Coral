import { SimpleAlbumDto } from "../../client/schemas";
import styles from "../../styles/AlbumList.module.css";
import AlbumListItem from "../common/album/AlbumListItem";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  if (albums == null) {
    return <p>No albums available.</p>;
  }
  const albumItems = albums.map((album) => {
    return <AlbumListItem album={album} key={album.id} artworkSize={150} />;
  });
  return <div className={styles.listWrapper}>{albumItems}</div>;
}
