import React from "react";
import { AlbumDto } from "../client/schemas";
import styles from "../styles/AlbumInfo.module.css";
import { Image, Text } from "@mantine/core";
import {
  getAlbumArtists,
  getAlbumDuration,
  getAlbumGenre,
} from "../common/album";
import getConfig from "next/config";

type AlbumInfoProps = {
  album?: AlbumDto;
};

export default function AlbumInfo({ album }: AlbumInfoProps) {
  if (album == null) {
    return <Text fz={32}>Cannot get album info...</Text>;
  }

  return (
    <div className={styles.wrapper}>
      <Image
        withPlaceholder
        width={200}
        height={200}
        src={`${
          getConfig().publicRuntimeConfig.apiBaseUrl
        }/api/library/albums/${album.id}/artwork`}
      ></Image>
      <div className={styles.metadataWrapper}>
        <div className={styles.metadata}>
          <Text fw={700} fz={32}>
            {album.name}
          </Text>
          <Text fz={20}>{getAlbumArtists(album)}</Text>
        </div>
        <div className={styles.attributes}>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {album.releaseYear}
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {album.tracks?.length} tracks
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {getAlbumDuration(album)}
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {getAlbumGenre(album)}
          </Text>
        </div>
      </div>
    </div>
  );
}
