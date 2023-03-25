import { Title } from "@mantine/core";
import { SimpleAlbumDto } from "../../client/schemas";
import styles from "../../styles/Artist.module.css";
import AlbumListItem from "../common/album/AlbumListItem";

type ArtistAlbumListProps = {
  albums?: SimpleAlbumDto[];
  title: string;
};

export default function AlbumList({ albums, title }: ArtistAlbumListProps) {
  const albumListItems = albums?.map((a) => (
    <AlbumListItem key={a.id} album={a} artworkSize={200} />
  ));
  return (
    <div>
      <Title order={2} mb="sm">
        {title}
      </Title>
      <div className={styles.albumWrapper}>{albumListItems}</div>
    </div>
  );
}
