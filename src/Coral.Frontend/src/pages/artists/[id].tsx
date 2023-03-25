import { useRouter } from "next/router";
import { useArtist } from "../../client/components";
import { CenteredLoader } from "../../common/ui";
import Artist from "../../components/artist/Artist";

export default function Artists() {
  const router = useRouter();
  const { data, isLoading } = useArtist({
    pathParams: {
      artistId: typeof router.query.id === "string" ? router.query.id : "",
    },
  });

  if (isLoading) {
    return <CenteredLoader />;
  }

  return <Artist artist={data} />;
}
