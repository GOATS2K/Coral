import { IconPlayerPlay, IconDisc, IconVinyl } from "@tabler/icons";
import { TrackDto } from "../../client/schemas";
import styles from "../../styles/PlaylistItem.module.css";
import { Text, UnstyledButton, useMantineTheme, Image } from "@mantine/core";
import { formatSecondsToSingleMinutes } from "../../utils";
import { useState } from "react";
import { usePlayerStore } from "../../store";
import { useAlbumArtwork } from "../../client/components";

type PlaylistItemProps = {
  track: TrackDto;
  onPlayback: () => void;
  displayArtwork?: boolean;
};

export function PlaylistItem({
  track,
  onPlayback,
  displayArtwork = false,
}: PlaylistItemProps) {
  const [trackHover, setTrackHover] = useState(false);
  const nowPlayingTrack = usePlayerStore((state) => state.selectedTrack);
  const playState = usePlayerStore((state) => state.playState);

  const { data: artwork } = useAlbumArtwork(
    {
      pathParams: {
        albumId: track.album.id,
      },
    },
    {
      enabled: displayArtwork,
    }
  );

  const setSelectedTrack = (track: TrackDto) => {
    // set the selected track
    usePlayerStore.setState({ selectedTrack: track });
    onPlayback();
  };

  const theme = useMantineTheme();
  const playButton = (
    <UnstyledButton onClick={() => setSelectedTrack(track)}>
      <IconPlayerPlay
        strokeWidth={1}
        size={24}
        style={{
          // center element
          marginTop: "4px",
        }}
      ></IconPlayerPlay>
    </UnstyledButton>
  );
  const albumArt = (
    <Image
      withPlaceholder
      src={artwork?.small}
      alt={`Album art of ${track.album}`}
    ></Image>
  );
  const defaultLeftSection = displayArtwork ? (
    albumArt
  ) : (
    <Text fz="lg">{track.trackNumber}</Text>
  );
  const spinningDisc = (
    <Text fz="lg" className={styles.spinningDisc}>
      <IconDisc strokeWidth={1.3} size={24}></IconDisc>
    </Text>
  );

  return (
    <div
      className={styles.gridContainer}
      key={track.id.toString()}
      id={track.id.toString()}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          setSelectedTrack(track);
        }
      }}
      onMouseEnter={() => setTrackHover(true)}
      onMouseLeave={() => setTrackHover(false)}
      onDoubleClick={() => setSelectedTrack(track)}
      style={
        nowPlayingTrack?.id === track.id
          ? {
              color: theme.colors.blue[4],
            }
          : {}
      }
    >
      <div className={styles.trackNumber}>
        {nowPlayingTrack?.id !== track.id && trackHover
          ? // show play button if track is hovered over
            // and it is not playing the current selected track
            playButton
          : // show the disc icon for the currently playing track
          nowPlayingTrack?.id === track.id && playState
          ? spinningDisc
          : // show left-section content
            defaultLeftSection}
      </div>
      <div className={styles.info}>
        <Text fz="sm" fw={500}>
          {track.title}
        </Text>
        <Text fz="xs">{track.artist.name}</Text>
      </div>
      <div className={styles.duration}>
        <Text fz="xs">
          {formatSecondsToSingleMinutes(track.durationInSeconds)}
        </Text>
      </div>
    </div>
  );
}
