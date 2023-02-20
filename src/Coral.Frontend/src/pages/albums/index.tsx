import React from "react";
import { useAlbums } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import AlbumList from "../../components/album/AlbumList";

export default function Albums() {
  // get list of albums
  const { data, isLoading, error } = useAlbums({});

  if (isLoading) {
    return CenteredLoader();
  }

  return <AlbumList albums={data}></AlbumList>;
}
