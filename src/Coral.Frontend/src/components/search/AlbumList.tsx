import React, { useState } from "react";
import AlbumListItem from "../common/album/AlbumListItem";
import styles from "../../styles/Search.module.css";
import { SimpleAlbumDto } from "../../client/schemas";
import { Anchor, Pagination, Title } from "@mantine/core";
import Link from "next/link";
import { useSearchStore } from "../../store";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  const artworkSize = 150;

  if (albums == null) {
    return <p>No albums available.</p>;
  }

  let albumItems = albums.map((album) => {
    return (
      <AlbumListItem
        album={album}
        key={album.id}
        artworkSize={artworkSize}
      ></AlbumListItem>
    );
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
