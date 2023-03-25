import { Text } from "@mantine/core";
import { useEffect } from "react";
import { useSearch } from "../../client/components";
import { SearchResult } from "../../client/schemas";
import { CenteredLoader } from "../../common/ui";
import { useSearchStore } from "../../store";
import AlbumList from "./AlbumList";
import ArtistList from "./ArtistList";
import TrackList from "./TrackList";

type SearchProps = {
  searchString: string;
};

export default function Search({ searchString }: SearchProps) {
  const lastResult = useSearchStore((state) => state.result);
  const searchPage = (inc?: SearchResult) => {
    return (
      <div>
        <ArtistList artists={inc?.artists} />
        <AlbumList albums={inc?.albums} />
        <TrackList tracks={inc?.tracks} />
      </div>
    );
  };

  const { data, isLoading, error } = useSearch(
    {
      queryParams: {
        query: searchString,
      },
    },
    { enabled: searchString != "" }
  );

  useEffect(() => {
    if (searchString != "") {
      useSearchStore.setState({ query: searchString, result: data });
    }
  }, [searchString, data]);

  if (searchString == "" && lastResult.tracks == null) {
    return <Text>What are you looking for?</Text>;
  }

  if (searchString == "" && lastResult.tracks != null) {
    return searchPage(lastResult);
  }

  if (isLoading) {
    return <CenteredLoader />;
  }

  if (error) {
    return <div>Something went wrong trying to search...</div>;
  }

  return searchPage(data);
}
