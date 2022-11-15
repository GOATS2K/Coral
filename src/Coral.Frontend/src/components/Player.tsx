import { Button } from '@mantine/core';
import React, { useState } from 'react'
import ReactPlayer from 'react-player';
import { RepositoryService, TrackDto, TranscodeService } from '../client'

async function getTracks(): Promise<TrackDto[]> {
  return await RepositoryService.getApiRepositoryTracks();
}


export default function Player() {
  const [tracks, setTracks] = useState<TrackDto[]>([] as TrackDto[]);
  const [playlist, setPlaylist] = useState("");
  const [playState, setPlayState] = useState(false);

  React.useEffect(() => {
    const getApiTracks = async () => {
      let tracks = await getTracks()
      setTracks(tracks)
    }
    getApiTracks()
  }, [])

  React.useEffect(() => {
    if (tracks?.length === 0 || tracks == null) return;
    let selectedTrack = tracks[1]
    if (selectedTrack == null) return;

    const getTrackPlaylist = async () => {
      let playlistUrl = await TranscodeService.getApiTranscodeTracks(selectedTrack.id!)
      setPlaylist(playlistUrl?.link!)
    }
    getTrackPlaylist()
  }, [tracks])

  return (
    <div>
      <Button onClick={() => setPlayState(!playState)}>Play!</Button>
      <ReactPlayer url={playlist} playing={playState}></ReactPlayer>
    </div>
  )
}
