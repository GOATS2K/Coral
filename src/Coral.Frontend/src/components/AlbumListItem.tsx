import React, { useState } from "react";
import {
  Anchor,
  Image,
  Text,
  UnstyledButton,
  useMantineTheme,
} from "@mantine/core";
import { SimpleAlbumDto } from "../client/schemas";
import styles from "../styles/AlbumList.module.css";
import { getAlbumArtists } from "../common/album";
import getConfig from "next/config";
import Link from "next/link";
import { IconPlayerPlay } from "@tabler/icons";
import { fetchAlbum } from "../client/components";
import Album from "../pages/albums/[id]";
import {
  Initializer,
  PlayerInitializationSource,
  usePlayerStore,
} from "../store";

type AlbumListItemProps = {
  album: SimpleAlbumDto;
};

export default function AlbumListItem({ album }: AlbumListItemProps) {
  const baseUrl = getConfig().publicRuntimeConfig.apiBaseUrl;
  const theme = useMantineTheme();
  const [onHover, setOnHover] = useState(false);
  const [playButtonOnHover, setPlayButtonOnHover] = useState(false);
  const onPlayClick = async (albumToFetch: SimpleAlbumDto) => {
    // get album
    let album = await fetchAlbum({
      pathParams: {
        albumId: albumToFetch.id,
      },
    });
    let initializer = {
      id: albumToFetch.id,
      source: PlayerInitializationSource.Album,
    } as Initializer;
    // initialize player
    usePlayerStore.setState({
      tracks: album.tracks,
      initializer: initializer,
      selectedTrack: album.tracks[0],
    });
  };
  const imageStyle = onHover ? { filter: "brightness(35%)" } : {};
  const playButtonStyle = playButtonOnHover
    ? { backgroundColor: theme.colors.blue[9] }
    : { backgroundColor: theme.colors.blue[5] };
  return (
    <div
      className={styles.item}
      onMouseEnter={() => setOnHover(true)}
      onMouseLeave={() => setOnHover(false)}
    >
      <div className={styles.imageContainer}>
        <Link href={`/albums/${album.id}`} key={album.id.toString()}>
          <Image
            alt={`Album cover of ${album.name}`}
            className={styles.image}
            style={imageStyle}
            withPlaceholder
            width={150}
            height={150}
            src={`${baseUrl}/api/library/albums/${album.id}/artwork`}
          ></Image>
        </Link>
        <div className={styles.playButtonOnImage}>
          {onHover && (
            <UnstyledButton onClick={() => onPlayClick(album)}>
              <div
                className={styles.circle}
                style={playButtonStyle}
                onMouseEnter={() => setPlayButtonOnHover(true)}
                onMouseLeave={() => setPlayButtonOnHover(false)}
              >
                <IconPlayerPlay className={styles.playButton}></IconPlayerPlay>
              </div>
            </UnstyledButton>
          )}
        </div>
      </div>
      <div>
        <Link className={styles.link} passHref href={`/albums/${album.id}`}>
          <Anchor
            className={styles.link}
            component="a"
            lineClamp={2}
            fz={"md"}
            fw={"bold"}
          >
            {album.name}
          </Anchor>
        </Link>
        <Text lineClamp={2} fz={"sm"} fw={"light"}>
          {getAlbumArtists(album)}
        </Text>
      </div>
    </div>
  );
}
