import React from "react";
import { SimpleArtistDto } from "../../client/schemas";
import ArtistSearchItem from "./ArtistSearchItem";
import styles from "../../styles/Search.module.css";
import { Title } from "@mantine/core";

type ArtistListProps = {
  artists?: SimpleArtistDto[];
};

export default function ArtistList({ artists }: ArtistListProps) {
  const artistList = artists?.map((a) => (
    <ArtistSearchItem artist={a} key={a.id}></ArtistSearchItem>
  ));
  return (
    <div className={styles.artistComponentWrapper}>
      <Title order={1} className={styles.title}>
        Artists
      </Title>
      <div className={styles.artistWrapper}>{artistList}</div>
    </div>
  );
}
