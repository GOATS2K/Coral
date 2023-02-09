import React from "react";
import { Image, Text } from "@mantine/core";
import { AlbumDto, ArtistDto } from "../client/schemas";
import styles from "../styles/AlbumListItem.module.css";
import { getAlbumArtists } from "../common/album";

type AlbumListItemProps = {
  album: AlbumDto;
};

export default function AlbumListItem() {
  let album = {
    artists: [
      {
        id: 1,
        name: "Test Artist",
      },
    ],
    name: "The Greatest Album of All Time",
    coverPresent: true,
    releaseYear: 2023,
    genres: [{ id: 1, name: "Drum & Bass" }],
    tracks: [{ id: 1 }, { id: 2 }, { id: 3 }],
    id: 1,
  } as AlbumDto;

  return (
    <div className={styles.item}>
      <Image withPlaceholder width={150} height={150}></Image>
      <div>
        <Text lineClamp={2} fw={"bold"}>
          {album.name}
        </Text>
        <Text lineClamp={2}>{getAlbumArtists(album)}</Text>
      </div>
    </div>
  );
}
