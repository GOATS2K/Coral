import { TrackDto } from "../../client/schemas";
import { Initializer, usePlayerStore } from "../../store";
import styles from "../../styles/Playlist.module.css";
import { PlaylistItem } from "./PlaylistItem";

type PlaylistProps = {
  tracks?: TrackDto[];
  initializer: Initializer;
  displayArtwork?: boolean;
};

export default function Playlist({ tracks, initializer, displayArtwork = false }: PlaylistProps) {
  if (tracks == null) {
    return <p>No tracks in playlist</p>;
  }

  // trigger initiailization check on playback
  const onPlayback = () => {
    const playerInitializer = usePlayerStore.getState().initializer;
    if (playerInitializer.id != initializer.id || playerInitializer.source != initializer.source) {
      usePlayerStore.setState({ tracks: tracks, initializer: initializer });
    }
  };

  const playlistItems = tracks
    .sort((a, b) => a.trackNumber - b.trackNumber)
    .map((track) => {
      return (
        <PlaylistItem
          track={track}
          key={track.id}
          onPlayback={onPlayback}
          displayArtwork={displayArtwork}
        />
      );
    });

  return <div className={styles.wrapper}>{playlistItems}</div>;
}
