import { IconPlayerPlay, IconDisc, IconVinyl } from "@tabler/icons";
import { TrackDto } from "../client";
import styles from "../styles/PlaylistItem.module.css";
import { Text, UnstyledButton, useMantineTheme } from "@mantine/core";
import { formatSecondsToSingleMinutes } from "../utils";
import { useState } from "react";
import { usePlayerStore } from "../store";

type PlaylistItemProps = {
  track: TrackDto;
};

export function PlaylistItem({ track }: PlaylistItemProps) {
  const [trackHover, setTrackHover] = useState(false);
  const nowPlayingTrack = usePlayerStore((state) => state.selectedTrack);
  const playState = usePlayerStore((state) => state.playState);
  const setSelectedTrack = (track: TrackDto) =>
    usePlayerStore.setState({ selectedTrack: track });
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
  const trackNumber = <Text fz="lg">{track.trackNumber}</Text>;
  const discEmoji = (
    <Text fz="lg" className={styles.spinningDisc}>
      <IconDisc strokeWidth={1.3} size={24}></IconDisc>
    </Text>
  );

  return (
    <div
      className={styles.gridContainer}
      key={track.id.toString()}
      tabIndex={track.id}
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
        nowPlayingTrack.id === track.id
          ? {
              color: theme.colors.blue[4],
            }
          : {}
      }
    >
      <div className={styles.trackNumber}>
        {nowPlayingTrack.id !== track.id && trackHover
          ? // show play button if track is hovered over
            // and it is not playing the current selected track
            playButton
          : // show the disc emoji for the currently playing track
          nowPlayingTrack.id === track.id && playState
          ? discEmoji
          : // show track if not hovered or currently playing
            trackNumber}
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
