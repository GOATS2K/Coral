import { Paper, Slider, Text, UnstyledButton, Image } from '@mantine/core';
import React, { useState } from 'react'
import ReactPlayer from 'react-player';
import { RepositoryService, TrackDto, TranscodeService } from '../client'
import { IconPlayerSkipForward, IconPlayerSkipBack, IconPlayerPlay, IconPlayerPause } from '@tabler/icons'
import dayjs from 'dayjs'
import duration from 'dayjs/plugin/duration'
import { StreamDto } from '../client/models/StreamDto';
import axios from 'axios';

dayjs.extend(duration)

async function getTracks(): Promise<TrackDto[]> {
  return await RepositoryService.getApiRepositoryTracks();
}

function formatSecondsToMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format('mm:ss')
}

export default function Player() {
  const [tracks, setTracks] = useState<TrackDto[]>([] as TrackDto[]);
  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  const [playState, setPlayState] = useState(false);
  const [selectedTrack, setSelectedTrack] = useState({} as TrackDto);
  // const [duration, setDuration] = useState(0);
  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const playerRef = React.useRef<ReactPlayer>(null);

  const buttonSize = 32
  const strokeSize = 1.2

  React.useEffect(() => {
    const getApiTracks = async () => {
      let tracks = await getTracks()
      setTracks(tracks)
    }
    getApiTracks()
  }, [])

  React.useEffect(() => {
    if (tracks?.length === 0 || tracks == null) return;
    setSelectedTrack(tracks[22])
    if (selectedTrack == null) return;

    const getTrackPlaylist = async () => {
      console.log("Getting stream for track: ", selectedTrack)
      let streamTrack = await TranscodeService.getApiTranscodeTracks(selectedTrack.id!)
      // validate stream is live
      while (true) {
        try {
          let streamResult = await axios.get(streamTrack.link)
          if (streamResult.status === 200) {
            break;
          }
        } catch (error) {
          await new Promise(r => setTimeout(r, 500));
        }
      }
      setStreamTrack(streamTrack)
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
        <div style={{ maxWidth: "70px", marginRight: "8px" }}>
          <Image src={streamTrack.artworkUrl} withPlaceholder width={"70px"} height={"70px"}></Image>
        </div>
        <div style={{
          // vertically center
          margin: "auto 0",
        }}>
          <Text fz="sm" fw={700} lineClamp={1}>{selectedTrack.title}</Text>
          <Text fz="xs">{selectedTrack.artist?.name}</Text>
        </div>
        <div style={{
          display: "flex",
          flexDirection: "column",
          minWidth: "50%",
          // horizontally center
          margin: "0 auto",
        }}>
          <div style={{
            display: "flex",
            columnGap: "18px",
            // horizontally center
            margin: "0 auto",
            marginBottom: "4px"
          }}>
            <IconPlayerSkipBack size={buttonSize} strokeWidth={strokeSize}></IconPlayerSkipBack>
            <UnstyledButton onClick={() => setPlayState(!playState)}>
              {playState ? <IconPlayerPause size={buttonSize} strokeWidth={strokeSize}></IconPlayerPause> : <IconPlayerPlay size={buttonSize} strokeWidth={strokeSize}></IconPlayerPlay>}
            </UnstyledButton>
            <IconPlayerSkipForward size={buttonSize} strokeWidth={strokeSize}></IconPlayerSkipForward>
          </div>
          <div style={{ minWidth: "75%", display: "flex", flexDirection: "row" }}>
            <Text mr={16} fz={"sm"}>{formatSecondsToMinutes(secondsPlayed)}</Text>
            <Slider style={{ flex: 1, margin: "auto 0" }} size={4} value={secondsPlayed} max={selectedTrack.durationInSeconds} onChange={(value) => {
              playerRef.current?.seekTo(value)
              setSecondsPlayed(value)
            }} label={(value) => formatSecondsToMinutes(value)}></Slider>
            <Text ml={16} fz={"sm"}>{formatSecondsToMinutes(selectedTrack.durationInSeconds!)}</Text>
          </div>
        </div>
      </Paper >
      <ReactPlayer ref={playerRef} url={streamTrack.link} playing={playState} onProgress={(state) => {
        setSecondsPlayed(state.playedSeconds)
      }} onError={(error, data, hlsInstance) => {
        console.log({ error, data, hlsInstance })
      }}></ReactPlayer>
    </div >
  )
}
