import React from "react";
import AlbumListItem from "./AlbumListItem";
import styles from "../../styles/AlbumList.module.css";
import { SimpleAlbumDto } from "../../client/schemas";
import { Title } from "@mantine/core";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  if (albums == null) {
    return <p>No albums available.</p>;
  }
  let albumItems = albums.map((album) => {
    return <AlbumListItem album={album} key={album.id}></AlbumListItem>;
  });
  return (
    <div className={styles.wrapper}>
      <Title order={1} className={styles.title}>
        Albums
      </Title>
      <div className={styles.listWrapper}>{albumItems}</div>
    </div>
  );
}
