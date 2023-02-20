import { Image, Text } from "@mantine/core";
import React from "react";
import { SimpleArtistDto } from "../../client/schemas";
import styles from "../../styles/Search.module.css";

type ArtistSearchItemProps = {
  artist: SimpleArtistDto;
};

export default function ArtistSearchItem({ artist }: ArtistSearchItemProps) {
  return (
    <div className={styles.artistItem}>
      <Image
        withPlaceholder
        alt={""}
        width={100}
        height={100}
        radius={100}
      ></Image>
      <Text lineClamp={2} fw={"bold"} fz={"sm"} style={{ textAlign: "center" }}>
        {artist.name}
      </Text>
    </div>
  );
}
