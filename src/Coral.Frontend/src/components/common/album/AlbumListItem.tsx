import { Anchor, Image, Text, UnstyledButton, useMantineTheme } from "@mantine/core";
import { IconPlayerPlay } from "@tabler/icons-react";
import getConfig from "next/config";
import Link from "next/link";
import { useState } from "react";
import { fetchAlbum } from "../../../client/components";
import { SimpleAlbumDto } from "../../../client/schemas";
import { getAlbumArtists } from "../../../common/album";
import { Initializer, PlayerInitializationSource, usePlayerStore } from "../../../store";
import styles from "../../../styles/AlbumList.module.css";

type AlbumListItemProps = {
  album: SimpleAlbumDto;
  artworkSize: number;
};

export default function AlbumListItem({ album, artworkSize }: AlbumListItemProps) {
  const theme = useMantineTheme();
  const [onHover, setOnHover] = useState(false);
  const [playButtonOnHover, setPlayButtonOnHover] = useState(false);
  const onPlayClick = async (albumToFetch: SimpleAlbumDto) => {
    // get album
    const album = await fetchAlbum({
      pathParams: {
        albumId: albumToFetch.id,
      },
    });
    const initializer = {
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
      style={{ maxWidth: `${artworkSize}px` }}
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
            width={artworkSize}
            height={artworkSize}
            src={`${getConfig().publicRuntimeConfig.apiBaseUrl}${album?.artworks.medium}`}
          />
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
                <IconPlayerPlay className={styles.playButton} />
              </div>
            </UnstyledButton>
          )}
        </div>
      </div>
      <div className={styles.metadataContainer}>
        <Link className={styles.link} passHref href={`/albums/${album.id}`}>
          <Anchor className={styles.link} component="a" lineClamp={2} fz="md" fw="bold">
            {album.name}
          </Anchor>
        </Link>
        <Text lineClamp={1} fz="sm" fw="light">
          {getAlbumArtists(album)}
        </Text>
      </div>
      <div>
        <Text fz="xs" fw="lighter">{album.releaseYear}</Text>
      </div>
    </div>
  );
}
