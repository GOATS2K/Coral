import React, { useState } from "react";
import { useRouter } from "next/router";
import dynamic from "next/dynamic";
import Playlist from "../../components/Playlist";
import AlbumInfo from "../../components/AlbumInfo";
import { useAlbum } from "../../client/components";
import { Center, Loader } from "@mantine/core";

export default function Album() {
  const Player = dynamic(() => import("../../components/Player"), {
    ssr: false,
  });

  const router = useRouter();

  const [albumId, setAlbumId] = useState(1);

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

  React.useEffect(() => {
    console.log({ data, isLoading, error });
  }, [data, isLoading, error]);

  if (isLoading) {
    return (
      <Center style={{ height: "100vh" }}>
        <Loader></Loader>
      </Center>
    );
  }

  if (error) {
    console.error(error);
    return <div>Something went wrong loading the album...</div>;
  }

  return (
    <div>
      <AlbumInfo album={data}></AlbumInfo>
      <Playlist tracks={data?.tracks}></Playlist>
      <Player tracks={data?.tracks}></Player>
    </div>
  );
}
