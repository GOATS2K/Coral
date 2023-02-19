import { useRouter } from "next/router";
import Playlist from "../../components/Playlist";
import AlbumInfo from "../../components/AlbumInfo";
import { useAlbum } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import { Initializer, PlayerInitializationSource } from "../../store";

export default function Album() {
  const router = useRouter();
  const initializer = {
    id: isNaN(Number(router.query.id)) ? 1 : Number(router.query.id),
    source: PlayerInitializationSource.Album,
  } as Initializer;

  const { data, isLoading, error } = useAlbum({
    pathParams: {
      albumId: +router.query.id!,
    },
  });

  if (isNaN(Number(router.query.id))) {
    return <div>You have given an invalid album ID.</div>;
  }

  if (isLoading) {
    return CenteredLoader();
  }

  if (error) {
    console.error(error);
    return <div>Something went wrong loading the album...</div>;
  }

  return (
    <div key={data?.id}>
      <AlbumInfo album={data}></AlbumInfo>
      <Playlist tracks={data?.tracks} initializer={initializer}></Playlist>
    </div>
  );
}
