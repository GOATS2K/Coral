import React from "react";
import { useAlbums } from "../../client/components";
import AlbumList from "../../components/AlbumList";

export default function Albums() {
  // get list of albums
  const { data, isLoading, error } = useAlbums({});

  return <AlbumList albums={data}></AlbumList>;
}
