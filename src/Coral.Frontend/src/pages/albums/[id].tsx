import React, { useState } from "react";
import { useRouter } from "next/router";
import dynamic from "next/dynamic";
import Playlist from "../../components/Playlist";
import AlbumInfo from "../../components/AlbumInfo";
import { useAlbum } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import { Initializer, PlayerInitializationSource } from "../../store";

export default function Album() {
  const router = useRouter();
  const [albumId, setAlbumId] = useState(1);
  const initializer = {
    id: albumId,
    source: PlayerInitializationSource.Album,
  } as Initializer;

  React.useEffect(() => {
    if (!router.isReady) return;
    let { id } = router.query;
    setAlbumId(+id!);
  }, [router.isReady]);

  const { data, isLoading, error } = useAlbum({
    pathParams: {
      albumId: albumId,
    },
  });

  if (isLoading) {
    return CenteredLoader();
  }

  if (error) {
    console.error(error);
    return <div>Something went wrong loading the album...</div>;
  }

  return (
    <div>
      <AlbumInfo album={data}></AlbumInfo>
      <Playlist tracks={data?.tracks} initializer={initializer}></Playlist>
    </div>
  );
}
