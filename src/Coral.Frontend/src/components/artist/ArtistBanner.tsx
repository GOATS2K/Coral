import { Image, Text, Title } from "@mantine/core";
import getConfig from "next/config";
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

  const releaseWithArtwork = artist?.releases.find((a) => a.artworks.original !== "");
  return (
    <div
      className={styles.bannerBackground}
      style={{
        backgroundImage: `url(${getConfig().publicRuntimeConfig.apiBaseUrl}${
          releaseWithArtwork?.artworks.original
        })`,
      }}
    >
      <div className={styles.bannerWrapper}>
        {releaseWithArtwork && (
          <Image
            className={styles.bannerImage}
            src={`${getConfig().publicRuntimeConfig.apiBaseUrl}${
              releaseWithArtwork?.artworks.medium
            }`}
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
