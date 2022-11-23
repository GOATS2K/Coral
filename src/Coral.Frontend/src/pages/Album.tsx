import React from 'react'
import { RepositoryService, TrackDto } from '../client';
import Player from '../components/Player'

export default function Album() {
  const [album, setAlbum] = React.useState([] as TrackDto[]);
  React.useEffect(() => {
    const getAlbum = async () => {
      let albums = await RepositoryService.getApiRepositoryAlbums();
      setAlbum(albums[5].tracks);
    }
    getAlbum();
  }, [])

  return (
    <Player tracks={album}></Player>
  )
}
