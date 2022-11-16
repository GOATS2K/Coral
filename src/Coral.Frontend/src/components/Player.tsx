import { Button, Flex, Paper, Slider, Text, UnstyledButton } from '@mantine/core';
import React, { useState } from 'react'
import ReactPlayer from 'react-player';
import { RepositoryService, TrackDto, TranscodeService } from '../client'
import { IconPlayerSkipForward, IconPlayerSkipBack, IconPlayerPlay, IconPlayerPause } from '@tabler/icons'

async function getTracks(): Promise<TrackDto[]> {
  return await RepositoryService.getApiRepositoryTracks();
}

export default function Player() {
  const [tracks, setTracks] = useState<TrackDto[]>([] as TrackDto[]);
  const [playlist, setPlaylist] = useState("");
  const [playState, setPlayState] = useState(false);
  const [selectedTrack, setSelectedTrack] = useState({} as TrackDto);
  const [duration, setDuration] = useState(0);
  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const playerRef = React.useRef<ReactPlayer>(null);

  React.useEffect(() => {
    const getApiTracks = async () => {
      let tracks = await getTracks()
      setTracks(tracks)
    }
    getApiTracks()
  }, [])

  React.useEffect(() => {
    if (tracks?.length === 0 || tracks == null) return;
    setSelectedTrack(tracks[1])
    if (selectedTrack == null) return;

    const getTrackPlaylist = async () => {
      let playlistUrl = await TranscodeService.getApiTranscodeTracks(selectedTrack.id!)
      setPlaylist(playlistUrl?.link!)
    }
    getTrackPlaylist()
  }, [tracks, selectedTrack])

  return (
    <div>
      <Paper shadow="xs" radius="md" p="md" style={{
        display: "flex",
        flexDirection: "row",
        flexWrap: "wrap",
      }}>
        <div style={{
          // vertically center
          margin: "auto 0"
        }}>
          <Text fz="md">{selectedTrack.title}</Text>
          <Text fz="xs">{selectedTrack.artist?.name}</Text>
        </div>
        <div style={{
          display: "flex",
          flexDirection: "column",
          minWidth: "600px",
          // horizontally center
          margin: "0 auto",
        }}>
          <div style={{
            display: "flex",
            width: "30%",
            justifyContent: "space-between",
            // horizontally center
            margin: "0 auto",
            marginBottom: "12px"
          }}>
            <IconPlayerSkipBack size={36}></IconPlayerSkipBack>
            <UnstyledButton onClick={() => setPlayState(!playState)}>
              {playState ? <IconPlayerPause size={36}></IconPlayerPause> : <IconPlayerPlay size={36}></IconPlayerPlay>}
            </UnstyledButton>
            <IconPlayerSkipForward size={36}></IconPlayerSkipForward>
          </div>
          <div>
            <Slider size={6} value={secondsPlayed} max={duration} onChange={(value) => {
              console.log("onChange called:", value)
              playerRef.current?.seekTo(value)
              setSecondsPlayed(value)
            }}></Slider>
          </div>
        </div>
      </Paper >
      <ReactPlayer ref={playerRef} url={playlist} playing={playState} onDuration={(playerDuration) => setDuration(playerDuration)} onProgress={(state) => {
        setSecondsPlayed(state.playedSeconds)
      }}></ReactPlayer>
    </div >
  )
}
