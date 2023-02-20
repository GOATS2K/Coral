import React from "react";
import { useSearch } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import AlbumList from "../../components/search/AlbumList";
import ArtistList from "../../components/search/ArtistList";
import TrackList from "../../components/search/TrackList";

export default function Search() {
  const { data, isLoading, error } = useSearch({
    queryParams: {
      query: "satl",
    },
  });

  if (isLoading) {
    return <CenteredLoader></CenteredLoader>;
  }

  return (
    <div>
      <ArtistList artists={data?.artists}></ArtistList>
      <AlbumList albums={data?.albums}></AlbumList>
      <TrackList tracks={data?.tracks}></TrackList>
    </div>
  );
}
