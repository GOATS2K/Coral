import React from "react";
import AlbumListItem from "./AlbumListItem";
import styles from "../styles/AlbumList.module.css";
import { SimpleAlbumDto } from "../client/schemas";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  if (albums == null) {
    return <p>No albums available.</p>;
  }
  let albumItems = albums.map((album) => {
    return <AlbumListItem album={album}></AlbumListItem>;
  });
  return <div className={styles.wrapper}>{albumItems}</div>;
}
