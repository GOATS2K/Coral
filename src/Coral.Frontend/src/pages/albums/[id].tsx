import React from "react";
import { AlbumDto, RepositoryService, TrackDto } from "../../client";
import { useRouter } from "next/router";
import dynamic from "next/dynamic";
import Playlist from "../../components/Playlist";
import AlbumInfo from "../../components/AlbumInfo";

export default function Album() {
  const [album, setAlbum] = React.useState({} as AlbumDto);
  const router = useRouter();
  let { id } = router.query;
  React.useEffect(() => {
    const getAlbum = async () => {
      let targetId = id != null ? +id : 1;
      let targetAlbum = await RepositoryService.getAlbum(targetId);
      if (targetAlbum != null) {
        setAlbum(targetAlbum);
      }
    };
    getAlbum();
  }, [id]);

  const Player = dynamic(() => import("../../components/Player"), {
    ssr: false,
  });

  return (
    <div>
      <AlbumInfo album={album}></AlbumInfo>
      <Playlist tracks={album.tracks}></Playlist>
      <Player tracks={album.tracks}></Player>
    </div>
  );
}
