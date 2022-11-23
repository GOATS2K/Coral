import { Paper, Slider, Text, UnstyledButton, Image } from '@mantine/core';
import React, { useState } from 'react'
import ReactPlayer from 'react-player';
import { TrackDto, TranscodeService } from '../client'
import { IconPlayerSkipForward, IconPlayerSkipBack, IconPlayerPlay, IconPlayerPause } from '@tabler/icons'
import dayjs from 'dayjs'
import duration from 'dayjs/plugin/duration'
import { StreamDto } from '../client/models/StreamDto';

dayjs.extend(duration)

function formatSecondsToMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format('mm:ss')
}

type PlayerProps = {
  tracks: TrackDto[]
};

function Player({ tracks }: PlayerProps) {
  const [streamTrack, setStreamTrack] = useState({} as StreamDto);
  const [playState, setPlayState] = useState(false);
  const [selectedTrack, setSelectedTrack] = useState({} as TrackDto);
  // const [duration, setDuration] = useState(0);
  const [secondsPlayed, setSecondsPlayed] = useState(0);
  const [playerPosition, setPlayerPosition] = useState(0);

  React.useEffect(() => {
    const handleTrackChange = async () => {
      let track = tracks[playerPosition];

      if (track != null) {
        setSelectedTrack(track);
        console.info("Getting stream for track: ", track);
        let streamTrack = await TranscodeService.getApiTranscodeTracks(track.id);
        setStreamTrack(streamTrack);
      }
    }
    handleTrackChange();
  }, [tracks, selectedTrack, playerPosition])

  const nextTrack = () => {
    console.info("Next track called: ", { "position": playerPosition + 1, "targetTrack": tracks[playerPosition + 1] })
    if (playerPosition !== tracks.length - 1) {
      setPlayerPosition(playerPosition + 1);
    }
  }

  const prevTrack = () => {
    console.info("Previous track called: ", { "position": playerPosition - 1, "targetTrack": tracks[playerPosition - 1] })
    if (playerPosition !== 0) {
      setPlayerPosition(playerPosition - 1);
    }
  }

  const playerRef = React.useRef<ReactPlayer>(null);
  const buttonSize = 32
  const strokeSize = 1.2

  return (
    <div>
      <Paper shadow="xs" radius="md" p="md" style={{
        display: "flex",
        flexDirection: "row",
        flexWrap: "nowrap",
        // move to bottom of page
        position: 'absolute',
        bottom: 0,
        // restore width
        width: "100%"
      }}>
        <div style={{ maxWidth: "70px", marginRight: "8px" }}>
          <Image src={streamTrack.artworkUrl} withPlaceholder width={"70px"} height={"70px"}></Image>
        </div>
        <div style={{
          // vertically center
          alignSelf: "center",
          width: "20%"
        }}>
          <Text fz="sm" fw={700} lineClamp={2}>{selectedTrack.title}</Text>
          <Text fz="xs">{selectedTrack.artist?.name}</Text>
        </div>
        <div style={{
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          width: "50%"
        }}>
          <div style={{
            display: "flex",
            columnGap: "1.2em",
            // horizontally center
            alignSelf: "center",
            marginBottom: "4px"
          }}>
            <UnstyledButton onClick={prevTrack}>
              <IconPlayerSkipBack size={buttonSize} strokeWidth={strokeSize}></IconPlayerSkipBack>
            </UnstyledButton>

            <UnstyledButton onClick={() => setPlayState(!playState)}>
              {playState ? <IconPlayerPause size={buttonSize} strokeWidth={strokeSize}></IconPlayerPause> : <IconPlayerPlay size={buttonSize} strokeWidth={strokeSize}></IconPlayerPlay>}
            </UnstyledButton>

            <UnstyledButton onClick={nextTrack}>
              <IconPlayerSkipForward size={buttonSize} strokeWidth={strokeSize}></IconPlayerSkipForward>
            </UnstyledButton>
          </div>
          <div style={{ minWidth: "50%", display: "flex", flexDirection: "row", }}>
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

export default Player;