import React from "react";
import { useArtist } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import Artist from "../../components/artist/Artist";

export default function ArtistById() {
  const artistId = "44fb2fbf-ce89-4b8d-847c-7818d4fb554d";
  const { data, isLoading, error } = useArtist({
    pathParams: {
      artistId: artistId,
    },
  });

  if (isLoading) {
    return <CenteredLoader></CenteredLoader>;
  }

  return <Artist artist={data}></Artist>;
}
