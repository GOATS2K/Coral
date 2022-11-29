import React from "react";
import { RepositoryService, TrackDto } from "../../client";
import { useRouter } from "next/router";
import dynamic from "next/dynamic";
import Playlist from "../../components/Playlist";

export default function Album() {
  const [album, setAlbum] = React.useState([] as TrackDto[]);
  const router = useRouter();
  let { id } = router.query;
  React.useEffect(() => {
    const getAlbum = async () => {
      let albums = await RepositoryService.getAlbums();
      let targetId = id != null ? +id : 1;
      let targetAlbum = albums.find((a) => a.id === targetId);
      if (targetAlbum != null) {
        setAlbum(targetAlbum?.tracks);
      }
    };
    getAlbum();
  }, [id]);

  const Player = dynamic(() => import("../../components/Player"), {
    ssr: false,
  });

  return (
    <div>
      <Playlist tracks={album}></Playlist>
      <Player tracks={album}></Player>
    </div>
  );
}
