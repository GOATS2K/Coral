import { Anchor, Image } from "@mantine/core";
import Link from "next/link";
import { SimpleArtistDto } from "../../client/schemas";
import styles from "../../styles/Search.module.css";

type ArtistSearchItemProps = {
  artist: SimpleArtistDto;
};

export default function ArtistSearchItem({ artist }: ArtistSearchItemProps) {
  return (
    <div className={styles.artistItem}>
      <Link href={`/artists/${artist.id}`} className="link">
        <Image withPlaceholder alt="" width={100} height={100} radius={100} />
        <Anchor className="link" lineClamp={2} fw="bold" fz="sm" style={{ textAlign: "center" }}>
          {artist.name}
        </Anchor>
      </Link>
    </div>
  );
}
