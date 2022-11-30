import { Paper, Text } from "@mantine/core";
import React, { useState } from "react";
import { TrackDto } from "../client";
import { formatSecondsToSingleMinutes } from '../utils';
import styles from "../styles/Playlist.module.css";
import {IconPlayerPlay} from '@tabler/icons';
import { PlaylistItem } from "./PlaylistItem";

type PlaylistProps = {
  tracks: TrackDto[];
};

type HoverInfo = {
  id: number,
  hover: boolean
};

export default function Playlist({ tracks }: PlaylistProps) {
  const playlistItems = tracks
  .sort((a, b) => a.trackNumber - b.trackNumber)
  .map((track) => {
    return (
      <PlaylistItem track={track}></PlaylistItem>
    )
  })


  return (
    <div>
      {playlistItems}
    </div>
  );
}
