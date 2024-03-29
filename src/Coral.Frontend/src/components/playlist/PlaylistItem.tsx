import { Image, Text, UnstyledButton, useMantineTheme } from "@mantine/core";
import { IconDisc, IconPlayerPlay } from "@tabler/icons-react";
import getConfig from "next/config";
import { useState } from "react";
import { TrackDto } from "../../client/schemas";
import { getTrackArtists } from "../../common/album";
import { usePlayerStore } from "../../store";
import styles from "../../styles/PlaylistItem.module.css";
import { formatSecondsToSingleMinutes } from "../../utils";

type PlaylistItemProps = {
  track: TrackDto;
  onPlayback: () => void;
  displayArtwork?: boolean;
};

export function PlaylistItem({ track, onPlayback, displayArtwork = false }: PlaylistItemProps) {
  const [trackHover, setTrackHover] = useState(false);
  const nowPlayingTrack = usePlayerStore((state) => state.selectedTrack);
  const playState = usePlayerStore((state) => state.playState);
  const trackArtist = getTrackArtists(track);

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
      />
    </UnstyledButton>
  );
  const albumArt = (
    <Image
      withPlaceholder
      src={`${getConfig().publicRuntimeConfig.apiBaseUrl}${track?.album.artworks.small}`}
      alt={`Album art of ${track.album}`}
    />
  );
  const defaultLeftSection = displayArtwork ? albumArt : <Text fz="lg">{track.trackNumber}</Text>;
  const spinningDisc = (
    <Text fz="lg" className={styles.spinningDisc}>
      <IconDisc strokeWidth={1.3} size={24} />
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
        <Text fz="xs">{trackArtist}</Text>
      </div>
      <div className={styles.duration}>
        <Text fz="xs">{formatSecondsToSingleMinutes(track.durationInSeconds)}</Text>
      </div>
    </div>
  );
}
