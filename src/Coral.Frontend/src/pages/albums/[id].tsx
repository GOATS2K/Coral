import { useRouter } from "next/router";
import Playlist from "../../components/playlist/Playlist";
import AlbumInfo from "../../components/album/AlbumInfo";
import { useAlbum } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import { Initializer, PlayerInitializationSource } from "../../store";
import { isStringObject } from "util/types";
import { IconRouter } from "@tabler/icons";

export default function Album() {
  const router = useRouter();
  const initializer = {
    id: router.query.id,
    source: PlayerInitializationSource.Album,
  } as Initializer;

  const { data, isLoading, error } = useAlbum({
    pathParams: {
      albumId: typeof router.query.id === "string" ? router.query.id : "",
    },
  });

  if (isLoading) {
    return CenteredLoader();
  }

  if (error) {
    return (
      <div>An error occurred. You may have entered an invalid album ID.</div>
    );
  }

  return (
    <div key={data?.id}>
      <AlbumInfo album={data}></AlbumInfo>
      <Playlist tracks={data?.tracks} initializer={initializer}></Playlist>
    </div>
  );
}
