import { Pagination, Title } from "@mantine/core";
import React, { useState } from "react";
import { usePaginatedAlbums } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import AlbumList from "../../components/album/AlbumList";
import styles from "../../styles/AlbumList.module.css";

export default function Albums() {
  const [currentPage, setCurrentPage] = useState(1);
  const limit = 50;
  const offset = currentPage * limit;
  // get list of albums
  const { data, isLoading, error } = usePaginatedAlbums({
    queryParams: {
      limit: limit,
      offset: offset,
    },
  });

  const pages = Number(data?.totalRecords) / limit;

  if (isLoading) {
    return CenteredLoader();
  }

  return (
    <div className={styles.wrapper}>
      <Title order={1} className={styles.title}>
        Albums
      </Title>
      <AlbumList albums={data?.data}></AlbumList>
      <div className={styles.pagination}>
        <Pagination
          size={"lg"}
          total={pages}
          page={currentPage}
          onChange={setCurrentPage}
        ></Pagination>
      </div>
    </div>
  );
}
