import { Pagination, Text } from "@mantine/core";
import { memo, useEffect } from "react";
import { useSearch } from "../../client/components";
import { SearchResult } from "../../client/schemas";
import { CenteredLoader } from "../../common/ui";
import { useSearchStore } from "../../store";
import AlbumList from "./AlbumList";
import ArtistList from "./ArtistList";
import TrackList from "./TrackList";
import styles from "../../styles/Search.module.css";

export type SearchProps = {
  query: string;
};

export const Search = memo(function Search({ query }: SearchProps) {
  const lastResult = useSearchStore((state) => state.result);
  const searchPage = (inc?: SearchResult) => {
    return (
      <div>
        <ArtistList artists={inc?.artists} />
        <AlbumList albums={inc?.albums} />
        <TrackList tracks={inc?.tracks} />
        <div className={styles.pagination}>
          <Pagination
            size="lg"
            total={pages}
            page={currentPage}
            onChange={(page) => {
              useSearchStore.setState({ currentPage: page });
              // too lazy to get refs working
              document.getElementById("artistList")?.scrollIntoView();
            }}
          />
        </div>
      </div>
    );
  };

  const currentPage = useSearchStore((state) => state.currentPage);
  const pages = useSearchStore((state) => state.pages);
  const limit = 50;
  const offset = (currentPage - 1) * limit;

  const { data, isLoading, error } = useSearch(
    {
      queryParams: {
        query: query,
        offset: offset,
        limit: limit,
      },
    },
    { enabled: query != "" }
  );

  useEffect(() => {
    if (query != "") {
      useSearchStore.setState({
        result: data?.data,
        pages: Math.ceil(Number(data?.totalRecords) / limit),
      });
    }
  }, [query, data, pages]);

  if (query == "" && lastResult.tracks == null) {
    return <Text>What are you looking for?</Text>;
  }

  if (query == "" && lastResult.tracks != null) {
    return searchPage(lastResult);
  }

  if (isLoading) {
    return <CenteredLoader />;
  }

  if (error) {
    return <div>Something went wrong trying to search...</div>;
  }

  return searchPage(data?.data);
});
