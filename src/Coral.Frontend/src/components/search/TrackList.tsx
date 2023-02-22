import { Title } from "@mantine/core";
import React from "react";
import { TrackDto } from "../../client/schemas";
import { Initializer, PlayerInitializationSource } from "../../store";
import styles from "../../styles/Search.module.css";
import Playlist from "../playlist/Playlist";

type TrackListProps = {
  tracks?: TrackDto[];
};

export default function TrackList({ tracks }: TrackListProps) {
  const initializer = {
    id: "",
    source: PlayerInitializationSource.Search,
  } as Initializer;
  return (
    <div>
      <Title order={3} className={styles.title}>
        Tracks
      </Title>
      <Playlist
        displayArtwork
        initializer={initializer}
        tracks={tracks}
      ></Playlist>
    </div>
  );
}
