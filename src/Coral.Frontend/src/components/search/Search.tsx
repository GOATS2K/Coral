import { TextInput, Title } from "@mantine/core";
import React, { useEffect, useState } from "react";
import { useSearch } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import { useSearchStore } from "../../store";
import AlbumList from "./AlbumList";
import ArtistList from "./ArtistList";
import TrackList from "./TrackList";
import styles from "../../styles/Search.module.css";

type SearchProps = {
  searchString: string;
};

export default function Search({ searchString }: SearchProps) {
  const { data, isLoading, error } = useSearch({
    queryParams: {
      query: searchString,
    },
  });

  useEffect(() => {
    useSearchStore.setState({ query: searchString, result: data });
  }, [searchString, data]);

  if (searchString == "") {
    return <div>Search away!</div>;
  }

  if (isLoading) {
    return <CenteredLoader></CenteredLoader>;
  }

  if (error) {
    return <div>Something went wrong trying to search...</div>;
  }

  return (
    <div>
      <ArtistList artists={data?.artists}></ArtistList>
      <AlbumList albums={data?.albums}></AlbumList>
      <TrackList tracks={data?.tracks}></TrackList>
    </div>
  );
}
