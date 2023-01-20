import React, { forwardRef } from "react";
import { Player, polyfill } from "shaka-player";

export type ShakaPlayerRef = {
  player: () => Player | undefined;
  audioRef: () => HTMLMediaElement | null;
};

export type ShakaPlayerProps = {
  source: string;
  playState: boolean;
  mimeType?: string;
  onTimeUpdate: (timeStamp?: number) => void;
  onPlay: () => void;
  onEnd: () => void;
};

export const ShakaPlayer = forwardRef(
  (
    {
      source,
      mimeType,
      playState,
      onTimeUpdate,
      onPlay,
      onEnd,
    }: ShakaPlayerProps,
    ref
  ) => {
    const [player, setPlayer] = React.useState<Player>();
    const playerRef = React.useRef<HTMLAudioElement>(null);
    // needed for mp2t playback in Chrome
    const muxjs = require("mux.js");
    window.muxjs = muxjs;

    React.useImperativeHandle(ref, () => ({
      player() {
        return player;
      },
      audioRef() {
        return playerRef.current;
      },
    }));

    React.useEffect(() => {
      const player = new Player(playerRef.current);
      player.configure({
        streaming: {
          bufferingGoal: 30,
          rebufferingGoal: 2,
        },
      });
      console.log("Configured player!", player.getConfiguration());
      polyfill.installAll();
      setPlayer(player);

      return () => {
        player.destroy();
      };
    }, []);

    if (playerRef.current != null) {
      playerRef.current.ontimeupdate = () => {
        onTimeUpdate(playerRef.current?.currentTime);
      };

      playerRef.current.onplay = () => {
        onPlay();
      };

      playerRef.current.onended = () => {
        onEnd();
      };
    }

    React.useEffect(() => {
      const loadSource = async () => {
        if (player && source != null) {
          try {
            await player.load(source, 0, mimeType);
            if (playState) {
              await playerRef.current?.play();
            }
          } catch (e) {
            console.error(e);
          }
        }
      };
      loadSource();
    }, [source]);

    React.useEffect(() => {
      const togglePlayState = async () => {
        if (playState) {
          await playerRef.current?.play();
        } else {
          playerRef.current?.pause();
        }
      };
      togglePlayState();
    }, [playState]);

    return <audio ref={playerRef}></audio>;
  }
);
