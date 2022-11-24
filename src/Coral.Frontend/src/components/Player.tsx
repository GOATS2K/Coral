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
import dayjs from "dayjs";
import duration from "dayjs/plugin/duration";
import { StreamDto } from "../client/models/StreamDto";

dayjs.extend(duration);

function formatSecondsToMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format("mm:ss");
}

type PlayerProps = {
  tracks: TrackDto[];
};

function Player({ tracks }: PlayerProps) {
  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  const [playState, setPlayState] = useState(false);
  const [selectedTrack, setSelectedTrack] = useState({} as TrackDto);
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
    }
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
      if (existingMetadata?.artist == metadata.artist 
        && existingMetadata?.title == metadata.title
        && existingMetadata.album == metadata.album) {
          return;
      }

      navigator.mediaSession.metadata = metadata;
      updatePositionState(0);

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

      navigator.mediaSession.setActionHandler("seekbackward", (details) => {
        if (playerRef.current?.getCurrentTime() == 0) {
          return;
        }
        let seekTime = Math.floor(
          playerRef.current!.getCurrentTime() -
            (details.seekOffset != null ? details.seekOffset : 10)
        );
        if (seekTime < 0) {
          return;
        }
        playerRef.current?.seekTo(seekTime);
        setSecondsPlayed(seekTime);
        updatePositionState(seekTime);
      });

      navigator.mediaSession.setActionHandler("seekforward", (details) => {
        if (playerRef.current!.getCurrentTime() == 0) {
          return;
        }

        let seekTime =
          playerRef.current!.getCurrentTime() +
          (details.seekOffset != null ? details.seekOffset : 10);
        if (seekTime > selectedTrack.durationInSeconds) {
          return;
        }

        playerRef.current?.seekTo(seekTime);
        setSecondsPlayed(seekTime);
        updatePositionState(seekTime);
      });

      navigator.mediaSession.setActionHandler("seekto", (details) => {
        if (playerRef.current!.getCurrentTime() == 0) {
          return;
        }
        if (details.seekTime != null) {
          playerRef.current?.seekTo(details.seekTime);
          setSecondsPlayed(details.seekTime);
          updatePositionState(details.seekTime);
        }
      });
    }
  };


  React.useEffect(() => {
    if (playState) {
      announceMediaSession();
      navigator.mediaSession.playbackState = "playing";
    } else {
      navigator.mediaSession.playbackState = "paused";
    }
  }, [playState, selectedTrack]);

  React.useEffect(() => {
    const handleTrackChange = async () => {
      let track = tracks[playerPosition];

      if (track != null) {
        setSelectedTrack(track);
        let streamTrack = await TranscodeService.getApiTranscodeTracks(
          track.id
        );
        setStreamTrack(streamTrack);
      }
    };
    handleTrackChange();
  }, [tracks, selectedTrack, playerPosition]);

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
    <div>
      <Paper
        shadow="xs"
        radius="md"
        p="md"
        style={{
          display: "flex",
          flexDirection: "row",
          flexWrap: "nowrap",
          // move to bottom of page
          position: "absolute",
          bottom: 0,
          // restore width
          width: "100%",
        }}
      >
        <div style={{ maxWidth: "70px", marginRight: "8px" }}>
          <Image
            src={streamTrack.artworkUrl}
            withPlaceholder
            width={"70px"}
            height={"70px"}
          ></Image>
        </div>
        <div
          style={{
            // vertically center
            alignSelf: "center",
            // allow metadata panel to take up 20% of the space in the flexbox
            width: "20%",
          }}
        >
          <Text fz="sm" fw={700} lineClamp={2}>
            {selectedTrack.title}
          </Text>
          <Text fz="xs">{selectedTrack.artist?.name}</Text>
        </div>
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            justifyContent: "center",
            // by allowing the container to take up 50% of the width
            // we can ensure it's fully centered
            // some funny wizardry I don't quite get yet
            width: "50%",
          }}
        >
          <div
            style={{
              display: "flex",
              columnGap: "1.2em",
              // horizontally center
              alignSelf: "center",
              marginBottom: "4px",
            }}
          >
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
          <div
            style={{
              minWidth: "75%",
              display: "flex",
              flexDirection: "row",
              alignSelf: "center",
            }}
          >
            <Text mr={16} fz={"sm"}>
              {formatSecondsToMinutes(secondsPlayed)}
            </Text>
            <Slider
              style={{ flex: 1, alignSelf: "center" }}
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
      </Paper>
      <ReactPlayer
        ref={playerRef}
        url={streamTrack.link}
        playing={playState}
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
    </div>
  );
}

export default Player;
