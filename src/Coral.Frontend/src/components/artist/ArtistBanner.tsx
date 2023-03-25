import { Image, Text, Title } from "@mantine/core";
import { useAlbumArtwork } from "../../client/components";
import { ArtistDto } from "../../client/schemas";
import styles from "../../styles/Artist.module.css";

type ArtistBannerProps = {
  artist?: ArtistDto;
};

export default function ArtistBanner({ artist }: ArtistBannerProps) {
  const mainReleaseCount = artist?.releases.length;
  const featuredInCount = artist?.featuredIn.length;
  const remixerInCount = artist?.remixerIn.length;
  const compilationCount = artist?.inCompilation.length;

  const totalReleaseCount =
    Number(mainReleaseCount) +
    Number(featuredInCount) +
    Number(remixerInCount) +
    Number(compilationCount);

  const releaseWithArtwork = artist?.releases.find((a) => a.hasArtwork === true)?.id;

  const { data } = useAlbumArtwork(
    {
      pathParams: {
        albumId: releaseWithArtwork != null ? releaseWithArtwork : "",
      },
    },
    {
      enabled: releaseWithArtwork != null,
    }
  );

  return (
    <div
      className={styles.bannerBackground}
      style={{
        backgroundImage: `url(${data?.original})`,
      }}
    >
      <div className={styles.bannerWrapper}>
        {releaseWithArtwork && (
          <Image
            className={styles.bannerImage}
            src={data?.original}
            alt=""
            height={150}
            width={150}
            radius={100}
            withPlaceholder
          />
        )}
        <div>
          <Title className={styles.bannerTitle} color="white" order={1}>
            {artist?.name}
          </Title>
          <div className="attributes">
            <Text className="attribute">{totalReleaseCount} releases</Text>
          </div>
        </div>
      </div>
    </div>
  );
}
