import React from "react";
import { AlbumDto, TrackDto } from "../client";
import styles from "../styles/AlbumInfo.module.css";
import { Badge, Image, Text } from "@mantine/core";
import { OpenAPI } from "../client";
import { formatSecondsToDateString, formatSecondsToMinutes } from "../utils";

type AlbumInfoProps = {
  album: AlbumDto;
};

export default function AlbumInfo({ album }: AlbumInfoProps) {
  if (album == null) {
    return <Text fz={32}>Cannot get album info...</Text>;
  }

  const getAlbumArtists = () => {
    if (album.artists?.length >= 4) {
      return "Various Artists";
    }
    return album.artists?.map((a) => a.name).join(", ");
  };

  const getGenre = () => {
    let uniqueGenres = Array.from(
      new Set(album.tracks?.map((a) => a.genre?.name))
    );
    if (uniqueGenres.length >= 4) {
      return "Various Genres";
    }
    return uniqueGenres.join(", ");
  };

  const getDuration = () => {
    let totalDurationInSeconds = album.tracks
      ?.map((t) => t.durationInSeconds)
      .reduce((a, b) => a + b);
    return formatSecondsToDateString(totalDurationInSeconds);
  };

  return (
    <div className={styles.wrapper}>
      <Image
        withPlaceholder
        width={200}
        height={200}
        src={`${OpenAPI.BASE}/api/repository/albums/${album.id}/artwork`}
      ></Image>
      <div className={styles.metadataWrapper}>
        <div className={styles.metadata}>
          <Text fw={700} fz={32}>
            {album.name}
          </Text>
          <Text fz={20}>{getAlbumArtists()}</Text>
        </div>
        <div className={styles.attributes}>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {album.releaseYear}
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {album.tracks?.length} tracks
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {getDuration()}
          </Text>
          <Text c={"dimmed"} fz={16} className={styles.attribute}>
            {getGenre()}
          </Text>
        </div>
      </div>
    </div>
  );
}
