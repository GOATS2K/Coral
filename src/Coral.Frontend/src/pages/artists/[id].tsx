import React from "react";
import { useArtist } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import Artist from "../../components/artist/Artist";

export default function ArtistById() {
  const artistId = "3283d220-87c6-47af-9666-577f8a0f948a";
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
