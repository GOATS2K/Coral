import {
  Paper,
  Slider,
  Text,
  UnstyledButton,
  Image,
  ColorSchemeProvider,
  useMantineTheme,
  Menu,
  Switch,
  Select,
} from "@mantine/core";
import React, { useState } from "react";
import {
  OpenAPI,
  RepositoryService,
  TrackDto,
  TranscodeService,
} from "../client";
import {
  IconPlayerSkipForward,
  IconPlayerSkipBack,
  IconPlayerPlay,
  IconPlayerPause,
  IconSettings,
} from "@tabler/icons";
import { StreamDto } from "../client/models/StreamDto";
import styles from "../styles/Player.module.css";
import { formatSecondsToMinutes } from "../utils";
import { PlayerState, usePlayerStore } from "../store";
import { ShakaPlayer, ShakaPlayerRef } from "../components/ShakaPlayer";
import axios from "axios";
type PlayerProps = {
  tracks: TrackDto[];
};

function Player({ tracks }: PlayerProps) {
  const theme = useMantineTheme();

  const playState = usePlayerStore((state: PlayerState) => state.playState);
  const selectedTrack = usePlayerStore(
    (state: PlayerState) => state.selectedTrack
  );

  const setPlayState = (value: boolean) =>
    usePlayerStore.setState({ playState: value });
  const setSelectedTrack = (track: TrackDto) =>
    usePlayerStore.setState({ selectedTrack: track });

  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  const [mimeType, setMimeType] = useState<string | undefined>();

  // const [duration, setDuration] = useState(0);
  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const [playerPosition, setPlayerPosition] = useState(0);

  const [transcodeTrack, setTranscodeTrack] = useState(false);
  const [bitrate, setBitrate] = useState<string | null>("192");

  const updatePositionState = (timestamp?: number) => {
    if (selectedTrack.durationInSeconds == null) {
      return;
    }
    let state = {
      position: timestamp,
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
      updatePositionState(playerRef.current?.audioRef()?.currentTime);

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
        if (playerRef.current!.audioRef()?.currentTime == 0) {
          return;
        }
        if (details.seekTime != null) {
          playerRef.current!.audioRef()!.currentTime = details.seekTime;
          // updatePositionState(details.seekTime);
        }
      });
    }
  };

  React.useEffect(() => {
    if (selectedTrack == null) {
      return;
    }
    let currentTrackIndex = tracks?.indexOf(selectedTrack);
    // selectedTrack was modifed by the player controls
    if (currentTrackIndex === playerPosition) {
      return;
    }

    if (currentTrackIndex < 0) {
      // the track array hasn't fully loaded yet
      return;
    }
    // selectedTrack was modified by the playlist
    setPlayerPosition(currentTrackIndex);
  }, [selectedTrack]);

  React.useEffect(() => {
    const handleTrackChange = async () => {
      if (tracks == null) {
        return;
      }

      if (playerPosition !== 0 && !playState) {
        setStreamTrack({} as StreamDto);
        setPlayState(true);
      }

      let track = tracks[playerPosition];
      if (track != null) {
        setSelectedTrack(track);
        let streamTrack = await RepositoryService.streamTrack(
          track.id,
          // parse as int and claim value is not null
          +bitrate!,
          transcodeTrack
        );
        let resp = await axios.head(streamTrack.link);
        // because Shaka doesn't automatically detect the correct content-type
        // we need to set it ourselves
        let contentType = resp.headers["content-type"];
        setMimeType(contentType);
        setStreamTrack(streamTrack);
      }

      // preload next track for faster skipping
      if (
        transcodeTrack &&
        track != null &&
        tracks.length > playerPosition + 1
      ) {
        let nextTrack = tracks[playerPosition + 1];
        await RepositoryService.streamTrack(
          nextTrack.id,
          +bitrate!,
          transcodeTrack
        );
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

  const playerRef = React.useRef<ShakaPlayerRef>(null);
  const buttonSize = 32;
  const strokeSize = 1.2;

  return (
    <div
      className={styles.wrapper}
      style={{
        background: theme.colors.dark[7],
      }}
    >
      <div className={styles.imageBox}>
        <Image
          src={`${OpenAPI.BASE}/api/repository/albums/${selectedTrack.album?.id}/artwork`}
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
              playerRef.current!.audioRef()!.currentTime = value;
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
      <ShakaPlayer
        ref={playerRef}
        playState={playState}
        source={streamTrack.link}
        mimeType={mimeType}
        onTimeUpdate={(duration) => {
          if (duration) {
            setSecondsPlayed(duration);
          }
        }}
        onPlay={() => {
          announceMediaSession();
        }}
        onEnd={() => {
          nextTrack();
        }}
      ></ShakaPlayer>
      <div className={styles.settings}>
        <Menu shadow="md" width={200} closeOnItemClick={false}>
          <Menu.Target>
            <UnstyledButton>
              <IconSettings></IconSettings>
            </UnstyledButton>
          </Menu.Target>

          <Menu.Dropdown>
            <Menu.Label>Playback</Menu.Label>
            <Menu.Item
              rightSection={
                <Switch
                  checked={transcodeTrack}
                  onChange={(ev) => setTranscodeTrack(ev.currentTarget.checked)}
                ></Switch>
              }
            >
              Transcode audio
            </Menu.Item>
            <Menu.Item
              disabled={!transcodeTrack}
              rightSection={
                <Select
                  style={{
                    marginLeft: "auto",
                    maxWidth: "65%",
                    alignSelf: "end",
                  }}
                  data={["128", "192", "256", "320"]}
                  value={bitrate}
                  onChange={setBitrate}
                ></Select>
              }
            >
              Bitrate
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
      </div>
    </div>
  );
}

export default Player;
