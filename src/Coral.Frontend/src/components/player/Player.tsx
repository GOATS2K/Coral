/* eslint-disable react-hooks/exhaustive-deps */
import {
  Anchor,
  Image,
  Loader,
  Menu,
  Progress,
  Select,
  Slider,
  Switch,
  Text,
  UnstyledButton,
} from "@mantine/core";
import {
  IconPlayerPause,
  IconPlayerPlay,
  IconPlayerSkipBack,
  IconPlayerSkipForward,
  IconSettings,
  IconVolume2,
} from "@tabler/icons-react";
import getConfig from "next/config";
import Link from "next/link";
import React, { useState } from "react";
import { fetchLogPlayback, fetchStreamTrack } from "../../client/components";
import { StreamDto } from "../../client/schemas";
import { getLinksForArtist } from "../../common/links";
import { PlayerState, usePlayerStore } from "../../store";
import styles from "../../styles/Player.module.css";
import { formatSecondsToSingleMinutes } from "../../utils";
import { ShakaPlayer, ShakaPlayerRef } from "./ShakaPlayer";

function Player() {
  const playerRef = React.useRef<ShakaPlayerRef>(null);
  const playState = usePlayerStore((state: PlayerState) => state.playState);
  const tracks = usePlayerStore((state: PlayerState) => state.tracks);
  const initializer = usePlayerStore((state: PlayerState) => state.initializer);
  const selectedTrack = usePlayerStore((state: PlayerState) => state.selectedTrack);
  const selectedTrackArtist = usePlayerStore((state) => state.getMainArtists());
  const playerPosition = usePlayerStore((state) => state.getIndexOfSelectedTrack());

  // TODO: refactor these state calls to use a reducer at some point
  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  const [mimeType, setMimeType] = useState<string | undefined>();

  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const [buffering, setBuffering] = useState(false);
  const [bufferLength, setBufferLength] = useState(0);

  const setPlayState = (value: boolean) => usePlayerStore.setState({ playState: value });

  const [transcodeTrack, setTranscodeTrack] = useState(false);
  const [bitrate, setBitrate] = useState<string | null>("192");
  const buffered = playerRef.current?.player()?.getBufferedInfo();

  const [volume, setVolume] = useState(100);

  React.useEffect(() => {
    if (playerRef.current) {
      playerRef.current.audioRef().volume = volume / 100;
    }
  }, [volume]);

  React.useEffect(() => {
    const handleTrackChange = async () => {
      if (tracks.length == 0) {
        return;
      }

      if (!playState) {
        setStreamTrack({} as StreamDto);
        setPlayState(true);
      }

      const data = await fetchStreamTrack({
        pathParams: {
          trackId: selectedTrack.id,
        },
        queryParams: {
          bitrate: bitrate != null ? +bitrate : 192,
          transcodeTrack: transcodeTrack,
        },
      });

      // let streamTrack = await RepositoryService.streamTrack(
      //   track.id,
      //   // parse as int and claim value is not null
      //   +bitrate!,
      //   transcodeTrack
      // );
      const resp = await fetch(data.link, { method: "HEAD" });
      // because Shaka doesn't automatically detect the correct content-type
      // we need to set it ourselves
      const contentType = resp.headers.get("content-type");
      setMimeType(contentType != null ? contentType : "");
      setStreamTrack(data);

      // log playback of track
      await fetchLogPlayback({
        pathParams: {
          trackId: selectedTrack.id,
        },
      });

      // preload next track for faster skipping
      if (transcodeTrack && tracks.length > playerPosition + 1) {
        const nextTrack = tracks[playerPosition + 1];
        await fetchStreamTrack({
          pathParams: {
            trackId: nextTrack.id,
          },
          queryParams: {
            bitrate: bitrate != null ? +bitrate : 192,
            transcodeTrack: transcodeTrack,
          },
        });
      }
    };
    handleTrackChange();
  }, [tracks, selectedTrack, playerPosition, transcodeTrack, bitrate]);

  React.useEffect(() => {
    const lastBuffer = buffered?.total.at(-1)?.end;
    if (lastBuffer != null) {
      const bufferPercentage = (lastBuffer / selectedTrack.durationInSeconds) * 100;
      setBufferLength(bufferPercentage);
    }
  }, [buffered?.total]);

  if (tracks == null || tracks.length === 0) {
    return <div />;
  }

  const updatePositionState = (timestamp?: number) => {
    if (selectedTrack.durationInSeconds == null) {
      return;
    }
    const state = {
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
      const metadata = new MediaMetadata({
        title: selectedTrack.title,
        artist: selectedTrackArtist,
        album: selectedTrack.album?.name,
      });

      if (selectedTrack.album.artworks?.medium != null) {
        metadata["artwork"] = [
          {
            src: `${getConfig().publicRuntimeConfig.apiBaseUrl}${
              selectedTrack.album?.artworks.medium
            }`,
          },
        ];
      }

      // make sure we're not re-setting metadata
      // as that can cause the browser player to stop working
      const existingMetadata = navigator.mediaSession.metadata;
      if (
        existingMetadata?.artist == metadata.artist &&
        existingMetadata?.title == metadata.title &&
        existingMetadata.album == metadata.album
      ) {
        return;
      }

      navigator.mediaSession.metadata = metadata;
      navigator.mediaSession.playbackState = "playing";
      updatePositionState(playerRef.current?.audioRef()?.currentTime);

      navigator.mediaSession.setActionHandler("play", () => {
        navigator.mediaSession.playbackState = "playing";
        setPlayState(true);
      });
      navigator.mediaSession.setActionHandler("pause", () => {
        navigator.mediaSession.playbackState = "paused";
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
        const audioRef = playerRef.current?.audioRef();
        if (audioRef != null && audioRef.currentTime == 0) {
          return;
        }
        if (audioRef != null && details.seekTime != null) {
          audioRef.currentTime = details.seekTime;
          // updatePositionState(details.seekTime);
        }
      });
    }
  };

  const nextTrack = () => {
    usePlayerStore.getState().nextTrack();
  };

  const prevTrack = () => {
    usePlayerStore.getState().prevTrack();
  };
  const buttonSize = 32;
  const strokeSize = 1.2;

  const settingsIconSize = 24;

  const playButton = (
    <UnstyledButton onClick={() => setPlayState(!playState)}>
      {playState ? (
        <IconPlayerPause size={buttonSize} strokeWidth={strokeSize} />
      ) : (
        <IconPlayerPlay size={buttonSize} strokeWidth={strokeSize} />
      )}
    </UnstyledButton>
  );

  const loading = <Loader size={30} />;

  return (
    <div
      className={styles.wrapper}
      style={{
        display: tracks != null ? "flex" : "none",
      }}
    >
      <div className={styles.imageBox}>
        <Image
          alt={`Album cover of ${selectedTrack.album.name}`}
          src={`${getConfig().publicRuntimeConfig.apiBaseUrl}${
            selectedTrack.album?.artworks.small
          }`}
          withPlaceholder
          width="70px"
          height="70px"
        />
      </div>
      <div className={styles.imageText}>
        <Link className="link" href={`${initializer.source}/${initializer.id}`}>
          <Anchor className="link" fz="sm" fw={700} lineClamp={2}>
            {selectedTrack.title}
          </Anchor>
        </Link>
        {getLinksForArtist(selectedTrack)}
      </div>
      <div className={styles.playerWrapper}>
        <div className={styles.playerButtons}>
          <UnstyledButton onClick={prevTrack}>
            <IconPlayerSkipBack size={buttonSize} strokeWidth={strokeSize} />
          </UnstyledButton>
          {!buffering || !playState ? playButton : loading}
          <UnstyledButton onClick={nextTrack}>
            <IconPlayerSkipForward size={buttonSize} strokeWidth={strokeSize} />
          </UnstyledButton>
        </div>
        <div className={styles.playerSeekbar}>
          <Text mr={16} fz="sm">
            {formatSecondsToSingleMinutes(secondsPlayed)}
          </Text>
          <div className={styles.sliderWrapper}>
            <Progress size={4} className={styles.progressBar} value={bufferLength} />
            <Slider
              className={styles.slider}
              size={4}
              value={secondsPlayed}
              max={selectedTrack.durationInSeconds}
              onChange={(value: number) => {
                if (playerRef.current?.audioRef() != null)
                  playerRef.current.audioRef().currentTime = value;
                setSecondsPlayed(value);
                updatePositionState(value);
              }}
              label={(value: number) => formatSecondsToSingleMinutes(value)}
            />
          </div>
          <Text ml={16} fz="sm">
            {formatSecondsToSingleMinutes(selectedTrack.durationInSeconds)}
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
        onBuffer={(value) => {
          setBuffering(value);
        }}
      />
      <div className={styles.settings}>
        <Menu shadow="md" width={200} closeOnItemClick={false}>
          <Menu.Target>
            <UnstyledButton style={{ marginTop: "4px" }}>
              <IconSettings size={settingsIconSize} />
            </UnstyledButton>
          </Menu.Target>

          <Menu.Dropdown>
            <Menu.Label>Playback</Menu.Label>
            <Menu.Item
              rightSection={
                <Switch
                  checked={transcodeTrack}
                  onChange={(ev) => setTranscodeTrack(ev.currentTarget.checked)}
                />
              }
            >
              Transcode audio
            </Menu.Item>
            <Menu.Item
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
                />
              }
            >
              Bitrate
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
        <div className={styles.volumeWrapper}>
          <IconVolume2 size={settingsIconSize} />
          <Slider
            className={styles.volumeSlider}
            size={4}
            value={volume}
            onChange={setVolume}
            max={100}
          />
        </div>
      </div>
    </div>
  );
}

export default Player;
