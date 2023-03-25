import { Image, Text } from "@mantine/core";
import { useAlbumArtwork } from "../../client/components";
import { AlbumDto } from "../../client/schemas";
import { getAlbumArtists, getAlbumDuration, getAlbumGenre } from "../../common/album";
import styles from "../../styles/AlbumInfo.module.css";

type AlbumInfoProps = {
  album?: AlbumDto;
};

export default function AlbumInfo({ album }: AlbumInfoProps) {
  const { data } = useAlbumArtwork(
    {
      pathParams: {
        albumId: album != null ? album.id : "",
      },
    },
    {
      enabled: album != null,
    }
  );

  if (album == null) {
    return <Text fz={32}>Cannot get album info...</Text>;
  }

  return (
    <div
      className={styles.background}
      style={{
        // background: "linear-gradient(135deg, #ABB7B7, #724434, #004365)",
        backgroundImage: `url(${data?.original})`,
      }}
    >
      <div className={styles.wrapper}>
        <Image alt="Album cover" withPlaceholder width={250} height={250} src={data?.medium} />
        <div className={styles.metadataWrapper}>
          <div className={styles.metadata}>
            <Text fw={700} color="white" fz={32}>
              {album.name}
            </Text>
            <Text fz={20} color="white">
              {getAlbumArtists(album)}
            </Text>
          </div>
          <div className="attributes">
            <Text fz={16} className="attribute">
              {album.releaseYear}
            </Text>
            <Text fz={16} className="attribute">
              {album.tracks?.length} tracks
            </Text>
            <Text fz={16} className="attribute">
              {getAlbumDuration(album)}
            </Text>
            <Text fz={16} className="attribute">
              {getAlbumGenre(album)}
            </Text>
          </div>
        </div>
      </div>
    </div>
  );
}
