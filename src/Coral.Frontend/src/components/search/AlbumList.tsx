import React, { useState } from "react";
import AlbumListItem from "../common/album/AlbumListItem";
import styles from "../../styles/Search.module.css";
import { SimpleAlbumDto } from "../../client/schemas";
import { Pagination, Title } from "@mantine/core";

export type AlbumListProps = {
  albums?: SimpleAlbumDto[];
};

export default function AlbumList({ albums }: AlbumListProps) {
  let artworkSize = 150;
  let pageSize = 12;
  let pages = albums?.length != null ? albums?.length / pageSize : 1;
  let [currentPage, setCurrentPage] = useState(1);

  if (albums == null) {
    return <p>No albums available.</p>;
  }
  let albumItems = albums
    .slice(pageSize * currentPage, pageSize * currentPage + pageSize)
    .map((album) => {
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
      <Title order={1} className={styles.title}>
        Albums
      </Title>
      <div className={styles.albumGrid}>{albumItems}</div>
      <Pagination
        total={pages}
        page={currentPage}
        onChange={setCurrentPage}
      ></Pagination>
    </div>
  );
}
