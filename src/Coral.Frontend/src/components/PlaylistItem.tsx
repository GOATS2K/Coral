import { IconPlayerPlay } from '@tabler/icons';
import { TrackDto } from '../client';
import styles from '../styles/Playlist.module.css';
import {Text} from '@mantine/core';
import { formatSecondsToSingleMinutes } from '../utils';
import { useState } from 'react';

type PlaylistItemProps = {
    track: TrackDto
};

export function PlaylistItem({track}: PlaylistItemProps) {
    const [trackHover, setTrackHover] = useState(false);
    return <div className={styles.gridContainer}
      key={track.id}
      id={track.id.toString()}
      onMouseEnter={() => setTrackHover(true)}
      onMouseLeave={() => setTrackHover(false)}>
        <div className={styles.trackNumber}>
          {
            trackHover ? <IconPlayerPlay strokeWidth={1} size={24} style={{
              // center element
              marginTop: "4px"
            }}></IconPlayerPlay> : <Text fz="lg">{track.trackNumber}</Text>
          }
        </div>
        <div className={styles.info}>
          <Text fz="sm" fw={500}>{track.title}</Text>
          <Text fz="xs">{track.artist.name}</Text>
        </div>
        <div className={styles.duration}>
          <Text fz="xs">{formatSecondsToSingleMinutes(track.durationInSeconds)}</Text>
        </div>
      </div>
}