import { Paper, Text } from "@mantine/core";
import React from "react";
import { TrackDto } from "../client";
import { formatSecondsToSingleMinutes } from '../utils';
import styles from "../styles/Playlist.module.css";

type PlaylistProps = {
  tracks: TrackDto[];
};

export default function Playlist({ tracks }: PlaylistProps) {
  const playlistItems = tracks.map((track, index) => {
    return (
      <div className={styles.gridContainer}>
        <div className={styles.trackNumber}>
          <Text fz="lg">{index + 1}</Text>
        </div>
        <div className={styles.info}>
          <Text fz="sm" fw={500}>{track.title}</Text>
          <Text fz="xs">{track.artist.name}</Text>
        </div>
        <div className={styles.duration}>
          <Text fz="xs">{formatSecondsToSingleMinutes(track.durationInSeconds)}</Text>
        </div>
      </div>
    )
  })


  return (
    <div>
      {playlistItems}
    </div>
  );
}
