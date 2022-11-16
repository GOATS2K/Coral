import { Group, Paper, Slider, Text, UnstyledButton } from '@mantine/core';
import React, { useState } from 'react'
import ReactPlayer from 'react-player';
import { RepositoryService, TrackDto, TranscodeService } from '../client'
import { IconPlayerSkipForward, IconPlayerSkipBack, IconPlayerPlay, IconPlayerPause } from '@tabler/icons'
import dayjs from 'dayjs'
import duration from 'dayjs/plugin/duration'

dayjs.extend(duration)

async function getTracks(): Promise<TrackDto[]> {
  return await RepositoryService.getApiRepositoryTracks();
}

function formatSecondsToMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format('mm:ss')
}

export default function Player() {
  const [tracks, setTracks] = useState<TrackDto[]>([] as TrackDto[]);
  const [playlist, setPlaylist] = useState("");
  const [playState, setPlayState] = useState(false);
  const [selectedTrack, setSelectedTrack] = useState({} as TrackDto);
  // const [duration, setDuration] = useState(0);
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
    setSelectedTrack(tracks[0])
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
        flexWrap: "nowrap",
      }}>
        <div style={{
          // vertically center
          margin: "auto 0"
        }}>
          <Text fz="md" fw={700}>{selectedTrack.title}</Text>
          <Text fz="xs">{selectedTrack.artist?.name}</Text>
        </div>
        <div style={{
          display: "flex",
          flexDirection: "column",
          minWidth: "70%",
          // horizontally center
          margin: "0 auto",
        }}>
          <div style={{
            display: "flex",
            maxWidth: "60%",
            columnGap: "18px",
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
          <div style={{ minWidth: "75%", display: "flex", flexDirection: "row" }}>
            <Text mr={16} fz={"sm"}>{formatSecondsToMinutes(secondsPlayed)}</Text>
            <Slider style={{ flex: 1, margin: "auto 0" }} size={6} value={secondsPlayed} max={selectedTrack.durationInSeconds} onChange={(value) => {
              playerRef.current?.seekTo(value)
              setSecondsPlayed(value)
            }} label={(value) => formatSecondsToMinutes(value)}></Slider>
            <Text ml={16} fz={"sm"}>{formatSecondsToMinutes(selectedTrack.durationInSeconds!)}</Text>
          </div>
        </div>
      </Paper >
      <ReactPlayer ref={playerRef} url={playlist} playing={playState} onProgress={(state) => {
        setSecondsPlayed(state.playedSeconds)
      }}></ReactPlayer>
    </div >
  )
}
