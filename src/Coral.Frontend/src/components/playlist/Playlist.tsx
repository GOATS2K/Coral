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
    usePlayerStore.setState({ tracks: tracks, initializer: initializer });
  };

  const playlistItems = tracks
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
