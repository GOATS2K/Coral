import { Title } from "@mantine/core";
import { SimpleArtistDto } from "../../client/schemas";
import styles from "../../styles/Search.module.css";
import ArtistSearchItem from "./ArtistSearchItem";

type ArtistListProps = {
  artists?: SimpleArtistDto[];
};

export default function ArtistList({ artists }: ArtistListProps) {
  const artistList = artists?.map((a) => <ArtistSearchItem artist={a} key={a.id} />);
  return (
    <div id="artistList" className={styles.artistComponentWrapper}>
      <Title order={3} className={styles.title}>
        Artists
      </Title>
      <div className={styles.artistWrapper}>{artistList}</div>
    </div>
  );
}
