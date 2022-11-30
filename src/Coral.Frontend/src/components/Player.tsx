import { Paper, Slider, Text, UnstyledButton, Image } from "@mantine/core";
import React, { useState } from "react";
import ReactPlayer from "react-player";
import { TrackDto, TranscodeService } from "../client";
import {
  IconPlayerSkipForward,
  IconPlayerSkipBack,
  IconPlayerPlay,
  IconPlayerPause,
} from "@tabler/icons";
import { StreamDto } from "../client/models/StreamDto";
import styles from "../styles/Player.module.css";
import { formatSecondsToMinutes } from "../utils";
import { PlayerState, usePlayerStore } from '../store';

type PlayerProps = {
  tracks: TrackDto[];
};

function Player({ tracks }: PlayerProps) {
  const playState = usePlayerStore((state: PlayerState) => state.playState);
  const selectedTrack = usePlayerStore((state: PlayerState) => state.selectedTrack);

  const setPlayState = (value: boolean) => usePlayerStore.setState({ playState: value });
  const setSelectedTrack = (track: TrackDto) => usePlayerStore.setState({ selectedTrack: track });

  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  // const [duration, setDuration] = useState(0);
  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const [playerPosition, setPlayerPosition] = useState(0);

  const updatePositionState = (timestamp?: number) => {
    if (selectedTrack.durationInSeconds == null) {
      return;
    }
    let state = {
      position: timestamp != null ? timestamp : secondsPlayed,
      duration: selectedTrack.durationInSeconds,
      playbackRate: 1,
    };
    navigator.mediaSession.setPositionState(state);
  };

  const announceMediaSession = () => {
    if (selectedTrack == null) {
      return;
    }

    if ("mediaSession" in navigator) {
      let metadata = new MediaMetadata({
        title: selectedTrack.title,
        artist: selectedTrack.artist?.name,
        album: selectedTrack.album?.name,
      });

      if (streamTrack.artworkUrl != null) {
        metadata["artwork"] = [
          {
            src: streamTrack.artworkUrl,
          },
        ];
      }

      // make sure we're not re-setting metadata
      // as that can cause the browser player to stop working
      let existingMetadata = navigator.mediaSession.metadata;
      if (
        existingMetadata?.artist == metadata.artist &&
        existingMetadata?.title == metadata.title &&
        existingMetadata.album == metadata.album
      ) {
        return;
      }

      console.info("Annoucing media session for track: ", metadata);
      navigator.mediaSession.metadata = metadata;
      updatePositionState(playerRef.current?.getCurrentTime());

      navigator.mediaSession.setActionHandler("play", () => {
        setPlayState(true);
      });
      navigator.mediaSession.setActionHandler("pause", () => {
        setPlayState(false);
      });
      navigator.mediaSession.setActionHandler("previoustrack", () => {
        prevTrack();
      });
      navigator.mediaSession.setActionHandler("nexttrack", () => {
        nextTrack();
      });

      // navigator.mediaSession.setActionHandler("seekbackward", (details) => {
      //   if (playerRef.current?.getCurrentTime() == 0) {
      //     return;
      //   }
      //   let seekTime = Math.floor(
      //     playerRef.current!.getCurrentTime() -
      //     (details.seekOffset != null ? details.seekOffset : 10)
      //   );
      //   if (seekTime < 0) {
      //     return;
      //   }
      //   playerRef.current?.seekTo(seekTime);
      //   setSecondsPlayed(seekTime);
      //   updatePositionState(seekTime);
      // });

      // navigator.mediaSession.setActionHandler("seekforward", (details) => {
      //   if (playerRef.current!.getCurrentTime() == 0) {
      //     return;
      //   }

      //   let seekTime =
      //     playerRef.current!.getCurrentTime() +
      //     (details.seekOffset != null ? details.seekOffset : 10);
      //   if (seekTime > selectedTrack.durationInSeconds) {
      //     return;
      //   }

      //   playerRef.current?.seekTo(seekTime);
      //   setSecondsPlayed(seekTime);
      //   updatePositionState(seekTime);
      // });

      navigator.mediaSession.setActionHandler("seekto", (details) => {
        if (playerRef.current!.getCurrentTime() == 0) {
          return;
        }
        if (details.seekTime != null) {
          playerRef.current?.seekTo(details.seekTime);
          setSecondsPlayed(details.seekTime);
          // updatePositionState(details.seekTime);
        }
      });
    }
  };

  React.useEffect(() => {
    if (selectedTrack == null) {
      return;
    }
    // selectedTrack was modifed by the player controls
    if (tracks.indexOf(selectedTrack) === playerPosition) {
      return;
    }
    // selectedTrack was modified by the playlist
    setPlayerPosition(tracks.indexOf(selectedTrack))
  }, [selectedTrack])

  React.useEffect(() => {
    const handleTrackChange = async () => {
      let track = tracks[playerPosition];
      if (track != null) {
        setSelectedTrack(track);
        let streamTrack = await TranscodeService.transcodeTrack(track.id);
        setStreamTrack(streamTrack);
      }
    };
    handleTrackChange();
  }, [tracks, playerPosition]);

  const nextTrack = () => {
    if (playerPosition !== tracks.length - 1) {
      setPlayerPosition(playerPosition + 1);
    } else {
      // stop playing when we've reached the end
      setPlayState(false);
    }
  };

  const prevTrack = () => {
    if (playerPosition !== 0) {
      setPlayerPosition(playerPosition - 1);
    }
  };

  const playerRef = React.useRef<ReactPlayer>(null);
  const buttonSize = 32;
  const strokeSize = 1.2;

  return (
    <Paper shadow="xs" radius="md" p="md" className={styles.wrapper}>
      <div className={styles.imageBox}>
        <Image
          src={streamTrack.artworkUrl}
          withPlaceholder
          width={"70px"}
          height={"70px"}
        ></Image>
      </div>
      <div className={styles.imageText}>
        <Text fz="sm" fw={700} lineClamp={2}>
          {selectedTrack.title}
        </Text>
        <Text fz="xs">{selectedTrack.artist?.name}</Text>
      </div>
      <div className={styles.playerWrapper}>
        <div className={styles.playerButtons}>
          <UnstyledButton onClick={prevTrack}>
            <IconPlayerSkipBack
              size={buttonSize}
              strokeWidth={strokeSize}
            ></IconPlayerSkipBack>
          </UnstyledButton>

          <UnstyledButton onClick={() => setPlayState(!playState)}>
            {playState ? (
              <IconPlayerPause
                size={buttonSize}
                strokeWidth={strokeSize}
              ></IconPlayerPause>
            ) : (
              <IconPlayerPlay
                size={buttonSize}
                strokeWidth={strokeSize}
              ></IconPlayerPlay>
            )}
          </UnstyledButton>

          <UnstyledButton onClick={nextTrack}>
            <IconPlayerSkipForward
              size={buttonSize}
              strokeWidth={strokeSize}
            ></IconPlayerSkipForward>
          </UnstyledButton>
        </div>
        <div className={styles.playerSeekbar}>
          <Text mr={16} fz={"sm"}>
            {formatSecondsToMinutes(secondsPlayed)}
          </Text>
          <Slider
            className={styles.slider}
            size={4}
            value={secondsPlayed}
            max={selectedTrack.durationInSeconds}
            onChange={(value: number) => {
              playerRef.current?.seekTo(value);
              setSecondsPlayed(value);
              updatePositionState(value);
            }}
            label={(value: number) => formatSecondsToMinutes(value)}
          ></Slider>
          <Text ml={16} fz={"sm"}>
            {formatSecondsToMinutes(selectedTrack.durationInSeconds!)}
          </Text>
        </div>
      </div>
      <ReactPlayer
        ref={playerRef}
        url={streamTrack.link}
        playing={playState}
        onPlay={() => announceMediaSession()}
        onProgress={(state) => {
          setSecondsPlayed(state.playedSeconds);
        }}
        onError={(error, data, hlsInstance) => {
          console.log({ error, data, hlsInstance });
        }}
        onEnded={() => nextTrack()}
        width={0}
        height={0}
        style={{ display: "none" }}
      ></ReactPlayer>
    </Paper>
  );
}

export default Player;
