import React from 'react'
import { Image, Paragraph, Slider, Stack, Text, XStack, YStack } from '@coral/ui'
import { ArtistWithRoleDto, TrackDto } from 'app/client/schemas'

const getMainArists = (artists: ArtistWithRoleDto[]) => {
  return artists
    .filter((a) => a.role === 'Main')
    .map((a) => a.name)
    .join(', ')
}

export function Player() {
  // get state from zustand
  const trackDto = {
    title: 'So Much Better',
    trackNumber: 3,
    id: 1,
    durationInSeconds: 291,
    album: {
      artists: [{ id: 1, name: 'Satl' }],
      artworks: {
        original: 'https://f4.bcbits.com/img/a3244899140_10.jpg',
      },
    },
    artists: [
      {
        id: 1,
        name: 'Satl',
        role: 'Main',
      },
    ],
  } as TrackDto
  return (
    <YStack padding="$6" marginHorizontal="auto">
      <Image
        maxHeight={350}
        maxWidth={350}
        resizeMode="contain"
        source={{ uri: trackDto.album.artworks.original, height: 1200, width: 1200 }}
        marginBottom="$6"
      />
      <YStack justifyContent="center" alignItems="center" marginBottom="$6">
        <Paragraph fontWeight={'800'} fontSize={'$5'}>
          {trackDto.title}
        </Paragraph>
        <Paragraph fontSize={'$5'}>{getMainArists(trackDto.artists)}</Paragraph>
      </YStack>
      <Slider
        dir="ltr"
        size="$5"
        disabled={false}
        step={1}
        max={trackDto.durationInSeconds}
        marginBottom="$2"
      >
        <Slider.Track>
          <Slider.TrackActive />
        </Slider.Track>
        <Slider.Thumb circular index={0} size="$1" />
      </Slider>
      <XStack justifyContent="space-between">
        <Paragraph>0:00</Paragraph>
        <Paragraph>4:51</Paragraph>
      </XStack>
    </YStack>
  )
}
