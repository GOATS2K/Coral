import { Container, Paper, Text } from "@mantine/core";
import React, { useState } from "react";
import { TrackDto } from "../client";
import { formatSecondsToSingleMinutes } from "../utils";
import styles from "../styles/Playlist.module.css";
import { IconPlayerPlay } from "@tabler/icons";
import { PlaylistItem } from "./PlaylistItem";

type PlaylistProps = {
  tracks: TrackDto[];
};

export default function Playlist({ tracks }: PlaylistProps) {
  if (tracks == null) {
    return <p>No tracks in playlist</p>;
  }
  const playlistItems = tracks
    .sort((a, b) => a.trackNumber - b.trackNumber)
    .map((track) => {
      return <PlaylistItem track={track}></PlaylistItem>;
    });

  return <div className={styles.wrapper}>{playlistItems}</div>;
}
