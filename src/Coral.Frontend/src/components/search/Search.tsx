import { TextInput, Title } from "@mantine/core";
import React, { useEffect, useState } from "react";
import { useSearch } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import { useSearchStore } from "../../store";
import AlbumList from "./AlbumList";
import ArtistList from "./ArtistList";
import TrackList from "./TrackList";
import styles from "../../styles/Search.module.css";
import { SearchResult } from "../../client/schemas";

type SearchProps = {
  searchString: string;
};

export default function Search({ searchString }: SearchProps) {
  const lastResult = useSearchStore((state) => state.result);
  const searchPage = (inc?: SearchResult) => {
    return (
      <div>
        <ArtistList artists={inc?.artists}></ArtistList>
        <AlbumList albums={inc?.albums}></AlbumList>
        <TrackList tracks={inc?.tracks}></TrackList>
      </div>
    );
  };

  const { data, isLoading, error } = useSearch({
    queryParams: {
      query: searchString,
    },
  });

  useEffect(() => {
    if (searchString != "") {
      useSearchStore.setState({ query: searchString, result: data });
    }
  }, [searchString, data]);

  if (searchString == "" && lastResult != null) {
    return searchPage(lastResult);
  }

  if (isLoading) {
    return <CenteredLoader></CenteredLoader>;
  }

  if (error) {
    return <div>Something went wrong trying to search...</div>;
  }

  return searchPage(data);
}
