import React from "react";
import AlbumListItem from "../common/album/AlbumListItem";
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
    return (
      <AlbumListItem
        album={album}
        key={album.id}
        artworkSize={150}
      ></AlbumListItem>
    );
  });
  return <div className={styles.listWrapper}>{albumItems}</div>;
}
